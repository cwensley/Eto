using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Eto.WinUI.Drawing;
using Eto.WinUI.Forms.Menu;
using Windows.Graphics.Imaging;
using static Eto.WinUI.Forms.ShellNative;

namespace Eto.WinUI.Forms;

/// <summary>
/// Tray icon using Shell_NotifyIcon, as WinUI has no tray support of its own.
/// </summary>
public class TrayIndicatorHandler : WidgetHandler<IntPtr, TrayIndicator, TrayIndicator.ICallback>, TrayIndicator.IHandler
{
	const int CallbackMessage = WM_APP + 1;
	const string WindowClassName = "Eto.WinUI.TrayIndicator";

	static int s_nextId;
	static readonly int s_taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
	static WndProc? s_wndProc;
	static readonly Dictionary<IntPtr, TrayIndicatorHandler> s_handlers = new();

	readonly int _id = Interlocked.Increment(ref s_nextId);
	Image? _image;
	IntPtr _icon;
	string? _title;
	bool _visible;
	bool _added;

	internal event EventHandler? BalloonClicked;
	internal event EventHandler? BalloonClosed;

	public TrayIndicatorHandler()
	{
		Control = CreateMessageWindow();
		s_handlers[Control] = this;
	}

	static IntPtr CreateMessageWindow()
	{
		if (s_wndProc == null)
		{
			// kept in a static so the delegate outlives the window class
			s_wndProc = WindowProc;
			var wc = new WNDCLASSEX
			{
				cbSize = Marshal.SizeOf<WNDCLASSEX>(),
				lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
				hInstance = GetModuleHandle(null),
				lpszClassName = WindowClassName
			};
			RegisterClassEx(ref wc);
		}
		// Not message-only, as those don't receive the TaskbarCreated broadcast when Explorer restarts.
		return CreateWindowEx(0, WindowClassName, null, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
	}

	static IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
	{
		if (s_handlers.TryGetValue(hWnd, out var handler))
		{
			try
			{
				if (msg == CallbackMessage)
				{
					handler.OnTrayMessage(wParam, lParam);
					return IntPtr.Zero;
				}
				if (msg == s_taskbarCreatedMessage && handler._added)
				{
					// Explorer restarted, so the icon is gone
					handler._added = false;
					handler.UpdateIcon();
				}
			}
			catch (Exception ex)
			{
				// exceptions can't cross back into native code
				Debug.WriteLine($"TrayIndicator: {ex}");
			}
		}
		return DefWindowProc(hWnd, msg, wParam, lParam);
	}

	void OnTrayMessage(IntPtr wParam, IntPtr lParam)
	{
		var message = (int)((long)lParam & 0xFFFF);
		switch (message)
		{
			case NIN_SELECT:
			case NIN_KEYSELECT:
				Callback.OnActivated(Widget, EventArgs.Empty);
				break;
			case WM_CONTEXTMENU:
				var x = (short)((long)wParam & 0xFFFF);
				var y = (short)(((long)wParam >> 16) & 0xFFFF);
				ShowMenu(x, y);
				break;
			case NIN_BALLOONUSERCLICK:
				BalloonClicked?.Invoke(this, EventArgs.Empty);
				break;
			case NIN_BALLOONTIMEOUT:
			case NIN_BALLOONHIDE:
				BalloonClosed?.Invoke(this, EventArgs.Empty);
				break;
		}
	}

	public string Title
	{
		get => _title ?? string.Empty;
		set
		{
			if (value != null && value.Length >= 128)
				throw new ArgumentOutOfRangeException(nameof(value), "Text limited to 127 characters");
			_title = value;
			UpdateIcon();
		}
	}

	public bool Visible
	{
		get => _visible;
		set
		{
			_visible = value;
			UpdateIcon();
		}
	}

	public Image Image
	{
		get => _image!;
		set
		{
			_image = value;
			var old = _icon;
			_icon = value != null ? WinUIIconHelper.CreateIcon(value, GetSystemMetrics(SM_CXSMICON)) : IntPtr.Zero;
			UpdateIcon();
			if (old != IntPtr.Zero)
				DestroyIcon(old);
		}
	}

	public ContextMenu? Menu { get; set; }

	internal IntPtr Icon => _icon;

	NOTIFYICONDATA CreateData(int flags) => new NOTIFYICONDATA
	{
		cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
		hWnd = Control,
		uID = _id,
		uFlags = flags,
		uCallbackMessage = CallbackMessage,
		// a blank slot is easy to miss, so fall back to the generic application icon
		hIcon = _icon != IntPtr.Zero ? _icon : LoadIcon(IntPtr.Zero, IDI_APPLICATION),
		szTip = _title ?? string.Empty,
		szInfo = string.Empty,
		szInfoTitle = string.Empty
	};

	void UpdateIcon()
	{
		if (Control == IntPtr.Zero)
			return;
		if (!_visible)
		{
			if (_added)
			{
				var remove = CreateData(0);
				Shell_NotifyIcon(NIM_DELETE, ref remove);
				_added = false;
			}
			return;
		}

		var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
		if (_added)
		{
			Shell_NotifyIcon(NIM_MODIFY, ref data);
			return;
		}
		if (Shell_NotifyIcon(NIM_ADD, ref data))
		{
			_added = true;
			data.uVersion = NOTIFYICON_VERSION_4;
			Shell_NotifyIcon(NIM_SETVERSION, ref data);
		}
	}

	/// <summary>
	/// Shows a balloon notification from this tray icon.
	/// </summary>
	internal bool ShowBalloon(string? title, string? message, IntPtr icon)
	{
		if (!_added)
			return false;
		var data = CreateData(NIF_INFO);
		data.szInfoTitle = Truncate(title, 63);
		data.szInfo = Truncate(string.IsNullOrEmpty(message) ? " " : message, 255);
		if (icon != IntPtr.Zero)
		{
			data.dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON;
			data.hBalloonIcon = icon;
		}
		else
			data.dwInfoFlags = NIIF_INFO;
		return Shell_NotifyIcon(NIM_MODIFY, ref data);
	}

	static string Truncate(string? value, int length) => value == null ? string.Empty : value.Length > length ? value.Substring(0, length) : value;

	void ShowMenu(int x, int y)
	{
		var menu = Menu;
		if (menu == null)
			return;

		(menu.Handler as ContextMenuHandler)?.Callback.OnOpening(menu, EventArgs.Empty);

		var commands = new List<MenuItem>();
		var hmenu = BuildMenu(menu.Items, commands);
		try
		{
			// Without this the menu doesn't close when clicking elsewhere.
			SetForegroundWindow(Control);
			var id = TrackPopupMenuEx(hmenu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, x, y, Control, IntPtr.Zero);
			PostMessage(Control, WM_NULL, IntPtr.Zero, IntPtr.Zero);

			(menu.Handler as ContextMenuHandler)?.Callback.OnClosed(menu, EventArgs.Empty);

			if (id > 0 && id <= commands.Count)
			{
				var item = commands[id - 1];
				// run after the menu is fully dismissed, like other platforms
				Application.Instance.AsyncInvoke(item.PerformClick);
			}
		}
		finally
		{
			DestroyMenu(hmenu);
		}
	}

	static IntPtr BuildMenu(IEnumerable<MenuItem> items, List<MenuItem> commands)
	{
		var hmenu = CreatePopupMenu();
		foreach (var item in items)
		{
			if (!item.Visible)
				continue;
			if (item is SeparatorMenuItem)
			{
				AppendMenu(hmenu, MF_SEPARATOR, IntPtr.Zero, null);
				continue;
			}

			item.PerformValidate();
			var text = item.Text ?? string.Empty;
			if (item.Shortcut != Keys.None)
				text += "\t" + item.Shortcut.ToShortcutString();
			uint flags = MF_STRING;
			if (!item.Enabled)
				flags |= MF_GRAYED;

			if (item is ISubmenu submenu && submenu.Items.Count > 0)
			{
				var child = BuildMenu(submenu.Items, commands);
				AppendMenu(hmenu, flags | MF_POPUP, child, text);
				continue;
			}

			if (item is CheckMenuItem check && check.Checked)
				flags |= MF_CHECKED;
			else if (item is RadioMenuItem radio && radio.Checked)
				flags |= MF_CHECKED | MFT_RADIOCHECK;

			commands.Add(item);
			AppendMenu(hmenu, flags, new IntPtr(commands.Count), text);
		}
		return hmenu;
	}

	protected override void Dispose(bool disposing)
	{
		if (Control != IntPtr.Zero)
		{
			_visible = false;
			UpdateIcon();
			s_handlers.Remove(Control);
			DestroyWindow(Control);
			Control = IntPtr.Zero;
		}
		if (_icon != IntPtr.Zero)
		{
			DestroyIcon(_icon);
			_icon = IntPtr.Zero;
		}
		base.Dispose(disposing);
	}
}

/// <summary>
/// Converts Eto images to Win32 icons.
/// </summary>
static class WinUIIconHelper
{
	/// <summary>
	/// Creates an HICON of the given pixel size, or IntPtr.Zero if the image has no readable data. Destroy it with DestroyIcon.
	/// </summary>
	public static IntPtr CreateIcon(Image image, int size)
	{
		try
		{
			if (image is Icon icon)
			{
				if (icon.Handler is IconHandler iconHandler && iconHandler.EncodedData is byte[] iconData)
				{
					var hicon = CreateIconFromData(iconData, size);
					if (hicon != IntPtr.Zero)
						return hicon;
				}
				image = icon.GetFrame(1, new Size(size, size)).Bitmap;
			}

			if (image.Handler is not BitmapHandler bitmapHandler)
				return IntPtr.Zero;

			var bitmapSize = image.Size;
			return CreateIconFromPixels(bitmapHandler.GetStraightBgra(), bitmapSize.Width, bitmapSize.Height, size);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Could not create icon: {ex}");
		}
		return IntPtr.Zero;
	}

