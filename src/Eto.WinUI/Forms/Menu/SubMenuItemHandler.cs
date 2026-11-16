namespace Eto.WinUI.Forms.Menu;

public class SubMenuItemHandler : MenuItemBaseHandler<muc.MenuFlyoutSubItem, SubMenuItem, SubMenuItem.ICallback>, SubMenuItem.IHandler, IWinUIMenuItemHandler
{
	string? _text;

	public SubMenuItemHandler()
	{
		Control = new muc.MenuFlyoutSubItem();
	}

	protected override void OnImageSizeChanged()
	{
		Control.Icon = WinUIMenuHelper.CreateIcon(Image, WinUIMenuHelper.DefaultImageSize);
	}

	public override string? Text
	{
		get => _text;
		set
		{
			_text = value;
			WinUIMenuHelper.GetMenuTextAndAccessKey(value, out var menuText, out var accessKey);
			Control.Text = menuText;
			Control.AccessKey = accessKey;
			if (MenuBarItem != null)
			{
				MenuBarItem.Title = menuText;
				MenuBarItem.AccessKey = accessKey;
			}
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case SubMenuItem.OpeningEvent:
			case SubMenuItem.ClosedEvent:
			case SubMenuItem.ClosingEvent:
				// not supported by WinUI
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	protected override muc.MenuBarItem CreateMenuBarItem()
	{
		var item = base.CreateMenuBarItem();
		foreach (var subItem in Widget.Items)
		{
			if (subItem.Handler is IWinUIMenuItemHandler handler && handler.NativeControlObject is muc.MenuFlyoutItemBase nativeControl)
				item.Items.Add(nativeControl);
		}
		return item;
	}


	public void AddMenu(int index, MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI submenus.");

		Control.Items.Insert(index, nativeControl);
		if (MenuBarItem != null)
			MenuBarItem.Items.Insert(index, nativeControl);
	}

	public void RemoveMenu(MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			return;

		Control.Items.Remove(nativeControl);
		if (MenuBarItem != null)
			MenuBarItem.Items.Remove(nativeControl);
	}

	public void Clear()
	{
		Control.Items.Clear();
		if (MenuBarItem != null)
			MenuBarItem.Items.Clear();
	}

	public override void Validate()
	{
		base.Validate();

		foreach (var item in Widget.Items)
		{
			if (item.Handler is IWinUIMenuItemHandler handler)
				handler.Validate();
		}
	}

}
