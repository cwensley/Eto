using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Test.UnitTests;
using Eto.WinUI.Forms;

namespace Eto.Test.WinUI
{
	public class TestInput : ITestInput
	{
		const int WM_KEYDOWN = 0x0100;
		const int WM_KEYUP = 0x0101;

		[DllImport("user32.dll")]
		static extern IntPtr GetFocus();

		[DllImport("user32.dll")]
		static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

		static IWinUIWindow GetHandler(Window window) => (IWinUIWindow)window.Handler;

		public Task SendKeyDownAsync(Window window, Keys key) => SendKeyAsync(window, key, WM_KEYDOWN);

		public Task SendKeyUpAsync(Window window, Keys key) => SendKeyAsync(window, key, WM_KEYUP);

		static async Task SendKeyAsync(Window window, Keys key, int msg)
		{
			// WinUI takes keyboard input from the focused input site child window, so post it there
			var target = GetFocus();
			if (target == IntPtr.Zero)
				target = window.NativeHandle;

			// key-up needs its transition flags, or TranslateMessage types the character a second time
			var lParam = msg == WM_KEYUP ? unchecked((IntPtr)(int)0xC0000001) : (IntPtr)1;
			PostMessage(target, msg, (IntPtr)GetVirtualKey(key), lParam);

			// the message has to go around the message loop before WinUI delivers it
			await Task.Delay(50);
		}

		static int GetVirtualKey(Keys key)
		{
			var code = key & Keys.KeyMask;
			if (code >= Keys.A && code <= Keys.Z)
				return 'A' + (code - Keys.A);
			if (code >= Keys.D0 && code <= Keys.D9)
				return '0' + (code - Keys.D0);
			if (code >= Keys.Keypad0 && code <= Keys.Keypad9)
				return 0x60 + (code - Keys.Keypad0);
			if (code >= Keys.F1 && code <= Keys.F12)
				return 0x70 + (code - Keys.F1);
			return code switch
			{
				Keys.Space => 0x20,
				Keys.Enter => 0x0D,
				Keys.Tab => 0x09,
				Keys.Escape => 0x1B,
				Keys.Backspace => 0x08,
				Keys.Delete => 0x2E,
				Keys.Insert => 0x2D,
				Keys.Home => 0x24,
				Keys.End => 0x23,
				Keys.PageUp => 0x21,
				Keys.PageDown => 0x22,
				Keys.Left => 0x25,
				Keys.Up => 0x26,
				Keys.Right => 0x27,
				Keys.Down => 0x28,
				Keys.LeftShift or Keys.RightShift => 0x10,
				Keys.LeftControl or Keys.RightControl => 0x11,
				Keys.LeftAlt or Keys.RightAlt => 0x12,
				_ => 0
			};
		}

		static object GetFocusedElement(Window window)
		{
			var root = GetHandler(window).ContainerControl.XamlRoot;
			return root != null ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) : null;
		}

		static bool HasNativeFocus(Window window)
		{
			var hwnd = window.NativeHandle;
			var focus = GetFocus();
			return focus != IntPtr.Zero && (focus == hwnd || IsChild(hwnd, focus));
		}

		// WinUI has no focusable window element, so the window has focus "itself" when nothing inside it does
		public bool IsWindowFocusedItself(Window window)
		{
			if (!HasNativeFocus(window))
				return false;
			var focused = GetFocusedElement(window);
			return focused == null || ReferenceEquals(focused, GetHandler(window).ContainerControl);
		}

		public bool IsFocusWithinWindow(Window window) => HasNativeFocus(window);
	}
}
