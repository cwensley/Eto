using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;

namespace Eto.WinUI.Drawing;

public class GraphicsPathHandler : GraphicsPath.IHandler
{
	readonly List<IPathElement> _elements = [];
	FigureElement? _currentFigure;
	CanvasGeometry? _geometry;
	Matrix3x2 _transform = Matrix3x2.Identity;
	FillMode _fillMode = FillMode.Winding;
	PointF _currentPoint;

	public bool IsEmpty => _elements.Count == 0;

	public PointF CurrentPoint => _currentPoint;

	public RectangleF Bounds => GetGeometry().ComputeBounds().ToEtoF();

	public FillMode FillMode
	{
		get => _fillMode;
		set
		{
			if (_fillMode == value)
				return;
			_fillMode = value;
			InvalidateGeometry();
		}
	}

	public object ControlObject => GetGeometry();

	public void AddLine(float startX, float startY, float endX, float endY)
	{
		ConnectTo(new PointF(startX, startY));
		_currentFigure!.Segments.Add(new LineSegmentElement(new PointF(endX, endY)));
		_currentPoint = new PointF(endX, endY);
		InvalidateGeometry();
	}

	public void AddLines(IEnumerable<PointF> points)
	{
		var pointList = points as IList<PointF> ?? points.ToArray();
		if (pointList.Count == 0)
			return;

		ConnectTo(pointList[0]);
		for (var i = 1; i < pointList.Count; i++)
			_currentFigure!.Segments.Add(new LineSegmentElement(pointList[i]));
		_currentPoint = pointList[^1];
		InvalidateGeometry();
	}

	public void LineTo(float x, float y)
	{
		ConnectTo(new PointF(x, y));
		_currentPoint = new PointF(x, y);
		InvalidateGeometry();
	}

	public void MoveTo(float x, float y)
	{
		StartNewFigure(new PointF(x, y));
		_currentPoint = new PointF(x, y);
		InvalidateGeometry();
	}

	public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
	{
		var startRadians = DegreesToRadians(startAngle);
		var sweepRadians = DegreesToRadians(sweepAngle);
		var radiusX = width / 2f;
		var radiusY = height / 2f;
		var centerX = x + radiusX;
		var centerY = y + radiusY;

		var startPoint = new PointF(
			centerX + MathF.Cos(startRadians) * radiusX,
			centerY + MathF.Sin(startRadians) * radiusY);
		var endPoint = new PointF(
			centerX + MathF.Cos(startRadians + sweepRadians) * radiusX,
			centerY + MathF.Sin(startRadians + sweepRadians) * radiusY);

		ConnectTo(startPoint);
		_currentFigure!.Segments.Add(new ArcSegmentElement(
			endPoint,
			radiusX,
			radiusY,
			0f,
			sweepAngle < 0 ? CanvasSweepDirection.CounterClockwise : CanvasSweepDirection.Clockwise,
			Math.Abs(sweepAngle) > 180f ? CanvasArcSize.Large : CanvasArcSize.Small));
		_currentPoint = endPoint;
		InvalidateGeometry();
	}

	public void AddBezier(PointF start, PointF control1, PointF control2, PointF end)
	{
		ConnectTo(start);
		_currentFigure!.Segments.Add(new BezierSegmentElement(control1, control2, end));
		_currentPoint = end;
		InvalidateGeometry();
	}

	public void AddCurve(IEnumerable<PointF> points, float tension = 0.5f)
	{
		var splinePoints = SplineHelper.SplineCurve(points, tension).ToArray();
		if (splinePoints.Length == 0)
			return;

		SplineHelper.Draw(
			splinePoints,
			ConnectTo,
			(control1, control2, end) =>
			{
				_currentFigure!.Segments.Add(new BezierSegmentElement(control1, control2, end));
				_currentPoint = end;
			});
		InvalidateGeometry();
	}

	public void AddEllipse(float x, float y, float width, float height)
	{
		var radiusX = width / 2f;
		var radiusY = height / 2f;
		var centerX = x + radiusX;
		var centerY = y + radiusY;
		var start = new PointF(centerX + radiusX, centerY);
		var middle = new PointF(centerX - radiusX, centerY);

		var figure = new FigureElement(start)
		{
			IsClosed = true
		};
		figure.Segments.Add(new ArcSegmentElement(middle, radiusX, radiusY, 0f, CanvasSweepDirection.Clockwise, CanvasArcSize.Small));
		figure.Segments.Add(new ArcSegmentElement(start, radiusX, radiusY, 0f, CanvasSweepDirection.Clockwise, CanvasArcSize.Small));
		AddStandaloneFigure(figure);
		InvalidateGeometry();
	}

