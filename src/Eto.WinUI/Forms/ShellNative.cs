using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

/// <summary>
/// Win32 interop for the tray, taskbar, printing and message pumping.
/// </summary>
static class ShellNative
{
	public const int WM_NULL = 0x0000;
	public const int WM_DESTROY = 0x0002;
	public const int WM_COMMAND = 0x0111;
	public const int WM_CONTEXTMENU = 0x007B;
	public const int WM_LBUTTONUP = 0x0202;
	public const int WM_RBUTTONUP = 0x0205;
	public const int WM_USER = 0x0400;
	public const int WM_APP = 0x8000;

	public const int NIN_SELECT = WM_USER + 0;
	public const int NIN_KEYSELECT = WM_USER + 1;
	public const int NIN_BALLOONSHOW = WM_USER + 2;
	public const int NIN_BALLOONHIDE = WM_USER + 3;
	public const int NIN_BALLOONTIMEOUT = WM_USER + 4;
	public const int NIN_BALLOONUSERCLICK = WM_USER + 5;

	public const int NIM_ADD = 0;
	public const int NIM_MODIFY = 1;
	public const int NIM_DELETE = 2;
	public const int NIM_SETVERSION = 4;
	public const int NOTIFYICON_VERSION_4 = 4;

	public const int NIF_MESSAGE = 0x01;
	public const int NIF_ICON = 0x02;
	public const int NIF_TIP = 0x04;
	public const int NIF_INFO = 0x10;
	public const int NIF_SHOWTIP = 0x80;

	public const int NIIF_NONE = 0x00;
	public const int NIIF_INFO = 0x01;
	public const int NIIF_WARNING = 0x02;
	public const int NIIF_ERROR = 0x03;
	public const int NIIF_USER = 0x04;
	public const int NIIF_LARGE_ICON = 0x20;

	public const uint MF_STRING = 0x0000;
	public const uint MF_GRAYED = 0x0001;
	public const uint MF_CHECKED = 0x0008;
	public const uint MF_POPUP = 0x0010;
	public const uint MF_SEPARATOR = 0x0800;
	public const uint MFT_RADIOCHECK = 0x0200;

	public const uint TPM_RIGHTBUTTON = 0x0002;
	public const uint TPM_RETURNCMD = 0x0100;
	public const uint TPM_BOTTOMALIGN = 0x0020;

	public const int SM_CXSMICON = 49;
	public const int SM_CXICON = 11;

