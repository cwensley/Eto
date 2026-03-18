namespace Eto.WinUI.Forms.Menu;

public class MenuBarHandler : MenuHandler<muc.MenuBar, MenuBar, MenuBar.ICallback>, MenuBar.IHandler
{
	public MenuBarHandler()
	{
		Control = new muc.MenuBar();
	}

	public void AddMenu(int index, MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler)
			throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI menu bars.");
		Control.Items.Insert(index, handler.GetTopLevelMenuBarItem());
	}

	public void RemoveMenu(MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler)
			return;
		Control.Items.Remove(handler.GetTopLevelMenuBarItem());
	}

	public void Clear()
	{
		Control.Items.Clear();
	}
	
	MenuItem? _quitItem;
	public void SetQuitItem(MenuItem item)
	{
		item.Order = 1000;
		if (_quitItem != null)
			ApplicationMenu.Items.Remove(_quitItem);
		else
			ApplicationMenu.Items.AddSeparator(999);
		ApplicationMenu.Items.Add(item);
		_quitItem = item;
	}

	MenuItem? _aboutItem;
	public void SetAboutItem(MenuItem item)
	{
		item.Order = 1000;
		if (_aboutItem != null)
			HelpMenu.Items.Remove(_aboutItem);
		else
			HelpMenu.Items.AddSeparator(999);
		HelpMenu.Items.Add(item);
		_aboutItem = item;
	}

	public void CreateSystemMenu()
	{
	}

	public void CreateLegacySystemMenu()
	{
	}

	public IEnumerable<Command> GetSystemCommands()
	{
		yield break;
	}

	public ButtonMenuItem ApplicationMenu => Widget.Items.GetSubmenu(Application.Instance.Localize(Widget, "&File"), -100);

	public ButtonMenuItem HelpMenu => Widget.Items.GetSubmenu(Application.Instance.Localize(Widget, "&Help"), 1000);

}
