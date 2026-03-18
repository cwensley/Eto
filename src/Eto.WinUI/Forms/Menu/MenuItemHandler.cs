using Microsoft.UI.Xaml.Input;
using Windows.System;
using Eto.WinUI.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace Eto.WinUI.Forms.Menu;

public static class WinUIMenuHelper
{
	public static Size? DefaultImageSize = new(16, 16);
	internal static readonly object ImageSize_Key = new();

	internal static muc.IconElement? CreateIcon(Image? image, Size? imageSize)
	{
		if (image == null)
			return null;

		var source = (image.Handler as IWinUIImage)?.GetImageClosestToSize(Screen.PrimaryScreen.LogicalPixelSize, imageSize)
			?? image.ControlObject as Microsoft.UI.Xaml.Media.Imaging.BitmapSource;
		if (source == null)
			return null;

		return new muc.ImageIcon
		{
			Source = source
		};
	}

	static readonly Regex EtoMnemonic = new(@"(?<=([^_](?:[_]{2})*)|^)[_](?![_])", RegexOptions.Compiled);
	static readonly Regex PlatformMnemonic = new(@"(?<=([^&](?:[&]{2})*)|^)[&](?![&])", RegexOptions.Compiled);

	internal static string ToPlatformText(string? value)
	{
		if (value == null)
			return string.Empty;

		value = value.Replace("_", "__");

		var match = PlatformMnemonic.Match(value);
		if (match.Success)
		{
			var sb = new StringBuilder(value);
			sb[match.Index] = '_';
			sb.Replace("&&", "&");
			return sb.ToString();
		}

		return value.Replace("&&", "&");
	}

	internal static string? ToEtoText(string? value)
	{
		if (value == null)
			return null;

		var match = EtoMnemonic.Match(value);
		if (match.Success)
		{
			var sb = new StringBuilder(value);
			sb[match.Index] = '&';
			sb.Replace("__", "_");
			return sb.ToString();
		}

		return value.Replace("__", "_");
	}

	internal static void SetToolTip(mux.DependencyObject control, string? value)
	{
		muc.ToolTipService.SetToolTip(control, string.IsNullOrEmpty(value) ? null : value);
	}

	internal static KeyboardAccelerator? CreateKeyboardAccelerator(Keys value)
	{
		var key = value & Keys.KeyMask;
		if (key == Keys.None)
			return null;

		var accelerator = new KeyboardAccelerator
		{
			Key = key.ToVirtualKey(),
			Modifiers = value.ToVirtualKeyModifiers()
		};
		return accelerator;
	}

	internal static VirtualKeyModifiers ToVirtualKeyModifiers(this Keys value)
	{
		var modifiers = VirtualKeyModifiers.None;
		if ((value & Keys.Control) == Keys.Control)
			modifiers |= VirtualKeyModifiers.Control;
		if ((value & Keys.Shift) == Keys.Shift)
			modifiers |= VirtualKeyModifiers.Shift;
		if ((value & Keys.Alt) == Keys.Alt)
			modifiers |= VirtualKeyModifiers.Menu;
		if ((value & Keys.Application) == Keys.Application)
			modifiers |= VirtualKeyModifiers.Windows;
		return modifiers;
	}

