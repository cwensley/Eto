namespace Eto.WinUI.Drawing;

public class IconFrameHandler : IconFrame.IHandler
{
	// defers loading a frame's bitmap until it is actually used
	sealed class LazyFrame(Func<Stream> load)
	{
		readonly Lazy<Bitmap> _bitmap = new(() =>
		{
			using var stream = load();
			return new Bitmap(stream);
		});

		public Bitmap Bitmap => _bitmap.Value;
	}

	public object Create(IconFrame frame, Bitmap bitmap) => bitmap;

	public object Create(IconFrame frame, Func<Stream> load) => new LazyFrame(load);

	public object Create(IconFrame frame, Stream stream) => new Bitmap(stream);

	public Bitmap GetBitmap(IconFrame frame) => frame.ControlObject switch
	{
		Bitmap bitmap => bitmap,
		LazyFrame lazy => lazy.Bitmap,
		_ => throw new InvalidOperationException("Icon frame has no bitmap")
	};

	public Size GetPixelSize(IconFrame frame) => GetBitmap(frame).Size;
}
