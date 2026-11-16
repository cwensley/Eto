namespace Eto.WinUI.Forms.Controls;

public class RadioButtonHandler : WinUIControl<muc.RadioButton, RadioButton, RadioButton.ICallback>, RadioButton.IHandler
{
	public string Text
	{
		get => Control.Content as string ?? string.Empty;
		set => Control.Content = value;
	}

	public bool Checked
	{
		get => Control.IsChecked == true;
		set => Control.IsChecked = value;
	}

	public bool UseMnemonic { get; set; }

	public bool AlwaysShowMnemonic { get; set; }

	RadioButton? _controller;
	bool _settingGroup;

	// WinUI's style gives it a minimum width of 120, so it would be wider than its content
	protected override muc.RadioButton CreateControl() => new() { GroupName = Guid.NewGuid().ToString(), MinWidth = 0 };

	public RadioButton? Controller
	{
		get => _controller;
		set
		{
			_controller = value;
			var groupName = (value?.ControlObject as muc.RadioButton)?.GroupName ?? Guid.NewGuid().ToString();
			if (Control.GroupName == groupName)
				return;

			Control.GroupName = groupName;
			if (Control.IsChecked == true)
			{
				// WinUI only unchecks the rest of the group when a button becomes checked
				_settingGroup = true;
				try
				{
					Control.IsChecked = false;
					Control.IsChecked = true;
				}
				finally
				{
					_settingGroup = false;
				}
			}
		}
	}

	protected override void Initialize()
	{
		base.Initialize();
		Control.Checked += Control_CheckedChanged;
		Control.Unchecked += Control_CheckedChanged;
		Control.Click += Control_Click;
	}

	void Control_CheckedChanged(object sender, mux.RoutedEventArgs e)
	{
		if (!_settingGroup)
			Callback.OnCheckedChanged(Widget, EventArgs.Empty);
	}

	void Control_Click(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}
}
