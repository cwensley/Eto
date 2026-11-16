namespace Eto.WinUI.Forms.ToolBars;

public class ButtonToolItemHandler : ToolItemHandler<muc.AppBarButton, ButtonToolItem>, ButtonToolItem.IHandler
{
	public ButtonToolItemHandler()
	{
		Control = new muc.AppBarButton { LabelPosition = muc.CommandBarLabelPosition.Collapsed };
		Control.Click += Control_Click;
	}

	void Control_Click(object sender, mux.RoutedEventArgs e) => Widget.OnClick(EventArgs.Empty);
}
