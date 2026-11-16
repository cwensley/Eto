namespace Eto.WinUI.Forms.Controls;

public class CheckBoxHandler : WinUIControl<muc.CheckBox, CheckBox, CheckBox.ICallback>, CheckBox.IHandler
{
	public string? Text
	{
		get => Control.Content as string;
		set => Control.Content = value;
	}
	public bool? Checked
	{
		get => Control.IsChecked;
		set => Control.IsChecked = value;
	}
	public bool ThreeState
	{
		get => Control.IsThreeState;
		set => Control.IsThreeState = value;
	}
	public bool UseMnemonic { get; set; }
	public bool AlwaysShowMnemonic { get; set; }

	// WinUI's style gives it a minimum width of 120, so it would be wider than its content
	protected override muc.CheckBox CreateControl() => new() { MinWidth = 0 };

	protected override void Initialize()
	{
		base.Initialize();
		Control.Checked += Control_Checked;
		Control.Unchecked += Control_Checked;
	}

	private void Control_Checked(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnCheckedChanged(Widget, EventArgs.Empty);
	}

}
