namespace Eto.WinUI.Forms.Menu;

public class MenuBarHandler : MenuHandler<muc.MenuBar, MenuBar, MenuBar.ICallback>, MenuBar.IHandler
{
	public MenuBarHandler()
	{
		Control = new muc.MenuBar();
	}

	public void AddMenu(int index, MenuItem item)
	{
		Rebuild();
	}

	public void RemoveMenu(MenuItem item)
	{
		Rebuild();
	}

	public void Clear()
	{
		Rebuild();
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

	void Rebuild()
	{
		Control.Items.Clear();
		foreach (var item in Widget.Items)
			Control.Items.Add(CreateTopLevelItem(item));
	}

	muc.MenuBarItem CreateTopLevelItem(MenuItem item)
	{
		var menuBarItem = new muc.MenuBarItem
		{
			Title = WinUIMenuHelper.ToEtoText(item.Text)?.Replace("&", "")
		};

		if (item is ButtonMenuItem submenu && submenu.Items.Count > 0)
		{
			foreach (var child in submenu.Items)
				menuBarItem.Items.Add(CreateFlyoutItem(child));
		}
		else
		{
			menuBarItem.Items.Add(CreateFlyoutItem(item));
		}

		return menuBarItem;
	}

	static muc.MenuFlyoutItemBase CreateFlyoutItem(MenuItem item)
	{
		switch (item)
		{
			case SeparatorMenuItem:
				return new muc.MenuFlyoutSeparator();
			case CheckMenuItem checkItem:
			{
				var native = new muc.ToggleMenuFlyoutItem
				{
					Text = WinUIMenuHelper.ToPlatformText(checkItem.Text),
					IsChecked = checkItem.Checked,
					IsEnabled = checkItem.Enabled,
					Visibility = checkItem.Visible ? mux.Visibility.Visible : mux.Visibility.Collapsed
				};
				WinUIMenuHelper.SetToolTip(native, checkItem.ToolTip);
				native.Click += (_, _) =>
				{
					checkItem.PerformClick();
					native.IsChecked = checkItem.Checked;
				};
				return native;
			}
			case RadioMenuItem radioItem:
			{
				var native = new muc.RadioMenuFlyoutItem
				{
					Text = WinUIMenuHelper.ToPlatformText(radioItem.Text),
					IsChecked = radioItem.Checked,
					IsEnabled = radioItem.Enabled,
					Visibility = radioItem.Visible ? mux.Visibility.Visible : mux.Visibility.Collapsed,
					GroupName = radioItem.Handler is RadioMenuItemHandler radioHandler ? radioHandler.GroupName : Guid.NewGuid().ToString("N")
				};
				WinUIMenuHelper.SetToolTip(native, radioItem.ToolTip);
				native.Click += (_, _) =>
				{
					radioItem.PerformClick();
					native.IsChecked = radioItem.Checked;
				};
				return native;
			}
			case ButtonMenuItem submenu when submenu.Items.Count > 0:
			{
				var native = new muc.MenuFlyoutSubItem
				{
					Text = WinUIMenuHelper.ToPlatformText(submenu.Text),
					IsEnabled = submenu.Enabled,
					Visibility = submenu.Visible ? mux.Visibility.Visible : mux.Visibility.Collapsed
				};
				WinUIMenuHelper.SetToolTip(native, submenu.ToolTip);
				if (submenu.Image != null)
					native.Icon = WinUIMenuHelper.CreateIcon(submenu.Image, WinUIMenuHelper.DefaultImageSize);
				foreach (var child in submenu.Items)
					native.Items.Add(CreateFlyoutItem(child));
				return native;
			}
			case ButtonMenuItem buttonItem:
			{
				var native = new muc.MenuFlyoutItem
				{
					Text = WinUIMenuHelper.ToPlatformText(buttonItem.Text),
					IsEnabled = buttonItem.Enabled,
					Visibility = buttonItem.Visible ? mux.Visibility.Visible : mux.Visibility.Collapsed,
					KeyboardAcceleratorTextOverride = buttonItem.Shortcut == Keys.None ? null : buttonItem.Shortcut.ToShortcutString()
				};
				var accelerator = WinUIMenuHelper.CreateKeyboardAccelerator(buttonItem.Shortcut);
				if (accelerator != null)
					native.KeyboardAccelerators.Add(accelerator);
				WinUIMenuHelper.SetToolTip(native, buttonItem.ToolTip);
				if (buttonItem.Image != null)
					native.Icon = WinUIMenuHelper.CreateIcon(buttonItem.Image, WinUIMenuHelper.DefaultImageSize);
				native.Click += (_, _) => buttonItem.PerformClick();
				return native;
			}
			default:
				throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI menu bars.");
		}
	}
}
