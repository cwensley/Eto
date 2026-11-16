using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Implemented by image handlers so they can be shown in WinUI controls and drawn with Win2D.
/// </summary>
internal interface IWinUIImage
{
	/// <summary>Gets a native image to show in a XAML element; only call on the UI thread.</summary>
	BitmapSource? GetImageClosestToSize(float scale, Size? fittingSize);

	/// <summary>Gets a cached Win2D bitmap for the device of <paramref name="creator"/>; do not dispose it.</summary>
	CanvasBitmap? GetCanvasBitmap(ICanvasResourceCreator creator, float scale, SizeF? fittingSize);
}

/// <summary>
/// A decoded image frame with straight (non-premultiplied) BGRA pixels.
/// </summary>
internal readonly record struct DecodedFrame(int Width, int Height, byte[] Pixels);

internal static class WinUIImageHelper
{
	public static T Wait<T>(wf.IAsyncOperation<T> operation) => operation.AsTask().GetAwaiter().GetResult();

	public static void Wait(wf.IAsyncAction action) => action.AsTask().GetAwaiter().GetResult();

	public static List<DecodedFrame> Decode(Stream stream, bool allFrames)
	{
		using var copy = new MemoryStream();
		stream.CopyTo(copy);
		copy.Position = 0;
		using var ras = copy.AsRandomAccessStream();
		var decoder = Wait(BitmapDecoder.CreateAsync(ras));
		var count = allFrames ? decoder.FrameCount : 1;
		var frames = new List<DecodedFrame>((int)count);
		for (uint i = 0; i < count; i++)
		{
			var frame = Wait(decoder.GetFrameAsync(i));
			var data = Wait(frame.GetPixelDataAsync(
				BitmapPixelFormat.Bgra8,
				BitmapAlphaMode.Straight,
				new BitmapTransform(),
				ExifOrientationMode.IgnoreExifOrientation,
				ColorManagementMode.DoNotColorManage));
			frames.Add(new DecodedFrame((int)frame.PixelWidth, (int)frame.PixelHeight, data.DetachPixelData()));
		}
		return frames;
	}

	public static void Encode(Stream output, ImageFormat format, int width, int height, byte[] straightBgra)
	{
		var (encoderId, alphaMode) = format switch
		{
			ImageFormat.Png => (BitmapEncoder.PngEncoderId, BitmapAlphaMode.Straight),
			ImageFormat.Jpeg => (BitmapEncoder.JpegEncoderId, BitmapAlphaMode.Ignore),
			ImageFormat.Bitmap => (BitmapEncoder.BmpEncoderId, BitmapAlphaMode.Ignore),
			ImageFormat.Gif => (BitmapEncoder.GifEncoderId, BitmapAlphaMode.Straight),
			ImageFormat.Tiff => (BitmapEncoder.TiffEncoderId, BitmapAlphaMode.Straight),
			_ => throw new NotSupportedException($"Image format {format} is not supported")
		};
		using var ras = new InMemoryRandomAccessStream();
		var encoder = Wait(BitmapEncoder.CreateAsync(encoderId, ras));
		encoder.SetPixelData(BitmapPixelFormat.Bgra8, alphaMode, (uint)width, (uint)height, 96, 96, straightBgra);
		Wait(encoder.FlushAsync());
		ras.Seek(0);
		using var input = ras.GetInputStreamAt(0).AsStreamForRead();
		input.CopyTo(output);
	}

	public static byte[] Premultiply(byte[] straight)
	{
		var result = new byte[straight.Length];
		for (int i = 0; i < straight.Length; i += 4)
		{
			var a = straight[i + 3];
			if (a == 255)
			{
				result[i] = straight[i];
				result[i + 1] = straight[i + 1];
				result[i + 2] = straight[i + 2];
			}
			else if (a != 0)
			{
				result[i] = (byte)((straight[i] * a + 127) / 255);
				result[i + 1] = (byte)((straight[i + 1] * a + 127) / 255);
				result[i + 2] = (byte)((straight[i + 2] * a + 127) / 255);
			}
			result[i + 3] = a;
		}
		return result;
	}

	public static void Unpremultiply(byte[] pixels)
	{
		for (int i = 0; i < pixels.Length; i += 4)
		{
			var a = pixels[i + 3];
			if (a == 255 || a == 0)
				continue;
			pixels[i] = (byte)Math.Min(255, (pixels[i] * 255 + a / 2) / a);
			pixels[i + 1] = (byte)Math.Min(255, (pixels[i + 1] * 255 + a / 2) / a);
			pixels[i + 2] = (byte)Math.Min(255, (pixels[i + 2] * 255 + a / 2) / a);
		}
	}

	public static CanvasImageInterpolation ToCanvasInterpolation(this ImageInterpolation interpolation) => interpolation switch
	{
		ImageInterpolation.None => CanvasImageInterpolation.NearestNeighbor,
		ImageInterpolation.Low => CanvasImageInterpolation.Linear,
		ImageInterpolation.Medium => CanvasImageInterpolation.Cubic,
		ImageInterpolation.High => CanvasImageInterpolation.HighQualityCubic,
		_ => CanvasImageInterpolation.Linear,
	};
}

/// <summary>
/// Caches the Win2D and XAML representations of an image's pixels, rebuilding them when the version changes.
/// </summary>
internal sealed class WinUIImageCache : IDisposable
{
	readonly object _sync = new();
	CanvasBitmap? _canvasBitmap;
	CanvasDevice? _canvasDevice;
	int _canvasVersion = -1;
	WriteableBitmap? _native;
	int _nativeVersion = -1;

	/// <summary>Gets a Win2D bitmap from premultiplied BGRA pixels.</summary>
	public CanvasBitmap GetCanvasBitmap(ICanvasResourceCreator creator, int version, int width, int height, Func<byte[]> getPremultiplied)
	{
		var device = creator.Device;
		lock (_sync)
		{
			if (_canvasBitmap != null && _canvasVersion == version && ReferenceEquals(_canvasDevice, device))
				return _canvasBitmap;
			// D2D keeps its own reference while a pending draw still uses the old bitmap
			_canvasBitmap?.Dispose();
			_canvasBitmap = CanvasBitmap.CreateFromBytes(
				device,
				getPremultiplied(),
				Math.Max(1, width),
				Math.Max(1, height),
				DirectXPixelFormat.B8G8R8A8UIntNormalized,
				96f,
				CanvasAlphaMode.Premultiplied);
			_canvasDevice = device;
			_canvasVersion = version;
			return _canvasBitmap;
		}
	}

	/// <summary>Gets a XAML bitmap from premultiplied BGRA pixels; must be called on the UI thread.</summary>
	public BitmapSource GetNative(int version, int width, int height, Func<byte[]> getPremultiplied)
	{
		lock (_sync)
		{
			if (_native != null && _nativeVersion == version)
				return _native;
			// reuse the same object so elements already showing it pick up the change
			if (_native == null || _native.PixelWidth != width || _native.PixelHeight != height)
				_native = new WriteableBitmap(Math.Max(1, width), Math.Max(1, height));
			using (var stream = _native.PixelBuffer.AsStream())
			{
				var bytes = getPremultiplied();
				stream.Write(bytes, 0, Math.Min(bytes.Length, (int)stream.Length));
			}
			_native.Invalidate();
			_nativeVersion = version;
			return _native;
		}
	}

	public void Dispose()
	{
		lock (_sync)
		{
			_canvasBitmap?.Dispose();
			_canvasBitmap = null;
			_canvasDevice = null;
			_native = null;
		}
	}
}
