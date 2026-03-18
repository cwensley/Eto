namespace Eto.WinUI.Forms.Menu;

public class SeparatorMenuItemHandler : WidgetHandler<muc.MenuFlyoutSeparator, SeparatorMenuItem>, SeparatorMenuItem.IHandler, IWinUIMenuItemHandler
{
	public SeparatorMenuItemHandler()
	{
		Control = new muc.MenuFlyoutSeparator();
	}

	public string Text
	{
		get => null;
		set => throw new NotSupportedException();
	}

	public string ToolTip
	{
		get => null;
		set => throw new NotSupportedException();
	}

	public Keys Shortcut
	{
		get => Keys.None;
		set => throw new NotSupportedException();
	}

	public bool Enabled
	{
		get => false;
		set => throw new NotSupportedException();
	}

	public bool Visible
	{
		get => Control.Visibility == mux.Visibility.Visible;
		set => Control.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	object? IWinUIMenuItemHandler.NativeControlObject => Control;

	public void CreateFromCommand(Command command)
	{
	}

	void IWinUIMenuItemHandler.Validate()
	{
	}
}

