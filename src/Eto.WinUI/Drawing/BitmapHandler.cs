using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace Eto.WinUI.Drawing;

public class BitmapHandler : WidgetHandler<BitmapImage, Bitmap>, Bitmap.IHandler
{
	public BitmapHandler()
	{
	}

	public BitmapHandler(BitmapImage image)
	{
		Control = image;
	}

	public void Create(string fileName)
	{
		var uri = new System.Uri("file:///" + fileName.Replace("\\", "/"));
		Control = new BitmapImage(uri);
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

	public void Create(int width, int height, PixelFormat pixelFormat)
	{
		// WinUI BitmapImage does not support direct pixel creation.
		// For advanced scenarios, use WriteableBitmap.
		Control = new BitmapImage();
	}

	public void Create(int width, int height, Graphics graphics)
	{
		Control = new BitmapImage();
	}

	public void Create(Image image, int width, int height, ImageInterpolation interpolation)
	{
		// Not implemented: requires image resizing logic.
		Control = new BitmapImage();
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

	public BitmapData Lock()
	{
		// Not implemented: WinUI BitmapImage does not support direct pixel access.
		return null;
	}

	public void Unlock(BitmapData bitmapData)
	{
		// Not implemented.
	}

	public void Save(string fileName, ImageFormat format)
	{
		// Not implemented: WinUI does not provide direct save support.
		throw new NotSupportedException();
	}

	public void Save(Stream stream, ImageFormat format)
	{
		// Not implemented: WinUI does not provide direct save support.
		throw new NotSupportedException();
	}

	public Color GetPixel(int x, int y)
	{
		// Not implemented: WinUI BitmapImage does not support direct pixel access.
		throw new NotSupportedException();
	}

	public Bitmap Clone(Rectangle? rectangle = null)
	{
		// Not implemented: requires pixel access.
		throw new NotSupportedException();
	}

	//public void DrawImage(GraphicsHandler graphics, RectangleF source, RectangleF destination)
	//{
	//    // Not implemented: requires integration with WinUI drawing APIs.
	//    throw new NotSupportedException();
	//}
}