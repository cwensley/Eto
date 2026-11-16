using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

static class WindowNativeMethods
{
	public const int GWLP_HWNDPARENT = -8;
	public const int GWL_EXSTYLE = -20;
	public const int WS_EX_TOOLWINDOW = 0x00000080;
	public const int WS_EX_APPWINDOW = 0x00040000;
	public const int WS_EX_NOACTIVATE = 0x08000000;
	public const int WS_EX_LAYERED = 0x00080000;
	public const uint LWA_ALPHA = 0x2;
	public const int SW_SHOWNOACTIVATE = 4;
	public const int SW_SHOWNA = 8;

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsWindowEnabled(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern IntPtr GetFocus();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern uint GetDpiForWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

	[DllImport("user32.dll")]
	public static extern short GetKeyState(int nVirtKey);

	public const int DWMWA_CLOAK = 13;

	[DllImport("dwmapi.dll")]
	public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

	public static bool IsKeyDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

	public static bool IsKeyToggled(int virtualKey) => (GetKeyState(virtualKey) & 0x1) != 0;
}
