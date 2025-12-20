using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;

namespace Eto.WinUI.Drawing;

public class GraphicsHandler : WidgetHandler<CanvasDrawingSession, Graphics>, Graphics.IHandler
{
	static readonly RectangleF DefaultClipBounds = new(-1000000f, -1000000f, 2000000f, 2000000f);

	readonly Stack<Matrix3x2> _savedTransforms = new();
	IDisposable? _clipLayer;
	IDisposable? _clipResource;
	RectangleF _clipBounds = DefaultClipBounds;
	ImageInterpolation _imageInterpolation = ImageInterpolation.Medium;
	PixelOffsetMode _pixelOffsetMode = PixelOffsetMode.Half;

	public GraphicsHandler(CanvasDrawingSession drawingSession)
	{
		Control = drawingSession;
		Control.Units = CanvasUnits.Dips;
		Control.Antialiasing = CanvasAntialiasing.Antialiased;
		Control.TextAntialiasing = CanvasTextAntialiasing.Auto;
	}

	protected override bool DisposeControl => false;

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

	public bool IsRetained => false;

	public IMatrix CurrentTransform => new MatrixHandler(Control.Transform);

	public RectangleF ClipBounds => _clipBounds;

	public void Clear(SolidBrush brush)
	{
		ResetClip();
		Control.Clear(brush?.Color.ToWinUI() ?? Colors.Transparent.ToWinUI());
	}

	public void CreateFromImage(Bitmap image)
	{
		throw new NotSupportedException("Drawing to a WinUI bitmap is not implemented yet.");
	}

	public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
	{
		if (Math.Abs(sweepAngle) >= 360f)
		{
			DrawEllipse(pen, x, y, width, height);
			return;
		}

		var points = GetArcPoints(x, y, width, height, startAngle, sweepAngle).ToArray();
		if (points.Length < 2)
			return;

		for (var i = 1; i < points.Length; i++)
			DrawLine(pen, points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y);
	}

	public void DrawEllipse(Pen pen, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
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
		throw new NotSupportedException("WinUI image drawing requires a Win2D-backed image handler, which is not implemented yet.");
	}

	public void DrawImage(Image image, float x, float y, float width, float height)
	{
		throw new NotSupportedException("WinUI image drawing requires a Win2D-backed image handler, which is not implemented yet.");
	}

	public void DrawImage(Image image, RectangleF source, RectangleF destination)
	{
		throw new NotSupportedException("WinUI image drawing requires a Win2D-backed image handler, which is not implemented yet.");
	}

	public void DrawLine(Pen pen, float startx, float starty, float endx, float endy)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		Control.DrawLine(startx, starty, endx, endy, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawLines(Pen pen, IEnumerable<PointF> points)
	{
		var array = points as PointF[] ?? points.ToArray();
		if (array.Length < 2)
			return;

		for (var i = 1; i < array.Length; i++)
			DrawLine(pen, array[i - 1].X, array[i - 1].Y, array[i].X, array[i].Y);
	}

	public void DrawPath(Pen pen, IGraphicsPath path)
	{
		var geometry = path.ControlObject as CanvasGeometry
			?? throw new NotSupportedException("WinUI GraphicsPath drawing requires a Win2D CanvasGeometry-backed path handler.");

		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		Control.DrawGeometry(geometry, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawPolygon(Pen pen, IEnumerable<PointF> points)
	{
		var array = points as PointF[] ?? points.ToArray();
		if (array.Length < 2)
			return;

		DrawLines(pen, array);
		DrawLine(pen, array[^1].X, array[^1].Y, array[0].X, array[0].Y);
	}

	public void DrawRectangle(Pen pen, float x, float y, float width, float height)
	{
		using var brushRef = CreateBrushReference(pen.Brush);
		using var strokeStyle = CreateStrokeStyle(pen);
		Control.DrawRectangle(x, y, width, height, brushRef.Brush, pen.Thickness, strokeStyle);
	}

	public void DrawText(Font font, Brush brush, float x, float y, string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		using var brushRef = CreateBrushReference(brush);
		using var format = CreateTextFormat(font);
		Control.DrawText(text, x, y, brushRef.Brush, format);
	}

	public void DrawText(FormattedText formattedText, PointF location)
	{
		throw new NotSupportedException("WinUI formatted text drawing is not implemented yet.");
	}

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
	}

	public SizeF MeasureString(Font font, string text)
	{
		if (string.IsNullOrEmpty(text))
			return SizeF.Empty;

		using var format = CreateTextFormat(font);
		using var layout = new CanvasTextLayout(Control, text, format, 0f, 0f);
		return layout.LayoutBounds.ToEtoF().Size;
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
		_clipBounds = DefaultClipBounds;
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
		_clipLayer = Control.CreateLayer(1f, geometry);
		_clipBounds = clipBounds;
	}

	BrushReference CreateBrushReference(Brush brush)
	{
		if (brush.ControlObject is ICanvasBrush canvasBrush)
			return new BrushReference(canvasBrush, null);

		if (brush is SolidBrush solidBrush)
		{
			var createdBrush = new CanvasSolidColorBrush(Control, solidBrush.Color.ToWinUI());
			return new BrushReference(createdBrush, createdBrush);
		}

		throw new NotSupportedException($"Brush type '{brush.GetType().Name}' is not implemented for WinUI graphics yet.");
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

	static CanvasTextFormat CreateTextFormat(Font font)
	{
		return new CanvasTextFormat
		{
			FontFamily = font.FamilyName,
			FontSize = font.Size,
			FontWeight = font.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
			FontStyle = font.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
		};
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