	public void AddRectangle(float x, float y, float width, float height)
	{
		var figure = new FigureElement(new PointF(x, y))
		{
			IsClosed = true
		};
		figure.Segments.Add(new LineSegmentElement(new PointF(x + width, y)));
		figure.Segments.Add(new LineSegmentElement(new PointF(x + width, y + height)));
		figure.Segments.Add(new LineSegmentElement(new PointF(x, y + height)));
		AddStandaloneFigure(figure);
		InvalidateGeometry();
	}

	public void AddPath(IGraphicsPath path, bool connect = false)
	{
		if (path == null || path.IsEmpty)
			return;

		var handler = GetHandler(path);
		if (handler != null && handler._transform.IsIdentity)
		{
			var incomingFigures = handler.CloneElements();
			if (connect && _currentFigure != null && !_currentFigure.IsClosed && incomingFigures.Count > 0 && incomingFigures[0] is FigureElement firstFigure && !firstFigure.IsClosed)
			{
				_currentFigure.Segments.Add(new LineSegmentElement(firstFigure.StartPoint));
				foreach (var segment in firstFigure.Segments)
					_currentFigure.Segments.Add(segment.Clone());
				_currentPoint = firstFigure.GetEndPoint();
				incomingFigures.RemoveAt(0);
			}

			foreach (var element in incomingFigures)
				_elements.Add(element);

			_currentFigure = _elements.LastOrDefault() as FigureElement;
			if (_currentFigure?.IsClosed == true)
				_currentFigure = null;

			InvalidateGeometry();
			return;
		}

		if (path.ControlObject is CanvasGeometry geometry)
		{
			_elements.Add(new GeometryElement(geometry.Transform(Matrix3x2.Identity)));
			_currentFigure = null;
			InvalidateGeometry();
		}
	}

	public void Transform(IMatrix matrix)
	{
		if (matrix?.ControlObject is not Matrix3x2 winUIMatrix)
			throw new NotSupportedException("WinUI GraphicsPathHandler requires Matrix3x2-backed matrices.");

		_transform = winUIMatrix * _transform;
		InvalidateGeometry();
	}

	public void StartFigure()
	{
		_currentFigure = null;
	}

	public void CloseFigure()
	{
		if (_currentFigure != null)
			_currentFigure.IsClosed = true;
		_currentFigure = null;
		InvalidateGeometry();
	}

	public IGraphicsPath Clone()
	{
		var clone = new GraphicsPathHandler
		{
			_transform = _transform,
			_fillMode = _fillMode,
			_currentPoint = _currentPoint
		};
		foreach (var element in CloneElements())
			clone._elements.Add(element);
		clone._currentFigure = clone._elements.LastOrDefault() as FigureElement;
		if (clone._currentFigure?.IsClosed == true)
			clone._currentFigure = null;
		return clone;
	}

	public bool FillContains(PointF point) => GetGeometry().FillContainsPoint(ToVector(point));

	public bool StrokeContains(Pen pen, PointF point)
	{
		using var strokeStyle = CreateStrokeStyle(pen);
		return strokeStyle != null
			? GetGeometry().StrokeContainsPoint(ToVector(point), pen.Thickness, strokeStyle)
			: GetGeometry().StrokeContainsPoint(ToVector(point), pen.Thickness);
	}

	public void Dispose()
	{
		InvalidateGeometry();
		foreach (var element in _elements.OfType<GeometryElement>())
			element.Dispose();
		_elements.Clear();
		_currentFigure = null;
	}

	void ConnectTo(PointF startPoint)
	{
		if (_currentFigure == null)
		{
			StartNewFigure(startPoint);
			return;
		}

		_currentFigure.Segments.Add(new LineSegmentElement(startPoint));
	}

	void StartNewFigure(PointF startPoint)
	{
		var figure = new FigureElement(startPoint);
		_elements.Add(figure);
		_currentFigure = figure;
	}

	void AddStandaloneFigure(FigureElement figure)
	{
		_elements.Add(figure);
		_currentFigure = null;
	}

	List<IPathElement> CloneElements() => _elements.Select(static element => element.Clone()).ToList();

