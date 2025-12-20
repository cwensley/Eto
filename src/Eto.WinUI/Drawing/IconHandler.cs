using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Eto.WinUI.Drawing;

public class IconHandler : WidgetHandler<BitmapImage, Icon>, Icon.IHandler
{
	public IconHandler()
	{
	}

	public IconHandler(BitmapImage image)
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

	public void Create(IEnumerable<IconFrame> frames)
	{
		// Not implemented: WinUI does not support multi-frame icons.
		throw new NotSupportedException();
	}

	public IEnumerable<IconFrame> Frames
	{
		get
		{
			// Not implemented: WinUI does not support multi-frame icons.
			yield break;
		}
	}
}