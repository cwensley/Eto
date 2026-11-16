namespace Eto.WinUI.Drawing;

public class LinearGradientBrushHandler : LinearGradientBrush.IHandler
{
	static LinearGradientBrushData Get(LinearGradientBrush widget) => (LinearGradientBrushData)widget.ControlObject;

	public object Create(Color startColor, Color endColor, PointF startPoint, PointF endPoint)
	{
		return new LinearGradientBrushData
		{
			StartColor = startColor,
			EndColor = endColor,
			StartPoint = startPoint,
			EndPoint = endPoint
		};
	}

	public object Create(RectangleF rectangle, Color startColor, Color endColor, float angle)
	{
		GradientHelper.GetLinearFromRectangle(rectangle, angle, out var startPoint, out var endPoint);
		return Create(startColor, endColor, startPoint, endPoint);
	}

	public IMatrix GetTransform(LinearGradientBrush widget) => Get(widget).Transform!;

	public void SetTransform(LinearGradientBrush widget, IMatrix transform) => Get(widget).Transform = transform;

	public GradientWrapMode GetGradientWrap(LinearGradientBrush widget) => Get(widget).WrapMode;

	public void SetGradientWrap(LinearGradientBrush widget, GradientWrapMode gradientWrap) => Get(widget).WrapMode = gradientWrap;
}

public class RadialGradientBrushHandler : RadialGradientBrush.IHandler
{
	static RadialGradientBrushData Get(RadialGradientBrush widget) => (RadialGradientBrushData)widget.ControlObject;

	public object Create(Color startColor, Color endColor, PointF center, PointF gradientOrigin, SizeF radius)
	{
		return new RadialGradientBrushData
		{
			StartColor = startColor,
			EndColor = endColor,
			Center = center,
			GradientOrigin = gradientOrigin,
			Radius = radius
		};
	}

	public IMatrix GetTransform(RadialGradientBrush widget) => Get(widget).Transform!;

	public void SetTransform(RadialGradientBrush widget, IMatrix transform) => Get(widget).Transform = transform;

	public GradientWrapMode GetGradientWrap(RadialGradientBrush widget) => Get(widget).WrapMode;

	public void SetGradientWrap(RadialGradientBrush widget, GradientWrapMode gradientWrap) => Get(widget).WrapMode = gradientWrap;
}

public class TextureBrushHandler : TextureBrush.IHandler
{
	static TextureBrushData Get(TextureBrush widget) => (TextureBrushData)widget.ControlObject;

	public object Create(Image image, float opacity) => new TextureBrushData { Image = image, Opacity = opacity };

	public IMatrix GetTransform(TextureBrush widget) => Get(widget).Transform!;

	public void SetTransform(TextureBrush widget, IMatrix transform) => Get(widget).Transform = transform;

	public void SetOpacity(TextureBrush widget, float opacity) => Get(widget).Opacity = opacity;
}
