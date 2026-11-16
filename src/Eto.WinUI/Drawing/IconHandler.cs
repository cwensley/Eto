using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eto.WinUI.Drawing;

public class IconHandler : WidgetHandler<Icon>, Icon.IHandler, IWinUIImage
{
	List<IconFrame>? _frames;
	Size? _size;

	public IconHandler()
	{
	}

	/// <summary>Creates an icon with a single frame from the specified bitmap.</summary>
	public IconHandler(Bitmap bitmap)
	{
		_frames = [IconFrame.FromControlObject(1f, bitmap)];
	}

	public void Create(Stream stream)
	{
		// each frame of an .ico becomes its own bitmap at scale 1, like other platforms
		_frames = WinUIImageHelper.Decode(stream, true)
			.Select(r => IconFrame.FromControlObject(1f, new Bitmap(new BitmapHandler(r.Width, r.Height, r.Pixels))))
			.ToList();
	}

	public void Create(string fileName)
	{
		using var stream = File.OpenRead(fileName);
		Create(stream);
	}

	public void Create(IEnumerable<IconFrame> frames)
	{
		_frames = frames.ToList();
	}

	public IEnumerable<IconFrame> Frames => _frames ??= [];

	public Size Size
	{
		get
		{
			if (_size != null)
				return _size.Value;
			var largest = Frames.OrderByDescending(r => r.Size.Width * r.Size.Height).FirstOrDefault();
			_size = largest?.Size ?? Size.Empty;
			return _size.Value;
		}
	}

	IWinUIImage? GetFrameImage(float scale, SizeF? fittingSize)
	{
		if (Frames.FirstOrDefault() == null)
			return null;
		var frame = Widget.GetFrame(scale, fittingSize != null ? Size.Ceiling(fittingSize.Value) : null);
		return frame?.Bitmap?.Handler as IWinUIImage;
	}

	public BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize) => GetFrameImage(scale, fittingSize)?.GetImageClosestToSize(scale, fittingSize);

	public CanvasBitmap? GetCanvasBitmap(ICanvasResourceCreator creator, float scale, SizeF? fittingSize) => GetFrameImage(scale, fittingSize)?.GetCanvasBitmap(creator, scale, fittingSize);
}
