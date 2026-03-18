namespace Eto.WinUI.Forms.Controls;

public class PasswordBoxHandler : WinUIControl<muc.PasswordBox, PasswordBox, PasswordBox.ICallback>, PasswordBox.IHandler
{
	protected override bool PreventUserResize => true;

	public bool ReadOnly
	{
		get => false;
		set
		{
		}
	}

	public int MaxLength
	{
		get => Control.MaxLength;
		set => Control.MaxLength = value;
	}

	public char PasswordChar
	{
		get => string.IsNullOrEmpty(Control.PasswordChar) ? '\0' : Control.PasswordChar[0];
		set => Control.PasswordChar = value == '\0' ? string.Empty : value.ToString();
	}

	public string Text
	{
		get => Control.Password;
		set => Control.Password = value ?? string.Empty;
	}

	protected override wf.Size DefaultSize => new wf.Size(100, Control.MinHeight);

	protected override muc.PasswordBox CreateControl() => new();

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextControl.TextChangedEvent:
				Control.PasswordChanged += Control_PasswordChanged;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void Control_PasswordChanged(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnTextChanged(Widget, EventArgs.Empty);
	}
}
