namespace Eto.WinUI.Forms.Controls;

public class ComboBoxHandler : DropDownHandler<EtoComboBox, ComboBox, ComboBox.ICallback>, ComboBox.IHandler
{
	static readonly object ReadOnlyProperty = new();

	muc.TextBox? _editableTextBox;
	string? _lastText;
	bool _textChanging;

	protected override wf.Size DefaultSize => new wf.Size(100, double.NaN);

	protected override bool PreventUserResize => true;

	muc.TextBox? EditableTextBox => _editableTextBox ??= Control.FindVisualChild<muc.TextBox>();

	protected override void Initialize()
	{
		base.Initialize();
		Control.IsEditable = true;
		Control.IsTextSearchEnabled = false;
		Control.Loaded += Control_Loaded;
		Control.TextSubmitted += Control_TextSubmitted;
	}

	void Control_Loaded(object sender, mux.RoutedEventArgs e) => HookEditableTextBox();

	void HookEditableTextBox()
	{
		var textBox = Control.FindVisualChild<muc.TextBox>();
		if (ReferenceEquals(textBox, _editableTextBox))
			return;

		if (_editableTextBox != null)
			_editableTextBox.TextChanged -= EditableTextBox_TextChanged;

		_editableTextBox = textBox;

		if (_editableTextBox != null)
			_editableTextBox.TextChanged += EditableTextBox_TextChanged;
	}

	void EditableTextBox_TextChanged(object sender, muc.TextChangedEventArgs e) => HandleTextChanged();

	void Control_TextSubmitted(muc.ComboBox sender, muc.ComboBoxTextSubmittedEventArgs args)
	{
		HandleTextChanged();
		if (SelectedIndex == -1)
			args.Handled = true;
	}

	void HandleTextChanged()
	{
		if (_textChanging)
			return;

		HookEditableTextBox();

		try
		{
			_textChanging = true;

			var text = Text;
			if (text != _lastText)
			{
				Callback.OnTextChanged(Widget, EventArgs.Empty);
				_lastText = text;
			}

			var itemTextBinding = Widget.ItemTextBinding;
			var item = itemTextBinding != null ? DataStore?.FirstOrDefault(o => itemTextBinding.GetValue(o) == text) : null;
			if (item != null)
			{
				if (!Equals(Control.SelectedItem, item))
					Control.SelectedItem = item;
				return;
			}

			if (Control.SelectedIndex == -1)
				return;

			var textBox = EditableTextBox;
			if (textBox != null)
			{
				var selectionStart = textBox.SelectionStart;
				var selectionLength = textBox.SelectionLength;
				Control.SelectedIndex = -1;
				Control.Text = text;
				textBox.SelectionStart = selectionStart;
				textBox.SelectionLength = selectionLength;
			}
			else
			{
				Control.SelectedIndex = -1;
				Control.Text = text;
			}
		}
		finally
		{
			_textChanging = false;
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case ComboBox.TextChangedEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	public string Text
	{
		get => Control.Text;
		set
		{
			value ??= string.Empty;
			if (value == Text)
				return;

			Control.Text = value;
			HandleTextChanged();
		}
	}

	public bool ReadOnly
	{
		get
		{
			var textBox = EditableTextBox;
			return textBox?.IsReadOnly ?? Widget.Properties.Get(ReadOnlyProperty, false);
		}
		set => SetNullableValue(ReadOnlyProperty, value, () =>
		{
			HookEditableTextBox();
			return EditableTextBox!;
		}, (t, v) => t.IsReadOnly = v);
	}

	public bool AutoComplete
	{
		get => Control.IsTextSearchEnabled;
		set => Control.IsTextSearchEnabled = value;
	}
}
