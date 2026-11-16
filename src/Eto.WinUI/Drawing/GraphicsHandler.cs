using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;

namespace Eto.WinUI.Drawing;

public class GraphicsHandler : WidgetHandler<CanvasDrawingSession, Graphics>, Graphics.IHandler
{
	static readonly RectangleF DefaultClipBounds = new(-1000000f, -1000000f, 2000000f, 2000000f);

	readonly Stack<Matrix3x2> _savedTransforms = new();
	Bitmap? _image;
	CanvasRenderTarget? _renderTarget;
	IDisposable? _clipLayer;
	IDisposable? _clipResource;
	RectangleF _clipBounds = DefaultClipBounds;
	RectangleF _initialClipBounds = DefaultClipBounds;
	ImageInterpolation _imageInterpolation = ImageInterpolation.Default;
	PixelOffsetMode _pixelOffsetMode = PixelOffsetMode.None;
	bool _disposeControl;

	public GraphicsHandler()
	{
	}

	public GraphicsHandler(CanvasDrawingSession drawingSession)
	{
		InitializeDrawingSession(drawingSession);
	}

	/// <summary>Creates a handler for a session that draws within the specified bounds.</summary>
	public GraphicsHandler(CanvasDrawingSession drawingSession, RectangleF bounds)
	{
		InitializeDrawingSession(drawingSession);
		_initialClipBounds = _clipBounds = bounds;
	}

	protected override bool DisposeControl => _disposeControl;

	public float PointsPerPixel => 72f / 96f;

	public PixelOffsetMode PixelOffsetMode
	{
		get => _pixelOffsetMode;
		set => _pixelOffsetMode = value;
	}

	public bool AntiAlias
	{
		get => Control.Antialiasing == CanvasAntialiasing.Antialiased;
		set
		{
			Control.Antialiasing = value ? CanvasAntialiasing.Antialiased : CanvasAntialiasing.Aliased;
			Control.TextAntialiasing = value ? CanvasTextAntialiasing.Auto : CanvasTextAntialiasing.Aliased;
		}
	}

	public ImageInterpolation ImageInterpolation
	{
		get => _imageInterpolation;
		set => _imageInterpolation = value;
	}

	public bool IsRetained => _image != null;

	public IMatrix CurrentTransform => new MatrixHandler(Control.Transform);

	// the clip is kept in device space, but is reported in the current coordinate space
	public RectangleF ClipBounds
	{
		get
		{
			var transform = Control.Transform;
			if (transform.IsIdentity || !Matrix3x2.Invert(transform, out var inverse))
				return _clipBounds;
			return TransformRectangle(_clipBounds, inverse);
		}
	}

	// strokes are offset by half a pixel so lines on integer coordinates are crisp, like other platforms
	readonly struct StrokeOffset(CanvasDrawingSession? session, Matrix3x2 original) : IDisposable
	{
		public void Dispose()
		{
			if (session != null)
				session.Transform = original;
		}
	}

	StrokeOffset BeginStroke()
	{
		if (_pixelOffsetMode != PixelOffsetMode.None)
			return default;
		var transform = Control.Transform;
		Control.Transform = transform * Matrix3x2.CreateTranslation(0.5f, 0.5f);
		return new StrokeOffset(Control, transform);
	}

	public void Clear(SolidBrush brush)
	{
		ResetClip();
		Control.Clear(brush?.Color.ToWinUI() ?? Colors.Transparent.ToWinUI());
	}

