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

	protected override muc.RadioButton CreateControl() => new();

	public void Create(RadioButton controller)
	{
		if (controller?.ControlObject is muc.RadioButton parent)
			Control.GroupName = parent.GroupName;
		else
			Control.GroupName = Guid.NewGuid().ToString();
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
		Callback.OnCheckedChanged(Widget, EventArgs.Empty);
	}

	void Control_Click(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}
}
