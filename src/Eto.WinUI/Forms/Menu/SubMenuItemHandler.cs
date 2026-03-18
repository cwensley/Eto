namespace Eto.WinUI.Forms.Menu;

public class SubMenuItemHandler : MenuHandler<muc.MenuFlyoutSubItem, SubMenuItem, SubMenuItem.ICallback>, SubMenuItem.IHandler, IWinUIMenuItemHandler
{
	Image? _image;

	public SubMenuItemHandler()
	{
		Control = new muc.MenuFlyoutSubItem();
	}

	public Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			Control.Icon = WinUIMenuHelper.CreateIcon(value, WinUIMenuHelper.DefaultImageSize);
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
		get => Keys.None;
		set
		{
		}
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
			case SubMenuItem.OpeningEvent:
			case SubMenuItem.ClosedEvent:
			case SubMenuItem.ClosingEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	public void AddMenu(int index, MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI submenus.");

		Control.Items.Insert(index, nativeControl);
	}

	public void RemoveMenu(MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			return;

		Control.Items.Remove(nativeControl);
	}

	public void Clear()
	{
		Control.Items.Clear();
	}

	object? IWinUIMenuItemHandler.NativeControlObject => Control;

	public void CreateFromCommand(Command command)
	{
	}

	public void Validate()
	{
		Callback.OnValidate(Widget, EventArgs.Empty);
		foreach (var item in Widget.Items)
		{
			if (item.Handler is IMenuItemHandler handler)
				handler.Validate();
		}
	}
}
