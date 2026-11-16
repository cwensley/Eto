using Eto.WinUI.Drawing;

namespace Eto.WinUI.Forms.Controls;

public class ImageViewHandler : WinUIBorderedControl<muc.Image, ImageView, ImageView.ICallback>, ImageView.IHandler
{
	Image? _image;

	protected override muc.Image CreateControl() => new muc.Image
	{
		Stretch = muxm.Stretch.Uniform,
		HorizontalAlignment = mux.HorizontalAlignment.Center,
		VerticalAlignment = mux.VerticalAlignment.Center
	};

	// the native image only knows its pixel size once decoded, so measure from the Eto image instead,
	// shrinking it to fit but never growing it
	public override sw.Size MeasureOverride(sw.Size constraint, Func<sw.Size, sw.Size> measure)
	{
		return base.MeasureOverride(constraint, available =>
		{
			var desired = measure(available);
			if (_image == null)
				return new sw.Size(0, 0);
			var size = _image.Size.ToWinUI();
			// not decoded yet
			if (size.Width <= 0 || size.Height <= 0)
				return desired;
			var scale = Math.Min(1, Math.Min(available.Width / Math.Max(1, size.Width), available.Height / Math.Max(1, size.Height)));
			return new sw.Size(size.Width * scale, size.Height * scale);
		});
	}

	public Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			Control.Source = GetSource(value);
			ContainerControl.InvalidateMeasure();
		}
	}

	static muxm.ImageSource? GetSource(Image? image) => image switch
	{
		Bitmap bitmap => bitmap.ToBitmapSource(),
		Icon icon => icon.GetFrame(1).Bitmap.ToBitmapSource(),
		_ => (image?.Handler as IWinUIImage)?.GetImageClosestToSize(1, null)
	};
}
