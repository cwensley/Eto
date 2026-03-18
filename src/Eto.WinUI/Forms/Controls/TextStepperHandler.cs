using Eto.WinUI.CustomControls;
using Windows.System;

namespace Eto.WinUI.Forms.Controls;

public class TextStepperHandler : WinUIControl<EtoButtonSpinner, TextStepper, TextStepper.ICallback>, TextStepper.IHandler
{
	readonly EtoTextBox _textBox;
	bool _suppressNativeTextChanging;
	bool _suppressNativeTextChanged;

	public TextStepperHandler()
	{
		_textBox = new EtoTextBox
		{
			Handler = this,
			HorizontalAlignment = mux.HorizontalAlignment.Stretch
		};
	}

	public override mux.FrameworkElement FocusControl => _textBox;

	protected override wf.Size DefaultSize => new wf.Size(100, _textBox.MinHeight);

	protected override bool PreventUserResize => true;

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override Color TextColor
	{
		get => _textBox.Foreground.ToEtoColor();
		set => _textBox.Foreground = value.ToWinUIBrush();
	}

	public bool ReadOnly
	{
		get => _textBox.IsReadOnly;
		set => _textBox.IsReadOnly = value;
	}

	public int MaxLength
	{
		get => _textBox.MaxLength;
		set => _textBox.MaxLength = value;
	}

	public string PlaceholderText
	{
		get => _textBox.PlaceholderText;
		set => _textBox.PlaceholderText = value;
	}

	public int CaretIndex
	{
		get => _textBox.SelectionStart;
		set
		{
			_textBox.SelectionStart = value;
			_textBox.SelectionLength = 0;
		}
	}

	public Range<int> Selection
	{
		get => Eto.Forms.Range.FromLength(_textBox.SelectionStart, _textBox.SelectionLength);
		set
		{
			_textBox.SelectionStart = value.Start;
			_textBox.SelectionLength = value.Length();
		}
	}

	public bool ShowBorder
	{
		get => !_textBox.BorderThickness.ToEto().IsZero;
		set => _textBox.BorderThickness = new mux.Thickness(value ? 1 : 0);
	}

	public TextAlignment TextAlignment
	{
		get => _textBox.TextAlignment.ToEto();
		set => _textBox.TextAlignment = value.ToWinUI();
	}

	public AutoSelectMode AutoSelectMode { get; set; }

	public string Text
	{
		get => _textBox.Text;
		set
		{
			value ??= string.Empty;
			if (value == _textBox.Text)
				return;

			if (!HandleProgrammaticTextChanging(value))
				return;

			try
			{
				_suppressNativeTextChanging = true;
				_suppressNativeTextChanged = true;
				_textBox.Text = value;
			}
			finally
			{
				_suppressNativeTextChanging = false;
				_suppressNativeTextChanged = false;
			}

			Callback.OnTextChanged(Widget, EventArgs.Empty);
		}
	}

	public bool AlwaysShowSelection
	{
		get => _textBox.SelectionHighlightColorWhenNotFocused != null;
		set => _textBox.SelectionHighlightColorWhenNotFocused = value ? _textBox.SelectionHighlightColor : null;
	}

	public StepperValidDirections ValidDirection
	{
		get
		{
			var direction = StepperValidDirections.None;
			if (Control.ValidSpinDirection.HasFlag(ValidSpinDirections.Increase))
				direction |= StepperValidDirections.Up;
			if (Control.ValidSpinDirection.HasFlag(ValidSpinDirections.Decrease))
				direction |= StepperValidDirections.Down;
			return direction;
		}
		set
		{
			var direction = ValidSpinDirections.None;
			if (value.HasFlag(StepperValidDirections.Up))
				direction |= ValidSpinDirections.Increase;
			if (value.HasFlag(StepperValidDirections.Down))
				direction |= ValidSpinDirections.Decrease;
			Control.ValidSpinDirection = direction;
		}
	}

	public bool ShowStepper
	{
		get => Control.ShowButtonSpinner;
		set => Control.ShowButtonSpinner = value;
	}

	protected override EtoButtonSpinner CreateControl() => new() { Handler = this, Content = _textBox };

	protected override void Initialize()
	{
		base.Initialize();
		_textBox.TextChanged += TextBox_TextChanged;
		_textBox.TextChanging += TextBox_TextChanging;
		_textBox.BeforeTextChanging += TextBox_BeforeTextChanging;
		_textBox.KeyDown += TextBox_KeyDown;
		Control.Spin += Control_Spin;
		ValidDirection = StepperValidDirections.Both;
		ShowStepper = true;
	}

	bool HandleProgrammaticTextChanging(string newText)
	{
		if (!IsEventHandled(TextBox.TextChangingEvent))
			return true;

		var args = new TextChangingEventArgs(_textBox.Text, newText, false);
		Callback.OnTextChanging(Widget, args);
		return !args.Cancel;
	}

	void TextBox_BeforeTextChanging(muc.TextBox sender, muc.TextBoxBeforeTextChangingEventArgs args)
	{
		if (_suppressNativeTextChanging || !IsEventHandled(TextBox.TextChangingEvent))
			return;

		var etoArgs = new TextChangingEventArgs(_textBox.Text, args.NewText, true);
		Callback.OnTextChanging(Widget, etoArgs);
		args.Cancel = etoArgs.Cancel;
	}

	void TextBox_TextChanging(muc.TextBox sender, muc.TextBoxTextChangingEventArgs args)
	{
		if (_suppressNativeTextChanging || !IsEventHandled(TextBox.TextChangingEvent))
			return;
	}

	void TextBox_TextChanged(object sender, muc.TextChangedEventArgs e)
	{
		if (_suppressNativeTextChanged)
			return;

		Callback.OnTextChanged(Widget, EventArgs.Empty);
	}

	void TextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Up && ValidDirection.HasFlag(StepperValidDirections.Up))
		{
			Callback.OnStep(Widget, new StepperEventArgs(StepperDirection.Up));
			e.Handled = true;
		}
		else if (e.Key == VirtualKey.Down && ValidDirection.HasFlag(StepperValidDirections.Down))
		{
			Callback.OnStep(Widget, new StepperEventArgs(StepperDirection.Down));
			e.Handled = true;
		}
	}

	void Control_Spin(object? sender, SpinEventArgs e)
	{
		Callback.OnStep(Widget, new StepperEventArgs(
			e.Direction == SpinDirection.Increase ? StepperDirection.Up : StepperDirection.Down));
	}

	public void SelectAll() => _textBox.SelectAll();

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextBox.TextChangedEvent:
			case TextBox.TextChangingEvent:
			case TextStepper.StepEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
