namespace Eto.WinUI.Forms.ToolBars;

public class CheckToolItemHandler : ToolItemHandler<muc.AppBarToggleButton, CheckToolItem>, CheckToolItem.IHandler
{
	public CheckToolItemHandler()
	{
		Control = new muc.AppBarToggleButton { IsThreeState = false, LabelPosition = muc.CommandBarLabelPosition.Collapsed };
		Control.Checked += Control_CheckedChanged;
		Control.Unchecked += Control_CheckedChanged;
		Control.Click += Control_Click;
	}

	void Control_Click(object sender, mux.RoutedEventArgs e) => Widget.OnClick(EventArgs.Empty);

	void Control_CheckedChanged(object sender, mux.RoutedEventArgs e) => Widget.OnCheckedChanged(EventArgs.Empty);

	public bool Checked
	{
		get => Control.IsChecked == true;
		set => Control.IsChecked = value;
	}
}