	internal static VirtualKey ToVirtualKey(this Keys value) => value switch
	{
		Keys.A => VirtualKey.A,
		Keys.B => VirtualKey.B,
		Keys.C => VirtualKey.C,
		Keys.D => VirtualKey.D,
		Keys.E => VirtualKey.E,
		Keys.F => VirtualKey.F,
		Keys.G => VirtualKey.G,
		Keys.H => VirtualKey.H,
		Keys.I => VirtualKey.I,
		Keys.J => VirtualKey.J,
		Keys.K => VirtualKey.K,
		Keys.L => VirtualKey.L,
		Keys.M => VirtualKey.M,
		Keys.N => VirtualKey.N,
		Keys.O => VirtualKey.O,
		Keys.P => VirtualKey.P,
		Keys.Q => VirtualKey.Q,
		Keys.R => VirtualKey.R,
		Keys.S => VirtualKey.S,
		Keys.T => VirtualKey.T,
		Keys.U => VirtualKey.U,
		Keys.V => VirtualKey.V,
		Keys.W => VirtualKey.W,
		Keys.X => VirtualKey.X,
		Keys.Y => VirtualKey.Y,
		Keys.Z => VirtualKey.Z,
		Keys.F1 => VirtualKey.F1,
		Keys.F2 => VirtualKey.F2,
		Keys.F3 => VirtualKey.F3,
		Keys.F4 => VirtualKey.F4,
		Keys.F5 => VirtualKey.F5,
		Keys.F6 => VirtualKey.F6,
		Keys.F7 => VirtualKey.F7,
		Keys.F8 => VirtualKey.F8,
		Keys.F9 => VirtualKey.F9,
		Keys.F10 => VirtualKey.F10,
		Keys.F11 => VirtualKey.F11,
		Keys.F12 => VirtualKey.F12,
		Keys.D0 => VirtualKey.Number0,
		Keys.D1 => VirtualKey.Number1,
		Keys.D2 => VirtualKey.Number2,
		Keys.D3 => VirtualKey.Number3,
		Keys.D4 => VirtualKey.Number4,
		Keys.D5 => VirtualKey.Number5,
		Keys.D6 => VirtualKey.Number6,
		Keys.D7 => VirtualKey.Number7,
		Keys.D8 => VirtualKey.Number8,
		Keys.D9 => VirtualKey.Number9,
		Keys.Minus => VirtualKey.Subtract,
		Keys.Grave => (VirtualKey)192,
		Keys.Insert => VirtualKey.Insert,
		Keys.Home => VirtualKey.Home,
		Keys.PageUp => VirtualKey.PageUp,
		Keys.PageDown => VirtualKey.PageDown,
		Keys.Delete => VirtualKey.Delete,
		Keys.End => VirtualKey.End,
		Keys.Divide => VirtualKey.Divide,
		Keys.Decimal => VirtualKey.Decimal,
		Keys.Backspace => VirtualKey.Back,
		Keys.Up => VirtualKey.Up,
		Keys.Down => VirtualKey.Down,
		Keys.Left => VirtualKey.Left,
		Keys.Right => VirtualKey.Right,
		Keys.Tab => VirtualKey.Tab,
		Keys.Space => VirtualKey.Space,
		Keys.CapsLock => VirtualKey.CapitalLock,
		Keys.ScrollLock => VirtualKey.Scroll,
		Keys.PrintScreen => VirtualKey.Print,
		Keys.NumberLock => VirtualKey.NumberKeyLock,
		Keys.Enter => VirtualKey.Enter,
		Keys.Escape => VirtualKey.Escape,
		Keys.Multiply => VirtualKey.Multiply,
		Keys.Add => VirtualKey.Add,
		Keys.Subtract => VirtualKey.Subtract,
		Keys.Pause => VirtualKey.Pause,
		Keys.Clear => VirtualKey.Clear,
		Keys.Backslash => (VirtualKey)220,
		Keys.Equal => (VirtualKey)187,
		Keys.Semicolon => (VirtualKey)186,
		Keys.Quote => (VirtualKey)222,
		Keys.Comma => (VirtualKey)188,
		Keys.Period => (VirtualKey)190,
		Keys.Slash => (VirtualKey)191,
		Keys.RightBracket => (VirtualKey)221,
		Keys.LeftBracket => (VirtualKey)219,
		Keys.ContextMenu => VirtualKey.Application,
		Keys.Keypad0 => VirtualKey.NumberPad0,
		Keys.Keypad1 => VirtualKey.NumberPad1,
		Keys.Keypad2 => VirtualKey.NumberPad2,
		Keys.Keypad3 => VirtualKey.NumberPad3,
		Keys.Keypad4 => VirtualKey.NumberPad4,
		Keys.Keypad5 => VirtualKey.NumberPad5,
		Keys.Keypad6 => VirtualKey.NumberPad6,
		Keys.Keypad7 => VirtualKey.NumberPad7,
		Keys.Keypad8 => VirtualKey.NumberPad8,
		Keys.Keypad9 => VirtualKey.NumberPad9,
		_ => throw new NotSupportedException($"Shortcut key '{value}' is not supported by the WinUI menu handlers.")
	};
}

interface IMenuItemHandler
{
	void Validate();
}

public class MenuItemHandler<TControl, TWidget, TCallback> : MenuHandler<TControl, TWidget, TCallback>, MenuItem.IHandler, IWinUIMenuItemHandler, IMenuItemHandler
	where TControl : muc.MenuFlyoutItem
	where TWidget : MenuItem
	where TCallback : MenuItem.ICallback
{
	Image? _image;
	KeyboardAccelerator? _accelerator;

	protected override void Initialize()
	{
		base.Initialize();
		Control.Click += HandleClick;
	}

	void HandleClick(object sender, mux.RoutedEventArgs e)
	{
		OnClick();
	}

	protected virtual void OnClick()
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}

	public Size? ImageSize
	{
		get => Widget.Properties.Get<Size?>(WinUIMenuHelper.ImageSize_Key, WinUIMenuHelper.DefaultImageSize);
		set
		{
			if (Widget.Properties.TrySet(WinUIMenuHelper.ImageSize_Key, value, WinUIMenuHelper.DefaultImageSize))
				OnImageSizeChanged();
		}
	}

	protected virtual void OnImageSizeChanged()
	{
		Control.Icon = WinUIMenuHelper.CreateIcon(_image, ImageSize);
	}

	public Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			OnImageSizeChanged();
		}
	}

	public string Text
	{
		get => WinUIMenuHelper.ToEtoText(Control.Text);
		set => Control.Text = WinUIMenuHelper.ToPlatformText(value);
	}

	public string ToolTip
	{
		get => muc.ToolTipService.GetToolTip(Control) as string;
		set => WinUIMenuHelper.SetToolTip(Control, value);
	}

	public Keys Shortcut
	{
		get;
		set;
	}

	public bool Enabled
	{
		get => Control.IsEnabled;
		set => Control.IsEnabled = value;
	}

	public bool Visible
	{
		get => Control.Visibility == mux.Visibility.Visible;
		set => Control.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case MenuItem.ValidateEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	Keys MenuItem.IHandler.Shortcut
	{
		get => Shortcut;
		set
		{
			Shortcut = value;
			Control.KeyboardAccelerators.Clear();
			_accelerator = WinUIMenuHelper.CreateKeyboardAccelerator(value);
			if (_accelerator != null)
			{
				Control.KeyboardAccelerators.Add(_accelerator);
				Control.KeyboardAcceleratorTextOverride = value.ToShortcutString();
			}
			else
			{
				Control.KeyboardAcceleratorTextOverride = null;
			}
		}
	}

	object? IWinUIMenuItemHandler.NativeControlObject => Control;

	public void CreateFromCommand(Command command)
	{
	}

	public virtual void Validate()
	{
		Callback.OnValidate(Widget, EventArgs.Empty);
	}
}
