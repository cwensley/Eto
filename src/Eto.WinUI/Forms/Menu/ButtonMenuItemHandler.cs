namespace Eto.WinUI.Forms.Menu;

public class ButtonMenuItemHandler : MenuItemHandler<muc.MenuFlyoutItem, ButtonMenuItem, ButtonMenuItem.ICallback>, ButtonMenuItem.IHandler
{
	public ButtonMenuItemHandler()
	{
		Control = new muc.MenuFlyoutItem();
	}

	public void AddMenu(int index, MenuItem item) => throw new NotSupportedException("Use SubMenuItem for nested menus on WinUI.");

	public void RemoveMenu(MenuItem item) => throw new NotSupportedException("Use SubMenuItem for nested menus on WinUI.");

	public void Clear() => throw new NotSupportedException("Use SubMenuItem for nested menus on WinUI.");
}
