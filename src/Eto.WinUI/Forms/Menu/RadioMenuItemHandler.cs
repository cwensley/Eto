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

	RadioMenuItem? _controller;

	public RadioMenuItem? Controller
	{
		get => _controller;
		set
		{
			_controller = value;
			var controllerHandler = value?.Handler as RadioMenuItemHandler;
			if (controllerHandler?._group != null && ReferenceEquals(controllerHandler._group, _group))
				return;

			_group?.Remove(Widget);
			_group = null;
			if (controllerHandler != null)
			{
				controllerHandler._group ??= new List<RadioMenuItem> { value! };
				_group = controllerHandler._group;
				_group.Add(Widget);
				Control.GroupName = controllerHandler.Control.GroupName;
			}
			else
				Control.GroupName = Guid.NewGuid().ToString("N");

			if (Checked)
				UncheckGroup();
		}
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