	public void CreateFromImage(Bitmap image)
	{
		if (image.Handler is not BitmapHandler bitmapHandler)
			throw new NotSupportedException("Graphics can only draw onto a Bitmap");
		_image = image;
		_initialClipBounds = new RectangleF(0, 0, image.Width, image.Height);
		_clipBounds = _initialClipBounds;
		// the shared device is free-threaded, so bitmaps can be drawn on from any thread
		_renderTarget = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), Math.Max(image.Width, 1), Math.Max(image.Height, 1), 96f);
		if (image.Width > 0 && image.Height > 0)
			_renderTarget.SetPixelBytes(bitmapHandler.GetPremultipliedBgra());
		_disposeControl = true;
		InitializeDrawingSession(_renderTarget.CreateDrawingSession());
	}

	public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
	{
		if (Math.Abs(sweepAngle) >= 360f)
		{
			DrawEllipse(pen, x, y, width, height);
			return;
		}

		DrawFigure(pen, GetArcPoints(x, y, width, height, startAngle, sweepAngle), false);
	}

	public void DrawEllipse(Pen pen, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		using var offset = BeginStroke();
		Control.DrawEllipse(
			x + width / 2f,
			y + height / 2f,
			width / 2f,
			height / 2f,
			brushRef.Brush,
			pen.Thickness,
			strokeStyle);
	}

	public void DrawImage(Image image, float x, float y)
	{
		var size = image.Size;
		DrawImage(image, x, y, size.Width, size.Height);
	}

	public void DrawImage(Image image, float x, float y, float width, float height)
	{
		DrawImageCore(image, null, new RectangleF(x, y, width, height));
	}

	public void DrawImage(Image image, RectangleF source, RectangleF destination)
	{
		DrawImageCore(image, source, destination);
	}

	public void DrawLine(Pen pen, float startx, float starty, float endx, float endy)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		using var offset = BeginStroke();
		Control.DrawLine(startx, starty, endx, endy, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawLines(Pen pen, IEnumerable<PointF> points)
	{
		DrawFigure(pen, points, false);
	}

	// one figure so the pen's line joins are applied between segments
	void DrawFigure(Pen pen, IEnumerable<PointF> points, bool closed)
	{
		var array = points as PointF[] ?? points.ToArray();
		if (array.Length < 2)
			return;

		using var builder = new CanvasPathBuilder(Control);
		builder.BeginFigure(ToVector(array[0]));
		for (var i = 1; i < array.Length; i++)
			builder.AddLine(ToVector(array[i]));
		builder.EndFigure(closed ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);
		using var geometry = CanvasGeometry.CreatePath(builder);

		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		using var offset = BeginStroke();
		Control.DrawGeometry(geometry, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawPath(Pen pen, IGraphicsPath path)
	{
		var geometry = path.ControlObject as CanvasGeometry
			?? throw new NotSupportedException("WinUI GraphicsPath drawing requires a Win2D CanvasGeometry-backed path handler.");

		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		using var offset = BeginStroke();
		Control.DrawGeometry(geometry, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawPolygon(Pen pen, IEnumerable<PointF> points)
	{
		DrawFigure(pen, points, true);
	}

	public void DrawRectangle(Pen pen, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		using var offset = BeginStroke();
		Control.DrawRectangle(x, y, width, height, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawText(Font font, Brush brush, float x, float y, string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		using var brushRef = CreateBrushReference(brush);
		using var layout = GetFontHandler(font).CreateTextLayout(Control, text, 0f, 0f);
		Control.DrawTextLayout(layout, x, y, brushRef.Brush);
	}

	public void DrawText(FormattedText formattedText, PointF location)
	{
		if (formattedText.Handler is not FormattedTextHandler handler || string.IsNullOrEmpty(handler.Text))
			return;

		using var brushRef = CreateBrushReference(handler.ForegroundBrush);
		using var layout = handler.CreateLayout(Control);
		Control.DrawTextLayout(layout, location.X, location.Y, brushRef.Brush);
	}

	static FontHandler GetFontHandler(Font font) => font?.Handler as FontHandler ?? (FontHandler)SystemFonts.Default().Handler;

	public void FillEllipse(Brush brush, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(brush);
		Control.FillEllipse(x + width / 2f, y + height / 2f, width / 2f, height / 2f, brushRef.Brush);
	}

	public void FillPath(Brush brush, IGraphicsPath path)
	{
		var geometry = path.ControlObject as CanvasGeometry
			?? throw new NotSupportedException("WinUI GraphicsPath filling requires a Win2D CanvasGeometry-backed path handler.");

		using var brushRef = CreateBrushReference(brush);
		Control.FillGeometry(geometry, brushRef.Brush);
	}

	public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
	{
		if (Math.Abs(sweepAngle) >= 360f)
		{
			FillEllipse(brush, x, y, width, height);
			return;
		}

		var points = new List<Vector2> { new(x + width / 2f, y + height / 2f) };
		points.AddRange(GetArcPoints(x, y, width, height, startAngle, sweepAngle).Select(ToVector));
		if (points.Count < 3)
			return;

		using var geometry = CanvasGeometry.CreatePolygon(Control, points.ToArray());
		using var brushRef = CreateBrushReference(brush);
		Control.FillGeometry(geometry, brushRef.Brush);
	}

	public void FillRectangle(Brush brush, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(brush);
		Control.FillRectangle(x, y, width, height, brushRef.Brush);
	}

	public void Flush()
	{
		CommitRenderTarget();
	}

	public SizeF MeasureString(Font font, string text)
	{
		if (string.IsNullOrEmpty(text))
			return SizeF.Empty;

		using var layout = GetFontHandler(font).CreateTextLayout(Control, text, 0f, 0f);
		return layout.LayoutBoundsIncludingTrailingWhitespace.ToEtoF().Size;
	}

	public void MultiplyTransform(IMatrix matrix)
	{
		if (matrix?.ControlObject is Matrix3x2 winUIMatrix)
			Control.Transform = winUIMatrix * Control.Transform;
		else
			throw new NotSupportedException("WinUI GraphicsHandler requires Matrix3x2-backed matrices.");
	}

	public void ResetClip()
	{
		_clipLayer?.Dispose();
		_clipLayer = null;
		_clipResource?.Dispose();
		_clipResource = null;
		_clipBounds = _initialClipBounds;
	}

	public void RestoreTransform()
	{
		if (_savedTransforms.Count > 0)
			Control.Transform = _savedTransforms.Pop();
	}

	public void RotateTransform(float angle)
	{
		Control.Transform = Matrix3x2.CreateRotation(DegreesToRadians(angle)) * Control.Transform;
	}

	public void SaveTransform()
	{
		_savedTransforms.Push(Control.Transform);
	}

	public void ScaleTransform(float scaleX, float scaleY)
	{
		Control.Transform = Matrix3x2.CreateScale(scaleX, scaleY) * Control.Transform;
	}

	public void SetClip(RectangleF rectangle)
	{
		rectangle.Normalize();
		var transformed = TransformRectangle(rectangle, Control.Transform);
		var geometry = CanvasGeometry.CreatePolygon(Control, GetRectangleCorners(rectangle).Select(TransformPoint).ToArray());
		ApplyClip(transformed, geometry);
	}

	public void SetClip(IGraphicsPath path)
	{
		var geometry = path.ControlObject as CanvasGeometry
			?? throw new NotSupportedException("WinUI GraphicsPath clipping requires a Win2D CanvasGeometry-backed path handler.");

		var transformedGeometry = geometry.Transform(Control.Transform);
		var bounds = transformedGeometry.ComputeBounds();
		ApplyClip(bounds.ToEtoF(), transformedGeometry);
	}

	public void TranslateTransform(float offsetX, float offsetY)
	{
		Control.Transform = Matrix3x2.CreateTranslation(offsetX, offsetY) * Control.Transform;
	}

	void ApplyClip(RectangleF clipBounds, CanvasGeometry geometry)
	{
		ResetClip();
		_clipResource = geometry;
		_clipLayer = CreateDeviceLayer(geometry);
		_clipBounds = clipBounds;
	}

	// the clip geometry is already in device space, but layers apply the current transform to it
	IDisposable CreateDeviceLayer(CanvasGeometry geometry)
	{
		var transform = Control.Transform;
		Control.Transform = Matrix3x2.Identity;
		var layer = Control.CreateLayer(1f, geometry);
		Control.Transform = transform;
		return layer;
	}

	void CommitRenderTarget()
	{
		if (_image?.Handler is not BitmapHandler bitmapHandler || _renderTarget == null || Control == null)
			return;

		var transform = Control.Transform;
		var antialiasing = Control.Antialiasing;
		var textAntialiasing = Control.TextAntialiasing;
		var clipGeometry = _clipResource as CanvasGeometry;
		var clipBounds = _clipBounds;
		_clipLayer?.Dispose();
		_clipLayer = null;
		// pixels can only be read once the drawing session has ended
		Control.Dispose();

		bitmapHandler.SetFromPremultiplied(_renderTarget.GetPixelBytes());

		InitializeDrawingSession(_renderTarget.CreateDrawingSession());
		Control.Transform = transform;
		Control.Antialiasing = antialiasing;
		Control.TextAntialiasing = textAntialiasing;
		_clipBounds = clipBounds;
		if (clipGeometry != null)
		{
			_clipResource = clipGeometry;
			_clipLayer = CreateDeviceLayer(clipGeometry);
		}
	}

	void InitializeDrawingSession(CanvasDrawingSession drawingSession)
	{
		Control = drawingSession;
		Control.Units = CanvasUnits.Dips;
		Control.Antialiasing = CanvasAntialiasing.Antialiased;
		Control.TextAntialiasing = CanvasTextAntialiasing.Auto;
	}

	BrushReference CreateBrushReference(Brush brush)
	{
		if (brush.ControlObject is ICanvasBrush canvasBrush)
			return new BrushReference(canvasBrush, null);

		if (brush.ControlObject is BrushData brushData)
		{
			var createdBrush = brushData.Create(Control);
			return new BrushReference(createdBrush, createdBrush);
		}

		throw new NotSupportedException($"Brush type '{brush.GetType().Name}' is not supported by WinUI graphics.");
	}

	CanvasStrokeStyle? CreateStrokeStyle(Pen pen)
	{
		if ((pen.DashStyle == null || pen.DashStyle.IsSolid)
			&& pen.LineCap == PenLineCap.Butt
			&& pen.LineJoin == PenLineJoin.Miter
			&& Math.Abs(pen.MiterLimit - 10f) < 0.001f)
			return null;

		var strokeStyle = new CanvasStrokeStyle
		{
			StartCap = pen.LineCap.ToWinUICapStyle(),
			EndCap = pen.LineCap.ToWinUICapStyle(),
			DashCap = pen.LineCap.ToWinUICapStyle(),
			LineJoin = pen.LineJoin.ToWinUILineJoin(),
			MiterLimit = pen.MiterLimit,
		};

		if (pen.DashStyle != null && !pen.DashStyle.IsSolid)
		{
			strokeStyle.DashOffset = pen.DashStyle.Offset;
			strokeStyle.CustomDashStyle = pen.DashStyle.Dashes;
		}

		return strokeStyle;
	}

	void DrawImageCore(Image image, RectangleF? source, RectangleF destination)
	{
		if (image == null || destination.Width == 0 || destination.Height == 0)
			return;

		if (image.Handler is not IWinUIImage winUIImage)
			throw new NotSupportedException($"Image type '{image.GetType().Name}' is not supported by WinUI graphics.");

		// pick the icon frame that best fits the destination at this session's DPI
		var bitmap = winUIImage.GetCanvasBitmap(Control, Control.Dpi / 96f, destination.Size);
		if (bitmap == null)
			return;

		var pixelSize = bitmap.SizeInPixels;
		var sourceRect = new RectangleF(0, 0, pixelSize.Width, pixelSize.Height);
		if (source != null)
		{
			// the source is in the image's logical units, which may differ from the frame's pixels
			var imageSize = image.Size;
			var scaleX = imageSize.Width > 0 ? pixelSize.Width / (float)imageSize.Width : 1f;
			var scaleY = imageSize.Height > 0 ? pixelSize.Height / (float)imageSize.Height : 1f;
			var s = source.Value;
			sourceRect = new RectangleF(s.X * scaleX, s.Y * scaleY, s.Width * scaleX, s.Height * scaleY);
		}
		Control.DrawImage(bitmap, destination.ToWinUI(), sourceRect.ToWinUI(), 1f, ImageInterpolation.ToCanvasInterpolation());
	}

	static IEnumerable<PointF> GetArcPoints(float x, float y, float width, float height, float startAngle, float sweepAngle)
	{
		var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweepAngle) / 6f));
		var centerX = x + width / 2f;
		var centerY = y + height / 2f;
		var radiusX = width / 2f;
		var radiusY = height / 2f;

		for (var i = 0; i <= segments; i++)
		{
			var angle = startAngle + sweepAngle * i / segments;
			var radians = DegreesToRadians(angle);
			yield return new PointF(
				centerX + radiusX * MathF.Cos(radians),
				centerY + radiusY * MathF.Sin(radians));
		}
	}

	static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

	static Vector2 ToVector(PointF point) => new(point.X, point.Y);

	Vector2 TransformPoint(PointF point) => Vector2.Transform(ToVector(point), Control.Transform);

	static RectangleF TransformRectangle(RectangleF rectangle, Matrix3x2 matrix)
	{
		var points = GetRectangleCorners(rectangle).Select(p => Vector2.Transform(ToVector(p), matrix)).ToArray();
		var left = points.Min(r => r.X);
		var top = points.Min(r => r.Y);
		var right = points.Max(r => r.X);
		var bottom = points.Max(r => r.Y);
		return RectangleF.FromSides(left, top, right, bottom);
	}

	static PointF[] GetRectangleCorners(RectangleF rectangle) =>
	[
		rectangle.TopLeft,
		rectangle.TopRight,
		rectangle.BottomRight,
		rectangle.BottomLeft
	];

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			CommitRenderTarget();
			ResetClip();
			_renderTarget?.Dispose();
			_renderTarget = null;
		}

		base.Dispose(disposing);
	}

	readonly record struct BrushReference(ICanvasBrush Brush, IDisposable? Disposable) : IDisposable
	{
		public void Dispose() => Disposable?.Dispose();
	}
}

static class WinUIGraphicsExtensions
{
	public static CanvasCapStyle ToWinUICapStyle(this PenLineCap lineCap) => lineCap switch
	{
		PenLineCap.Butt => CanvasCapStyle.Flat,
		PenLineCap.Round => CanvasCapStyle.Round,
		PenLineCap.Square => CanvasCapStyle.Square,
		_ => CanvasCapStyle.Flat
	};

	public static CanvasLineJoin ToWinUILineJoin(this PenLineJoin lineJoin) => lineJoin switch
	{
		PenLineJoin.Bevel => CanvasLineJoin.Bevel,
		PenLineJoin.Round => CanvasLineJoin.Round,
		_ => CanvasLineJoin.Miter
	};
}
