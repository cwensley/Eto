using Windows.System;

namespace Eto.WinUI.Forms;

static class KeyMap
{
	static readonly Dictionary<VirtualKey, Keys> s_keys = new()
	{
		{ VirtualKey.Back, Keys.Backspace },
		{ VirtualKey.Tab, Keys.Tab },
		{ VirtualKey.Clear, Keys.Clear },
		{ VirtualKey.Enter, Keys.Enter },
		{ VirtualKey.Shift, Keys.LeftShift },
		{ VirtualKey.LeftShift, Keys.LeftShift },
		{ VirtualKey.RightShift, Keys.RightShift },
		{ VirtualKey.Control, Keys.LeftControl },
		{ VirtualKey.LeftControl, Keys.LeftControl },
		{ VirtualKey.RightControl, Keys.RightControl },
		{ VirtualKey.Menu, Keys.LeftAlt },
		{ VirtualKey.LeftMenu, Keys.LeftAlt },
		{ VirtualKey.RightMenu, Keys.RightAlt },
		{ VirtualKey.LeftWindows, Keys.LeftApplication },
		{ VirtualKey.RightWindows, Keys.RightApplication },
		{ VirtualKey.Pause, Keys.Pause },
		{ VirtualKey.CapitalLock, Keys.CapsLock },
		{ VirtualKey.Escape, Keys.Escape },
		{ VirtualKey.Space, Keys.Space },
		{ VirtualKey.PageUp, Keys.PageUp },
		{ VirtualKey.PageDown, Keys.PageDown },
		{ VirtualKey.End, Keys.End },
		{ VirtualKey.Home, Keys.Home },
		{ VirtualKey.Left, Keys.Left },
		{ VirtualKey.Up, Keys.Up },
		{ VirtualKey.Right, Keys.Right },
		{ VirtualKey.Down, Keys.Down },
		{ VirtualKey.Print, Keys.PrintScreen },
		{ VirtualKey.Snapshot, Keys.PrintScreen },
		{ VirtualKey.Insert, Keys.Insert },
		{ VirtualKey.Delete, Keys.Delete },
		{ VirtualKey.Help, Keys.Help },
		{ VirtualKey.Application, Keys.ContextMenu },
		{ VirtualKey.Multiply, Keys.Multiply },
		{ VirtualKey.Add, Keys.Add },
		{ VirtualKey.Subtract, Keys.Subtract },
		{ VirtualKey.Decimal, Keys.Decimal },
		{ VirtualKey.Divide, Keys.Divide },
		{ VirtualKey.NumberKeyLock, Keys.NumberLock },
		{ VirtualKey.Scroll, Keys.ScrollLock },
		{ (VirtualKey)186, Keys.Semicolon },
		{ (VirtualKey)187, Keys.Equal },
		{ (VirtualKey)188, Keys.Comma },
		{ (VirtualKey)189, Keys.Minus },
		{ (VirtualKey)190, Keys.Period },
		{ (VirtualKey)191, Keys.Slash },
		{ (VirtualKey)192, Keys.Grave },
		{ (VirtualKey)219, Keys.LeftBracket },
		{ (VirtualKey)220, Keys.Backslash },
		{ (VirtualKey)221, Keys.RightBracket },
		{ (VirtualKey)222, Keys.Quote },
	};

	/// <summary>Converts a WinUI virtual key to an Eto key without modifiers.</summary>
	public static Keys ToEto(this VirtualKey key)
	{
		if (key >= VirtualKey.A && key <= VirtualKey.Z)
			return Keys.A + (key - VirtualKey.A);
		if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
			return Keys.D0 + (key - VirtualKey.Number0);
		if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
			return Keys.Keypad0 + (key - VirtualKey.NumberPad0);
		if (key >= VirtualKey.F1 && key <= VirtualKey.F12)
			return Keys.F1 + (key - VirtualKey.F1);
		if (key >= VirtualKey.F13 && key <= VirtualKey.F24)
			return Keys.F13 + (key - VirtualKey.F13);
		return s_keys.TryGetValue(key, out var value) ? value : Keys.None;
	}

	/// <summary>Gets the modifier keys currently held down, according to the input state of this thread.</summary>
	public static Keys GetModifiers()
	{
		var modifiers = Keys.None;
		if (WindowNativeMethods.IsKeyDown((int)VirtualKey.Shift))
			modifiers |= Keys.Shift;
		if (WindowNativeMethods.IsKeyDown((int)VirtualKey.Control))
			modifiers |= Keys.Control;
		if (WindowNativeMethods.IsKeyDown((int)VirtualKey.Menu))
			modifiers |= Keys.Alt;
		if (WindowNativeMethods.IsKeyDown((int)VirtualKey.LeftWindows) || WindowNativeMethods.IsKeyDown((int)VirtualKey.RightWindows))
			modifiers |= Keys.Application;
		return modifiers;
	}

	public static KeyEventArgs ToEto(this mux.Input.KeyRoutedEventArgs e, KeyEventType keyType)
	{
		var key = e.Key.ToEto();
		if (key == Keys.None)
			return new KeyEventArgs(Keys.None, keyType);
		return new KeyEventArgs(key | GetModifiers(), keyType);
	}
}