	public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct NOTIFYICONDATA
	{
		public int cbSize;
		public IntPtr hWnd;
		public int uID;
		public int uFlags;
		public int uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
		public int dwState;
		public int dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;
		public int uVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;
		public int dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

	public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WNDCLASSEX
	{
		public int cbSize;
		public int style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		public string? lpszMenuName;
		public string lpszClassName;
		public IntPtr hIconSm;
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string? lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsWindow(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int RegisterWindowMessage(string lpString);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr GetModuleHandle(string? lpModuleName);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	public static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32.dll")]
	public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

	[DllImport("user32.dll")]
	public static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll")]
	public static extern uint GetDpiForSystem();

	// Icons

	[StructLayout(LayoutKind.Sequential)]
	public struct ICONINFO
	{
		[MarshalAs(UnmanagedType.Bool)]
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BITMAPINFOHEADER
	{
		public int biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	[DllImport("gdi32.dll")]
	public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

	[DllImport("gdi32.dll")]
	public static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DeleteObject(IntPtr hObject);

	public static readonly IntPtr IDI_APPLICATION = new IntPtr(32512);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

	[DllImport("user32.dll")]
	public static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport("user32.dll")]
	public static extern IntPtr CreateIconFromResourceEx(ref byte presbits, int dwResSize, [MarshalAs(UnmanagedType.Bool)] bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint flags);

	/// <summary>
	/// Creates an icon from 32bpp premultiplied BGRA pixels, top-down.
	/// </summary>
	public static IntPtr CreateIcon(byte[] bgra, int width, int height)
	{
		var header = new BITMAPINFOHEADER
		{
			biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
			biWidth = width,
			biHeight = -height, // top-down
			biPlanes = 1,
			biBitCount = 32
		};
		var color = CreateDIBSection(IntPtr.Zero, ref header, 0, out var bits, IntPtr.Zero, 0);
		if (color == IntPtr.Zero)
			return IntPtr.Zero;
		var mask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
		try
		{
			// icons expect straight alpha
			var pixels = new byte[bgra.Length];
			for (int i = 0; i < bgra.Length; i += 4)
			{
				var a = bgra[i + 3];
				if (a > 0 && a < 255)
				{
					pixels[i] = (byte)Math.Min(255, bgra[i] * 255 / a);
					pixels[i + 1] = (byte)Math.Min(255, bgra[i + 1] * 255 / a);
					pixels[i + 2] = (byte)Math.Min(255, bgra[i + 2] * 255 / a);
				}
				else
				{
					pixels[i] = bgra[i];
					pixels[i + 1] = bgra[i + 1];
					pixels[i + 2] = bgra[i + 2];
				}
				pixels[i + 3] = a;
			}
			Marshal.Copy(pixels, 0, bits, Math.Min(pixels.Length, width * height * 4));
			var info = new ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };
			return CreateIconIndirect(ref info);
		}
		finally
		{
			DeleteObject(color);
			DeleteObject(mask);
		}
	}

	/// <summary>
	/// Creates an icon of the given size from .ico file data.
	/// </summary>
	public static IntPtr CreateIconFromIco(byte[] ico, int size)
	{
		if (ico.Length < 6 || BitConverter.ToUInt16(ico, 2) != 1)
			return IntPtr.Zero;
		int count = BitConverter.ToUInt16(ico, 4);
		int best = -1, bestScore = int.MaxValue;
		for (int i = 0; i < count; i++)
		{
			var entry = 6 + i * 16;
			if (entry + 16 > ico.Length)
				break;
			int w = ico[entry] == 0 ? 256 : ico[entry];
			int bpp = BitConverter.ToUInt16(ico, entry + 6);
			// prefer the smallest image at least as big as requested, then the most colours
			var score = (w >= size ? w - size : 10000 + size - w) * 64 - bpp;
			if (score < bestScore)
			{
				bestScore = score;
				best = entry;
			}
		}
		if (best < 0)
			return IntPtr.Zero;
		var length = BitConverter.ToInt32(ico, best + 8);
		var offset = BitConverter.ToInt32(ico, best + 12);
		if (offset < 0 || length <= 0 || offset + length > ico.Length)
			return IntPtr.Zero;
		var image = new byte[length];
		Array.Copy(ico, offset, image, 0, length);
		return CreateIconFromResourceEx(ref image[0], length, true, 0x00030000, size, size, 0);
	}

	// Message pumping

	[StructLayout(LayoutKind.Sequential)]
	public struct MSG
	{
		public IntPtr hwnd;
		public int message;
		public IntPtr wParam;
		public IntPtr lParam;
		public int time;
		public POINT pt;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	static extern uint MsgWaitForMultipleObjectsEx(uint nCount, IntPtr pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);

	const uint PM_REMOVE = 0x0001;
	const uint QS_ALLINPUT = 0x04FF;
	const uint MWMO_INPUTAVAILABLE = 0x0004;
	const int WM_QUIT = 0x0012;

	/// <summary>
	/// Dispatches pending messages, waiting up to <paramref name="timeout"/> ms for one to arrive.
	/// </summary>
	public static void PumpMessages(uint timeout)
	{
		MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, timeout, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
		while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
		{
			if (msg.message == WM_QUIT)
			{
				// leave it for the real loop
				PostQuitMessage((int)msg.wParam);
				return;
			}
			TranslateMessage(ref msg);
			DispatchMessage(ref msg);
		}
	}

	[DllImport("user32.dll")]
	static extern void PostQuitMessage(int nExitCode);

	// Taskbar

	[ComImport]
	[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ITaskbarList3
	{
		// ITaskbarList
		[PreserveSig] int HrInit();
		[PreserveSig] int AddTab(IntPtr hwnd);
		[PreserveSig] int DeleteTab(IntPtr hwnd);
		[PreserveSig] int ActivateTab(IntPtr hwnd);
		[PreserveSig] int SetActiveAlt(IntPtr hwnd);
		// ITaskbarList2
		[PreserveSig] int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
		// ITaskbarList3
		[PreserveSig] int SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
		[PreserveSig] int SetProgressState(IntPtr hwnd, int tbpFlags);
	}

	[ComImport]
	[Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
	[ClassInterface(ClassInterfaceType.None)]
	public class TaskbarList
	{
	}
}
