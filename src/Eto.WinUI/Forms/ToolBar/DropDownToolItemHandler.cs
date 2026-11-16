using Eto.WinUI.Forms.Menu;

namespace Eto.WinUI.Forms.ToolBars;

public class DropDownToolItemHandler : ToolItemHandler<muc.AppBarButton, DropDownToolItem>, DropDownToolItem.IHandler
{
	// theme resource the AppBarButton template reads when it shows the chevron for a flyout
	const string ChevronVisibilityKey = "AppBarButtonHasFlyoutChevronVisibility";
	readonly muc.MenuFlyout _menu = new();

	public DropDownToolItemHandler()
	{
		Control = new muc.AppBarButton { LabelPosition = muc.CommandBarLabelPosition.Collapsed, Flyout = _menu };
		Control.Click += Control_Click;
		_menu.Opening += Menu_Opening;
	}

	void Control_Click(object sender, mux.RoutedEventArgs e) => Widget.OnClick(EventArgs.Empty);

	void Menu_Opening(object? sender, object e)
	{
		foreach (var item in Widget.Items)
		{
			if (item.Handler is IWinUIMenuItemHandler handler)
				handler.Validate();
		}
	}

	public bool ShowDropArrow
	{
		get => !Control.Resources.TryGetValue(ChevronVisibilityKey, out var value) || value is not mux.Visibility.Collapsed;
		set
		{
			if (value)
				Control.Resources.Remove(ChevronVisibilityKey);
			else
				Control.Resources[ChevronVisibilityKey] = mux.Visibility.Collapsed;

			// re-enter the flyout visual state so the template picks up the new resource
			Control.Flyout = null;
			Control.Flyout = _menu;
		}
	}

	public void AddMenu(int index, MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI tool items.");
		_menu.Items.Insert(index, nativeControl);
	}

	public void RemoveMenu(MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			return;
		_menu.Items.Remove(nativeControl);
	}

	public void Clear() => _menu.Items.Clear();
}
