using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

public class KeyboardHandler : Keyboard.IHandler
{
	static readonly Keys[] s_supportedLockKeys = { Keys.CapsLock, Keys.NumberLock, Keys.ScrollLock, Keys.Insert };

	EventHandler<EventArgs>? _modifiersChanged;
	HookProc? _hookProc;
	IntPtr _hook;
	Keys _modifiers;
	Keys[] _lockedKeys = Array.Empty<Keys>();
	Keys? _downKeys;
	Keys? _upKeys;

	public IEnumerable<Keys> SupportedLockKeys => s_supportedLockKeys;

	public bool IsKeyLocked(Keys key) => key switch
	{
		Keys.CapsLock => WindowNativeMethods.IsKeyToggled(0x14),
		Keys.NumberLock => WindowNativeMethods.IsKeyToggled(0x90),
		Keys.ScrollLock => WindowNativeMethods.IsKeyToggled(0x91),
		Keys.Insert => WindowNativeMethods.IsKeyToggled(0x2D),
		_ => false
	};

	public Keys Modifiers
	{
		get
		{
			var modifiers = KeyMap.GetModifiers();
			// the key state isn't updated yet when the hook sees the message
			if (_downKeys != null)
				modifiers |= _downKeys.Value;
			if (_upKeys != null)
				modifiers &= ~_upKeys.Value;
			return modifiers;
		}
	}

	public event EventHandler<EventArgs> ModifiersChanged
	{
		add
		{
			if (_modifiersChanged == null)
			{
				_hookProc = HookCallback;
				_hook = SetWindowsHookEx(WH_GETMESSAGE, _hookProc, IntPtr.Zero, GetCurrentThreadId());
				_modifiers = Modifiers;
				_lockedKeys = GetLockedKeys();
			}
			_modifiersChanged += value;
		}
		remove
		{
			_modifiersChanged -= value;
			if (_modifiersChanged == null && _hook != IntPtr.Zero)
			{
				UnhookWindowsHookEx(_hook);
				_hook = IntPtr.Zero;
				_hookProc = null;
			}
		}
	}

	Keys[] GetLockedKeys() => s_supportedLockKeys.Where(IsKeyLocked).ToArray();

	IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
	{
		if (nCode >= 0 && wParam == (IntPtr)PM_REMOVE)
		{
			var msg = Marshal.PtrToStructure<MSG>(lParam);
			var isDown = msg.message == WM_KEYDOWN || msg.message == WM_SYSKEYDOWN;
			var isUp = msg.message == WM_KEYUP || msg.message == WM_SYSKEYUP;
			if (isDown || isUp)
			{
				var key = (int)msg.wParam switch
				{
					0x10 or 0xA0 or 0xA1 => Keys.Shift,
					0x11 or 0xA2 or 0xA3 => Keys.Control,
					0x12 or 0xA4 or 0xA5 => Keys.Alt,
					0x5B or 0x5C => Keys.Application,
					_ => Keys.None
				};
				if (isDown)
					_downKeys = key;
				else
					_upKeys = key;

				var modifiers = Modifiers;
				var lockedKeys = GetLockedKeys();
				_downKeys = null;
				_upKeys = null;
				if (modifiers != _modifiers || !lockedKeys.SequenceEqual(_lockedKeys))
				{
					_modifiers = modifiers;
					_lockedKeys = lockedKeys;
					_modifiersChanged?.Invoke(null, EventArgs.Empty);
				}
			}
		}
		return CallNextHookEx(_hook, nCode, wParam, lParam);
	}

	const int WH_GETMESSAGE = 3;
	const int PM_REMOVE = 1;
	const int WM_KEYDOWN = 0x0100;
	const int WM_KEYUP = 0x0101;
	const int WM_SYSKEYDOWN = 0x0104;
	const int WM_SYSKEYUP = 0x0105;

	delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	struct MSG
	{
		public IntPtr hwnd;
		public int message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public int ptX;
		public int ptY;
	}

	[DllImport("user32.dll", SetLastError = true)]
	static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("user32.dll")]
	static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll")]
	static extern uint GetCurrentThreadId();
}