	static IntPtr CreateIconFromData(byte[] data, int size)
	{
		// .ico files can hold an image for each size, so let Windows pick the right one
		if (data.Length > 4 && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0)
		{
			var hicon = CreateIconFromIco(data, size);
			if (hicon != IntPtr.Zero)
				return hicon;
		}

		using var stream = new MemoryStream(data);
		var decoder = BitmapDecoder.CreateAsync(stream.AsRandomAccessStream()).AsTask().GetAwaiter().GetResult();
		var transform = new BitmapTransform
		{
			ScaledWidth = (uint)size,
			ScaledHeight = (uint)size,
			InterpolationMode = BitmapInterpolationMode.Fant
		};
		var pixelData = decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask().GetAwaiter().GetResult();
		return ShellNative.CreateIcon(pixelData.DetachPixelData(), size, size);
	}

	static IntPtr CreateIconFromPixels(byte[] bgra, int width, int height, int size)
	{
		if (width == size && height == size)
			return ShellNative.CreateIcon(bgra, width, height);
		// nearest-neighbour is fine for the rare unencoded bitmap
		var scaled = new byte[size * size * 4];
		for (int y = 0; y < size; y++)
		{
			var sy = y * height / size;
			for (int x = 0; x < size; x++)
			{
				var sx = x * width / size;
				Array.Copy(bgra, (sy * width + sx) * 4, scaled, (y * size + x) * 4, 4);
			}
		}
		return ShellNative.CreateIcon(scaled, size, size);
	}
}
