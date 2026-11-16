using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Eto.Shared.Drawing;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Bitmap data for WinUI bitmaps, stored as BGR(A) with straight alpha.
/// </summary>
public class BitmapDataHandler : BaseBitmapData
{
	readonly bool _ignoreAlpha;

	public BitmapDataHandler(Image image, IntPtr data, int scanWidth, int bitsPerPixel, object? controlObject, bool ignoreAlpha)
		: base(image, data, scanWidth, bitsPerPixel, controlObject!, false)
	{
		_ignoreAlpha = ignoreAlpha;
	}

	public override int TranslateArgbToData(int argb) => _ignoreAlpha ? argb | unchecked((int)0xFF000000) : argb;

	public override int TranslateDataToArgb(int bitmapData) => _ignoreAlpha ? bitmapData | unchecked((int)0xFF000000) : bitmapData;
}

/// <summary>
/// Bitmap handler that keeps its pixels in memory so it can be used from any thread.
/// </summary>
public class BitmapHandler : WidgetHandler<Bitmap>, Bitmap.IHandler, IWinUIImage
{
	// pixels are kept in the bitmap's own format (BGR, BGRX or straight BGRA), rows aligned to 4 bytes
	readonly object _sync = new();
	readonly WinUIImageCache _cache = new();
	byte[] _pixels = [];
	int _width;
	int _height;
	int _stride;
	int _bytesPerPixel = 4;
	PixelFormat _format = PixelFormat.Format32bppRgba;
	int _version;
	GCHandle _lockHandle;
	BitmapSource? _externalSource;
	CanvasBitmap? _externalCanvasBitmap;

	public BitmapHandler()
	{
	}

	/// <summary>Wraps a native WinUI image; pixel access is only available for a <see cref="WriteableBitmap"/>.</summary>
	public BitmapHandler(BitmapSource image)
	{
		if (image is WriteableBitmap writeable)
		{
			var pixels = writeable.PixelBuffer.ToArray();
			WinUIImageHelper.Unpremultiply(pixels);
			SetStraightBgra(writeable.PixelWidth, writeable.PixelHeight, pixels);
		}
		else
			_externalSource = image;
	}

	internal BitmapHandler(int width, int height, byte[] straightBgra)
	{
		SetStraightBgra(width, height, straightBgra);
	}

	public void Create(string fileName)
	{
		using var stream = File.OpenRead(fileName);
		Create(stream);
	}

	public void Create(Stream stream)
	{
		var frame = WinUIImageHelper.Decode(stream, false)[0];
		SetStraightBgra(frame.Width, frame.Height, frame.Pixels);
	}

	public void Create(int width, int height, PixelFormat pixelFormat)
	{
		Allocate(width, height, pixelFormat);
	}

	public void Create(int width, int height, Graphics graphics)
	{
		Allocate(width, height, PixelFormat.Format32bppRgba);
	}

	public void Create(Image image, int width, int height, ImageInterpolation interpolation)
	{
		if (image.Handler is not IWinUIImage source)
			throw new NotSupportedException("Image type is not supported");

		var device = CanvasDevice.GetSharedDevice();
		using var target = new CanvasRenderTarget(device, Math.Max(1, width), Math.Max(1, height), 96f);
		using (var session = target.CreateDrawingSession())
		{
			session.Clear(Microsoft.UI.Colors.Transparent);
			var bitmap = source.GetCanvasBitmap(device, 1f, new SizeF(width, height));
			if (bitmap != null)
				session.DrawImage(bitmap, new wf.Rect(0, 0, width, height), bitmap.Bounds, 1f, interpolation.ToCanvasInterpolation());
		}
		var pixels = target.GetPixelBytes();
		WinUIImageHelper.Unpremultiply(pixels);
		SetStraightBgra(width, height, pixels);
	}

	void Allocate(int width, int height, PixelFormat format)
	{
		_format = format;
		_bytesPerPixel = format == PixelFormat.Format24bppRgb ? 3 : 4;
		_width = width;
		_height = height;
		_stride = (width * _bytesPerPixel + 3) & ~3;
		_pixels = new byte[_stride * height];
		_externalSource = null;
		_version++;
	}

	void SetStraightBgra(int width, int height, byte[] pixels)
	{
		_format = PixelFormat.Format32bppRgba;
		_bytesPerPixel = 4;
		_width = width;
		_height = height;
		_stride = width * 4;
		_pixels = pixels;
		_externalSource = null;
		_version++;
	}

	public Size Size
	{
		get
		{
			if (_externalSource != null)
				return new Size(_externalSource.PixelWidth, _externalSource.PixelHeight);
			return new Size(_width, _height);
		}
	}

	void EnsurePixels()
	{
		if (_externalSource != null)
			throw new NotSupportedException("Pixel access is not supported for this native image");
	}

