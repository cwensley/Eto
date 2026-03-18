namespace Eto.WinUI.Forms.Menu;

public abstract class MenuHandler<TControl, TWidget, TCallback> : WidgetHandler<TControl, TWidget, TCallback>
	where TWidget : Eto.Forms.Menu
	where TCallback : Eto.Forms.Menu.ICallback
{
}

internal interface IWinUIMenuItemHandler
{
	object? NativeControlObject { get; }
	void Validate();
}

internal interface IWinUITopLevelMenuItemHandler
{
	muc.MenuBarItem GetTopLevelMenuBarItem();
}
