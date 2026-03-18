namespace Eto.WinUI.Forms.Controls;

public class LinkButtonHandler : WinUIControl<muc.HyperlinkButton, LinkButton, LinkButton.ICallback>, LinkButton.IHandler
{
	static readonly object DisabledTextColor_Key = new();
	static readonly object TextColor_Key = new();

	readonly muc.TextBlock _textBlock = new()
	{
		TextWrapping = mux.TextWrapping.WrapWholeWords
	};

	protected override muc.HyperlinkButton CreateControl() => new();

	protected override void Initialize()
	{
		base.Initialize();
		Control.Content = _textBlock;
		Control.Click += Control_Click;
		Control.IsEnabledChanged += Control_IsEnabledChanged;
		UpdateForeground();
	}

	void Control_Click(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}

	void Control_IsEnabledChanged(object sender, mux.DependencyPropertyChangedEventArgs e)
	{
		UpdateForeground();
	}

	public string Text
	{
		get => _textBlock.Text;
		set => _textBlock.Text = value ?? string.Empty;
	}

	public override Color TextColor
	{
		get => Widget.Properties.Get<Color?>(TextColor_Key) ?? base.TextColor;
		set
		{
			Widget.Properties[TextColor_Key] = value;
			UpdateForeground();
		}
	}

	public Color DisabledTextColor
	{
		get => Widget.Properties.Get<Color?>(DisabledTextColor_Key) ?? Colors.Gray;
		set
		{
			Widget.Properties[DisabledTextColor_Key] = value;
			UpdateForeground();
		}
	}

	public override Font Font
	{
		get => base.Font;
		set => base.Font = value;
	}

	public bool UseMnemonic { get; set; }

	public bool AlwaysShowMnemonic { get; set; }

	void UpdateForeground()
	{
		var color = Control.IsEnabled ? Widget.Properties.Get<Color?>(TextColor_Key) : DisabledTextColor;
		if (color == null)
		{
			Control.ClearValue(muc.Control.ForegroundProperty);
			_textBlock.ClearValue(muc.TextBlock.ForegroundProperty);
			return;
		}

		var brush = color.Value.ToWinUIBrush();
		Control.Foreground = brush;
		_textBlock.Foreground = brush;
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextControl.TextChangedEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
