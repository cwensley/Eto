namespace Eto.WinUI.Forms.Menu;

public class RadioMenuItemHandler : MenuItemHandler<muc.RadioMenuFlyoutItem, RadioMenuItem, RadioMenuItem.ICallback>, RadioMenuItem.IHandler
{
	List<RadioMenuItem>? _group;
	bool _suppressCheckedChanged;

	public RadioMenuItemHandler()
	{
		Control = new muc.RadioMenuFlyoutItem
		{
			GroupName = Guid.NewGuid().ToString("N")
		};
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

	protected override void OnClick()
	{
		Checked = true;
		base.OnClick();
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
				if (value)
					UncheckGroup();
				Control.IsChecked = value;
			}
			finally
			{
				_suppressCheckedChanged = false;
			}
		}
	}

	void UncheckGroup()
	{
		if (_group == null)
			return;

		var checkedItem = _group.FirstOrDefault(r => r.Checked && r != Widget);
		if (checkedItem != null)
			checkedItem.Checked = false;
	}

	public void Create(RadioMenuItem controller)
	{
		if (controller == null)
		{
			_group = new List<RadioMenuItem> { Widget };
			return;
		}

		var controllerHandler = (RadioMenuItemHandler)controller.Handler;
		controllerHandler._group ??= new List<RadioMenuItem> { controller };
		_group = controllerHandler._group;
		_group.Add(Widget);
		Control.GroupName = controllerHandler.Control.GroupName;
	}

	internal string GroupName => Control.GroupName;

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case RadioMenuItem.CheckedChangedEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
