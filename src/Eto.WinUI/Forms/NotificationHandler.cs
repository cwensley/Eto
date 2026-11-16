using System.Runtime.InteropServices.WindowsRuntime;
using Eto.WinUI.Drawing;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Graphics.Imaging;

namespace Eto.WinUI.Forms;

/// <summary>
/// Shows toast notifications with the Windows App SDK, falling back to a tray balloon where they aren't available.
/// </summary>
public class NotificationHandler : WidgetHandler<object, Notification, Notification.ICallback>, Notification.IHandler
{
	const string IdArgument = "eto-id";
	const string UserDataArgument = "eto-data";

	static bool? s_registered;
	static TrayIndicator? s_sharedIndicator;

	public string? Title { get; set; }

	public string? Message { get; set; }

	public string? UserData { get; set; }

	public Image? ContentImage { get; set; }

	public bool RequiresTrayIndicator => !EnsureRegistered();

	static bool EnsureRegistered()
	{
		if (s_registered != null)
			return s_registered.Value;
		try
		{
			if (!AppNotificationManager.IsSupported())
				return (s_registered = false).Value;
			var manager = AppNotificationManager.Default;
			// must be hooked before registering, or the activation that launched us is lost
			manager.NotificationInvoked += Manager_NotificationInvoked;
			// unpackaged apps register their exe as the activator here
			manager.Register();
			// so clicking a toast after we exit starts the app again rather than erroring
			AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
			{
				try
				{
					manager.Unregister();
				}
				catch
				{
					// nothing to do while exiting
				}
			};
			s_registered = true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"App notifications are unavailable, using the tray instead: {ex}");
			s_registered = false;
		}
		return s_registered.Value;
	}

	static void Manager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
	{
		args.Arguments.TryGetValue(IdArgument, out var id);
		args.Arguments.TryGetValue(UserDataArgument, out var userData);
		// raised on a background thread
		Application.Instance?.AsyncInvoke(() => OnActivated(id, userData));
	}

	static void OnActivated(string? id, string? userData)
	{
		if (Application.Instance?.Handler is ApplicationHandler app)
			app.Callback.OnNotificationActivated(app.Widget, new NotificationEventArgs(id, userData));
	}

	public void Show(TrayIndicator? indicator = null)
	{
		if (EnsureRegistered() && ShowToast())
			return;
		ShowBalloon(indicator);
	}

	bool ShowToast()
	{
		try
		{
			var builder = new AppNotificationBuilder()
				.AddArgument(IdArgument, ID ?? string.Empty)
				.AddArgument(UserDataArgument, UserData ?? string.Empty);
			if (!string.IsNullOrEmpty(Title))
				builder.AddText(Title);
			if (!string.IsNullOrEmpty(Message))
				builder.AddText(Message);
			var imagePath = SaveContentImage();
			if (imagePath != null)
				builder.SetAppLogoOverride(new Uri(imagePath));

			var notification = builder.BuildNotification();
			AppNotificationManager.Default.Show(notification);
			return notification.Id != 0;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Could not show app notification: {ex}");
			return false;
		}
	}

	static byte[] EncodePng(BitmapHandler bitmap)
	{
		using var ms = new MemoryStream();
		bitmap.Save(ms, ImageFormat.Png);
		return ms.ToArray();
	}

	// Toasts can only show images from a file, so write it as a png named by its content.
	string? SaveContentImage()
	{
		var image = ContentImage;
		if (image == null)
			return null;
		byte[]? data = image.Handler switch
		{
			IconHandler icon => icon.EncodedData,
			BitmapHandler bitmap => EncodePng(bitmap),
			_ => null
		};
		if (data == null)
			return null;

		var folder = Path.Combine(Path.GetTempPath(), "Eto.WinUI.Notifications");
		Directory.CreateDirectory(folder);
		var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data));
		var path = Path.Combine(folder, hash + ".png");
		if (File.Exists(path))
			return path;

		using var input = new MemoryStream(data);
		var decoder = BitmapDecoder.CreateAsync(input.AsRandomAccessStream()).AsTask().GetAwaiter().GetResult();
		// icons have a frame per size, so use the largest
		BitmapFrame frame = decoder.GetFrameAsync(0).AsTask().GetAwaiter().GetResult();
		for (uint i = 1; i < decoder.FrameCount; i++)
		{
			var next = decoder.GetFrameAsync(i).AsTask().GetAwaiter().GetResult();
			if (next.PixelWidth > frame.PixelWidth)
				frame = next;
		}
		var pixels = frame.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask().GetAwaiter().GetResult();

		var output = new Windows.Storage.Streams.InMemoryRandomAccessStream();
		var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
		encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, frame.PixelWidth, frame.PixelHeight, 96, 96, pixels.DetachPixelData());
		encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
		using (var file = File.Create(path))
		{
			output.Seek(0);
			output.AsStreamForRead().CopyTo(file);
		}
		return path;
	}

	static readonly object NotificationHandler_Key = new object();

	void ShowBalloon(TrayIndicator? indicator)
	{
		indicator ??= GetSharedIndicator();
		if (indicator.Handler is not TrayIndicatorHandler tray)
			return;

		if (indicator.Properties.Get<NotificationHandler>(NotificationHandler_Key) is NotificationHandler current)
			current.Unhook(tray);

		var icon = ContentImage != null ? WinUIIconHelper.CreateIcon(ContentImage, ShellNative.GetSystemMetrics(ShellNative.SM_CXICON)) : IntPtr.Zero;
		tray.BalloonClicked += Tray_BalloonClicked;
		tray.BalloonClosed += Tray_BalloonClosed;
		indicator.Properties.Set(NotificationHandler_Key, this);
		tray.ShowBalloon(Title, Message, icon);
		// the shell copies the icon
		if (icon != IntPtr.Zero)
			ShellNative.DestroyIcon(icon);
	}

	static TrayIndicator GetSharedIndicator()
	{
		if (s_sharedIndicator == null)
		{
			s_sharedIndicator = new TrayIndicator();
			if (Application.Instance?.MainForm?.Icon is Icon icon)
				s_sharedIndicator.Image = icon;
		}
		s_sharedIndicator.Show();
		return s_sharedIndicator;
	}

	void Unhook(TrayIndicatorHandler tray)
	{
		tray.BalloonClicked -= Tray_BalloonClicked;
		tray.BalloonClosed -= Tray_BalloonClosed;
	}

	void Tray_BalloonClosed(object? sender, EventArgs e)
	{
		if (sender is TrayIndicatorHandler tray)
			Unhook(tray);
		s_sharedIndicator?.Hide();
	}

	void Tray_BalloonClicked(object? sender, EventArgs e)
	{
		Tray_BalloonClosed(sender, e);
		OnActivated(ID, UserData);
	}
}
