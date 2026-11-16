using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Thread-agnostic brush description that creates a Win2D brush for each drawing session.
/// </summary>
abstract class BrushData
{
	/// <summary>Creates a new Win2D brush that the caller must dispose.</summary>
	public abstract ICanvasBrush Create(ICanvasResourceCreator creator);
}

sealed class SolidBrushData : BrushData
{
	public Color Color { get; set; }

	public override ICanvasBrush Create(ICanvasResourceCreator creator) => new CanvasSolidColorBrush(creator, Color.ToWinUI());
}

abstract class TransformBrushData : BrushData
{
	public IMatrix? Transform { get; set; }

	protected Matrix3x2 NativeTransform => Transform?.ControlObject is Matrix3x2 matrix ? matrix : Matrix3x2.Identity;
}

abstract class GradientBrushData : TransformBrushData
{
	public Color StartColor { get; set; }

	public Color EndColor { get; set; }

	public GradientWrapMode WrapMode { get; set; }

	protected CanvasGradientStop[] Stops =>
	[
		new CanvasGradientStop { Position = 0f, Color = StartColor.ToWinUI() },
		new CanvasGradientStop { Position = 1f, Color = EndColor.ToWinUI() }
	];

	protected CanvasEdgeBehavior EdgeBehavior => WrapMode switch
	{
		GradientWrapMode.Repeat => CanvasEdgeBehavior.Wrap,
		GradientWrapMode.Reflect => CanvasEdgeBehavior.Mirror,
		_ => CanvasEdgeBehavior.Clamp
	};
}

sealed class LinearGradientBrushData : GradientBrushData
{
	public PointF StartPoint { get; set; }

	public PointF EndPoint { get; set; }

	public override ICanvasBrush Create(ICanvasResourceCreator creator)
	{
		return new CanvasLinearGradientBrush(creator, Stops, EdgeBehavior, CanvasAlphaMode.Premultiplied)
		{
			StartPoint = new Vector2(StartPoint.X, StartPoint.Y),
			EndPoint = new Vector2(EndPoint.X, EndPoint.Y),
			Transform = NativeTransform
		};
	}
}

sealed class RadialGradientBrushData : GradientBrushData
{
	public PointF Center { get; set; }

	public PointF GradientOrigin { get; set; }

	public SizeF Radius { get; set; }

	public override ICanvasBrush Create(ICanvasResourceCreator creator)
	{
		return new CanvasRadialGradientBrush(creator, Stops, EdgeBehavior, CanvasAlphaMode.Premultiplied)
		{
			Center = new Vector2(Center.X, Center.Y),
			OriginOffset = new Vector2(GradientOrigin.X - Center.X, GradientOrigin.Y - Center.Y),
			RadiusX = Radius.Width,
			RadiusY = Radius.Height,
			Transform = NativeTransform
		};
	}
}

sealed class TextureBrushData : TransformBrushData
{
	public required Image Image { get; init; }

	public float Opacity { get; set; } = 1f;

	public override ICanvasBrush Create(ICanvasResourceCreator creator)
	{
		var bitmap = (Image.Handler as IWinUIImage)?.GetCanvasBitmap(creator, 1f, Image.Size)
			?? throw new NotSupportedException("Image type is not supported for a TextureBrush");
		var transform = NativeTransform;
		// icons may pick a frame whose pixel size differs from the image's logical size
		var size = Image.Size;
		if (size.Width > 0 && size.Height > 0 && (bitmap.SizeInPixels.Width != size.Width || bitmap.SizeInPixels.Height != size.Height))
			transform = Matrix3x2.CreateScale((float)size.Width / bitmap.SizeInPixels.Width, (float)size.Height / bitmap.SizeInPixels.Height) * transform;
		return new CanvasImageBrush(creator, bitmap)
		{
			ExtendX = CanvasEdgeBehavior.Wrap,
			ExtendY = CanvasEdgeBehavior.Wrap,
			Opacity = Opacity,
			Transform = transform
		};
	}
}