	/// <summary>Gets the pixels as BGRA with straight alpha, 4 bytes per pixel with no row padding.</summary>
	internal byte[] GetStraightBgra()
	{
		lock (_sync)
		{
			EnsurePixels();
			var result = new byte[_width * _height * 4];
			if (_format == PixelFormat.Format32bppRgba)
			{
				Buffer.BlockCopy(_pixels, 0, result, 0, Math.Min(result.Length, _pixels.Length));
				return result;
			}
			for (int y = 0; y < _height; y++)
			{
				var src = y * _stride;
				var dst = y * _width * 4;
				for (int x = 0; x < _width; x++)
				{
					result[dst++] = _pixels[src];
					result[dst++] = _pixels[src + 1];
					result[dst++] = _pixels[src + 2];
					result[dst++] = 255;
					src += _bytesPerPixel;
				}
			}
			return result;
		}
	}

	internal byte[] GetPremultipliedBgra() => WinUIImageHelper.Premultiply(GetStraightBgra());

	/// <summary>Replaces the pixels with rendered output, keeping this bitmap's format.</summary>
	internal void SetFromPremultiplied(byte[] premultiplied)
	{
		lock (_sync)
		{
			EnsurePixels();
			if (_format == PixelFormat.Format32bppRgba)
			{
				WinUIImageHelper.Unpremultiply(premultiplied);
				Buffer.BlockCopy(premultiplied, 0, _pixels, 0, Math.Min(premultiplied.Length, _pixels.Length));
			}
			else
			{
				// opaque formats: premultiplied values are the colour composited over black
				for (int y = 0; y < _height; y++)
				{
					var src = y * _width * 4;
					var dst = y * _stride;
					for (int x = 0; x < _width; x++)
					{
						_pixels[dst] = premultiplied[src];
						_pixels[dst + 1] = premultiplied[src + 1];
						_pixels[dst + 2] = premultiplied[src + 2];
						if (_bytesPerPixel == 4)
							_pixels[dst + 3] = 255;
						src += 4;
						dst += _bytesPerPixel;
					}
				}
			}
			_version++;
		}
	}

	public BitmapData Lock()
	{
		lock (_sync)
		{
			EnsurePixels();
			_lockHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
			return new BitmapDataHandler(Widget, _lockHandle.AddrOfPinnedObject(), _stride, _bytesPerPixel * 8, null, _format == PixelFormat.Format32bppRgb);
		}
	}

	public void Unlock(BitmapData bitmapData)
	{
		lock (_sync)
		{
			if (_lockHandle.IsAllocated)
				_lockHandle.Free();
			_version++;
		}
	}

	public void Save(string fileName, ImageFormat format)
	{
		using var stream = File.Create(fileName);
		Save(stream, format);
	}

	public void Save(Stream stream, ImageFormat format)
	{
		WinUIImageHelper.Encode(stream, format, _width, _height, GetStraightBgra());
	}

	public Color GetPixel(int x, int y)
	{
		lock (_sync)
		{
			EnsurePixels();
			if (x < 0 || y < 0 || x >= _width || y >= _height)
				throw new ArgumentOutOfRangeException(nameof(x), "Pixel position is outside the bounds of the bitmap");
			var pos = y * _stride + x * _bytesPerPixel;
			var a = _format == PixelFormat.Format32bppRgba ? _pixels[pos + 3] : 255;
			return Color.FromArgb(_pixels[pos + 2], _pixels[pos + 1], _pixels[pos], a);
		}
	}

	public Bitmap Clone(Rectangle? rectangle = null)
	{
		lock (_sync)
		{
			EnsurePixels();
			var rect = rectangle ?? new Rectangle(0, 0, _width, _height);
			var clone = new BitmapHandler();
			clone.Allocate(rect.Width, rect.Height, _format);
			var rowBytes = rect.Width * _bytesPerPixel;
			for (int y = 0; y < rect.Height; y++)
			{
				Buffer.BlockCopy(_pixels, (rect.Y + y) * _stride + rect.X * _bytesPerPixel, clone._pixels, y * clone._stride, rowBytes);
			}
			return new Bitmap(clone);
		}
	}

	public BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize)
	{
		if (_externalSource != null)
			return _externalSource;
		return _cache.GetNative(_version, _width, _height, GetPremultipliedBgra);
	}

	public CanvasBitmap? GetCanvasBitmap(ICanvasResourceCreator creator, float scale, SizeF? fittingSize)
	{
		if (_externalSource != null)
		{
			// only file based native images can be loaded into Win2D
			if (_externalCanvasBitmap == null && _externalSource is BitmapImage { UriSource.IsFile: true } bitmapImage)
				_externalCanvasBitmap = WinUIImageHelper.Wait(CanvasBitmap.LoadAsync(creator, bitmapImage.UriSource.LocalPath));
			return _externalCanvasBitmap;
		}
		return _cache.GetCanvasBitmap(creator, _version, _width, _height, GetPremultipliedBgra);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_cache.Dispose();
			_externalCanvasBitmap?.Dispose();
			_externalCanvasBitmap = null;
		}
		base.Dispose(disposing);
	}
}