	CanvasGeometry GetGeometry()
	{
		if (_geometry != null)
			return _geometry;

		var device = CanvasDevice.GetSharedDevice();
		using var pathBuilder = new CanvasPathBuilder(device);
		pathBuilder.SetFilledRegionDetermination(_fillMode == FillMode.Alternate
			? CanvasFilledRegionDetermination.Alternate
			: CanvasFilledRegionDetermination.Winding);

		foreach (var element in _elements)
			element.Append(pathBuilder);

		_geometry = CanvasGeometry.CreatePath(pathBuilder);
		if (!_transform.IsIdentity)
		{
			var transformed = _geometry.Transform(_transform);
			_geometry.Dispose();
			_geometry = transformed;
		}

		return _geometry;
	}

	void InvalidateGeometry()
	{
		_geometry?.Dispose();
		_geometry = null;
	}

	static GraphicsPathHandler? GetHandler(IGraphicsPath path)
	{
		if (path is GraphicsPathHandler handler)
			return handler;
		if (path is IHandlerSource handlerSource && handlerSource.Handler is GraphicsPathHandler graphicsPathHandler)
			return graphicsPathHandler;
		return null;
	}

	static CanvasStrokeStyle? CreateStrokeStyle(Pen pen)
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

	static Vector2 ToVector(PointF point) => new(point.X, point.Y);

	static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

	interface IPathElement
	{
		void Append(CanvasPathBuilder builder);
		IPathElement Clone();
	}

	sealed class FigureElement(PointF startPoint) : IPathElement
	{
		public PointF StartPoint { get; } = startPoint;

		public List<ISegmentElement> Segments { get; } = [];

		public bool IsClosed { get; set; }

		public void Append(CanvasPathBuilder builder)
		{
			builder.BeginFigure(ToVector(StartPoint));
			foreach (var segment in Segments)
				segment.Append(builder);
			builder.EndFigure(IsClosed ? CanvasFigureLoop.Closed : CanvasFigureLoop.Open);
		}

		public IPathElement Clone()
		{
			var clone = new FigureElement(StartPoint)
			{
				IsClosed = IsClosed
			};
			foreach (var segment in Segments)
				clone.Segments.Add(segment.Clone());
			return clone;
		}

		public PointF GetEndPoint() => Segments.Count > 0 ? Segments[^1].EndPoint : StartPoint;
	}

	sealed class GeometryElement(CanvasGeometry geometry) : IPathElement, IDisposable
	{
		public CanvasGeometry Geometry { get; private set; } = geometry;

		public void Append(CanvasPathBuilder builder) => builder.AddGeometry(Geometry);

		public IPathElement Clone() => new GeometryElement(Geometry.Transform(Matrix3x2.Identity));

		public void Dispose()
		{
			Geometry.Dispose();
		}
	}

	interface ISegmentElement
	{
		PointF EndPoint { get; }

		void Append(CanvasPathBuilder builder);

		ISegmentElement Clone();
	}

	sealed class LineSegmentElement(PointF endPoint) : ISegmentElement
	{
		public PointF EndPoint { get; } = endPoint;

		public void Append(CanvasPathBuilder builder) => builder.AddLine(ToVector(EndPoint));

		public ISegmentElement Clone() => new LineSegmentElement(EndPoint);
	}

	sealed class BezierSegmentElement(PointF control1, PointF control2, PointF endPoint) : ISegmentElement
	{
		public PointF Control1 { get; } = control1;

		public PointF Control2 { get; } = control2;

		public PointF EndPoint { get; } = endPoint;

		public void Append(CanvasPathBuilder builder) => builder.AddCubicBezier(ToVector(Control1), ToVector(Control2), ToVector(EndPoint));

		public ISegmentElement Clone() => new BezierSegmentElement(Control1, Control2, EndPoint);
	}

	sealed class ArcSegmentElement(
		PointF endPoint,
		float radiusX,
		float radiusY,
		float rotationAngle,
		CanvasSweepDirection sweepDirection,
		CanvasArcSize arcSize) : ISegmentElement
	{
		public PointF EndPoint { get; } = endPoint;

		public float RadiusX { get; } = radiusX;

		public float RadiusY { get; } = radiusY;

		public float RotationAngle { get; } = rotationAngle;

		public CanvasSweepDirection SweepDirection { get; } = sweepDirection;

		public CanvasArcSize ArcSize { get; } = arcSize;

		public void Append(CanvasPathBuilder builder) => builder.AddArc(ToVector(EndPoint), RadiusX, RadiusY, RotationAngle, SweepDirection, ArcSize);

		public ISegmentElement Clone() => new ArcSegmentElement(EndPoint, RadiusX, RadiusY, RotationAngle, SweepDirection, ArcSize);
	}

}
