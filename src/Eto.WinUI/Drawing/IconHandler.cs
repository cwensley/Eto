using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Eto.WinUI.Drawing;

public class IconHandler : WidgetHandler<BitmapSource, Icon>, Icon.IHandler
{
	public IconHandler()
	{
	}

	public IconHandler(BitmapSource image)
	{
		Control = image;
	}

	public void Create(Stream stream)
	{
		var bitmap = new BitmapImage();
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		ms.Position = 0;
		bitmap.SetSource(ms.AsRandomAccessStream());
		Control = bitmap;
	}

	public void Create(string fileName)
	{
		var uri = new System.Uri("file:///" + fileName.Replace("\\", "/"));
		Control = new BitmapImage(uri);
	}

	public Size Size
	{
		get
		{
			if (Control == null)
				return Size.Empty;
			return new Size((int)Control.PixelWidth, (int)Control.PixelHeight);
		}
	}
	
	List<IconFrame>? frames;

	public void Create(IEnumerable<IconFrame> frames)
	{
		this.frames = frames.ToList();
		if (this.frames.Count > 0)
			Control = Widget.GetFrame(1).Bitmap.ToBitmapSource();
		
	}

	public IEnumerable<IconFrame> Frames => frames ??= CreateFramesFromControl().ToList();
	
	private IEnumerable<IconFrame> CreateFramesFromControl()
	{
		if (Control == null)
			yield break;
		yield return new IconFrame(1, new Bitmap(new BitmapHandler(Control)));
	}
}