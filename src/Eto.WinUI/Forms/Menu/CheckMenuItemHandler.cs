namespace Eto.WinUI.Forms.Menu;

public class CheckMenuItemHandler : MenuItemHandler<muc.ToggleMenuFlyoutItem, CheckMenuItem, CheckMenuItem.ICallback>, CheckMenuItem.IHandler
{
	bool _suppressCheckedChanged;

	public CheckMenuItemHandler()
	{
		Control = new muc.ToggleMenuFlyoutItem();
	}

	protected override void Initialize()
	{
		base.Initialize();
		Control.Click += HandleNativeClick;
	}

	void HandleNativeClick(object sender, mux.RoutedEventArgs e)
	{
		if (!_suppressCheckedChanged)
			Callback.OnCheckedChanged(Widget, EventArgs.Empty);
	}

	public bool Checked
	{
		get => Control.IsChecked;
		set
		{
			if (Control.IsChecked == value)
				return;
			_suppressCheckedChanged = true;
			try
			{
				Control.IsChecked = value;
			}
			finally
			{
				_suppressCheckedChanged = false;
			}
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case CheckMenuItem.CheckedChangedEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
