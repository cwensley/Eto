using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace Eto.WinUI.Drawing;

internal interface IWinUIImage
{
	BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize);
}

public class BitmapHandler : WidgetHandler<BitmapSource, Bitmap>, Bitmap.IHandler, IWinUIImage
{
	BitmapSource? _source;

	public BitmapHandler()
	{
	}

	public BitmapHandler(BitmapSource image)
	{
		Control = image;
		_source = image;
	}

	public void Create(string fileName)
	{
		var uri = new System.Uri("file:///" + fileName.Replace("\\", "/"));
		Control = new BitmapImage(uri);
		_source = Control;
	}

	public void Create(Stream stream)
	{
		var bitmap = new BitmapImage();
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		ms.Position = 0;
		bitmap.SetSource(ms.AsRandomAccessStream());
		Control = bitmap;
		_source = bitmap;
	}

	public void Create(int width, int height, PixelFormat pixelFormat)
	{
		Control = new WriteableBitmap(width, height);
		_source = Control;
	}

	public void Create(int width, int height, Graphics graphics)
	{
		Control = new WriteableBitmap(width, height);
		_source = Control;
	}

	public void Create(Image image, int width, int height, ImageInterpolation interpolation)
	{
		// Not implemented: requires image resizing logic.
		Control = new BitmapImage();
		_source = new WriteableBitmap(width, height);
	}

	public Size Size
	{
		get
		{
			var source = _source ?? Control;
			if (source == null)
				return Size.Empty;
			return new Size(source.PixelWidth, source.PixelHeight);
		}
	}

	internal void SetBitmap(BitmapSource source)
	{
		_source = source;
		if (source is BitmapImage bitmapImage)
			Control = bitmapImage;
	}

	public BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize) => _source ?? Control;

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
