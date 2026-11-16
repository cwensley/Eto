using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Indexed bitmap kept as palette indices in memory, converted to colour when shown or drawn.
/// </summary>
public class IndexedBitmapHandler : WidgetHandler<IndexedBitmap>, IndexedBitmap.IHandler, IWinUIImage
{
	readonly object _sync = new();
	readonly WinUIImageCache _cache = new();
	byte[] _data = [];
	int _width;
	int _height;
	int _bitsPerPixel;
	int _stride;
	int _version;
	GCHandle _lockHandle;
	Palette _palette = new();

	static int GetStride(int width, int bitsPerPixel) => (width * bitsPerPixel + 31) / 32 * 4;

	public void Create(int width, int height, int bitsPerPixel)
	{
		if (bitsPerPixel != 1 && bitsPerPixel != 2 && bitsPerPixel != 4 && bitsPerPixel != 8)
			throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), "Indexed bitmaps must be 1, 2, 4 or 8 bits per pixel");
		_width = width;
		_height = height;
		_bitsPerPixel = bitsPerPixel;
		_stride = GetStride(width, bitsPerPixel);
		_data = new byte[_stride * height];
		// greyscale ramp until a palette is assigned
		var count = 1 << bitsPerPixel;
		_palette = new Palette(Enumerable.Range(0, count).Select(i => Color.FromGrayscale(i / (float)(count - 1))));
		_version++;
	}

	public void Resize(int width, int height)
	{
		lock (_sync)
		{
			var stride = GetStride(width, _bitsPerPixel);
			var data = new byte[stride * height];
			var rowBytes = Math.Min(stride, _stride);
			for (int y = 0; y < Math.Min(height, _height); y++)
				Buffer.BlockCopy(_data, y * _stride, data, y * stride, rowBytes);
			_data = data;
			_stride = stride;
			_width = width;
			_height = height;
			_version++;
		}
	}

	public Palette Palette
	{
		get => _palette;
		set
		{
			_palette = value;
			_version++;
		}
	}

	public Size Size => new Size(_width, _height);

	public BitmapData Lock()
	{
		lock (_sync)
		{
			_lockHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
			return new BitmapDataHandler(Widget, _lockHandle.AddrOfPinnedObject(), _stride, _bitsPerPixel, null, false);
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

	byte[] GetPremultipliedBgra()
	{
		lock (_sync)
		{
			var colors = _palette.Select(r => r.ToArgb()).ToArray();
			var straight = new byte[_width * _height * 4];
			var mask = (1 << _bitsPerPixel) - 1;
			var dst = 0;
			for (int y = 0; y < _height; y++)
			{
				var row = y * _stride;
				for (int x = 0; x < _width; x++)
				{
					var bit = x * _bitsPerPixel;
					// pixels are packed from the most significant bit, like other platforms
					var index = (_data[row + bit / 8] >> (8 - _bitsPerPixel - bit % 8)) & mask;
					var argb = index < colors.Length ? colors[index] : 0;
					straight[dst++] = (byte)argb;
					straight[dst++] = (byte)(argb >> 8);
					straight[dst++] = (byte)(argb >> 16);
					straight[dst++] = (byte)(argb >> 24);
				}
			}
			return WinUIImageHelper.Premultiply(straight);
		}
	}

	public BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize) => _cache.GetNative(_version, _width, _height, GetPremultipliedBgra);

	public CanvasBitmap? GetCanvasBitmap(ICanvasResourceCreator creator, float scale, SizeF? fittingSize) => _cache.GetCanvasBitmap(creator, _version, _width, _height, GetPremultipliedBgra);

	protected override void Dispose(bool disposing)
	{
		if (disposing)
			_cache.Dispose();
		base.Dispose(disposing);
	}
}
