namespace Eto.WinUI.Forms.ToolBars;

public class RadioToolItemHandler : ToolItemHandler<RadioToolItemHandler.EtoRadioAppBarToggleButton, RadioToolItem>, RadioToolItem.IHandler
{
	public class EtoRadioAppBarToggleButton : muc.AppBarToggleButton
	{
		// clicking a checked radio item keeps it checked, like a radio button
		protected override void OnToggle()
		{
			if (IsChecked != true)
				base.OnToggle();
		}
	}

	public RadioToolItemHandler()
	{
		Control = new EtoRadioAppBarToggleButton { IsThreeState = false, LabelPosition = muc.CommandBarLabelPosition.Collapsed };
		Control.Checked += Control_Checked;
		Control.Unchecked += Control_Unchecked;
		Control.Click += Control_Click;
	}

	void Control_Click(object sender, mux.RoutedEventArgs e) => Widget.OnClick(EventArgs.Empty);

	void Control_Unchecked(object sender, mux.RoutedEventArgs e) => Widget.OnCheckedChanged(EventArgs.Empty);

	void Control_Checked(object sender, mux.RoutedEventArgs e)
	{
		// all radio items in a toolbar form one group
		if (Widget.Parent is Eto.Forms.ToolBar toolbar)
		{
			foreach (var item in toolbar.Items.OfType<RadioToolItem>())
			{
				if (item != Widget)
					item.Checked = false;
			}
		}
		Widget.OnCheckedChanged(EventArgs.Empty);
	}

	public bool Checked
	{
		get => Control.IsChecked == true;
		set => Control.IsChecked = value;
	}
}
