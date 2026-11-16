
namespace Eto.WinUI.Forms.Controls;


public class TextBoxHandler : WinUIControl<muc.TextBox, TextBox, TextBox.ICallback>, TextBox.IHandler, SearchBox.IHandler
{
	protected override bool PreventUserResize => true;

	public bool ReadOnly
	{
		get => Control.IsReadOnly;
		set => Control.IsReadOnly = value;
	}
	public int MaxLength
	{
		get => Control.MaxLength;
		set => Control.MaxLength = value;
	}
	public string PlaceholderText
	{
		get => Control.PlaceholderText;
		set => Control.PlaceholderText = value;
	}
	public int CaretIndex
	{
		get => Control.SelectionStart;
		set
		{
			Control.SelectionStart = value;
			Control.SelectionLength = 0;
		}
	}
	public Range<int> Selection
	{
		get => Eto.Forms.Range.FromLength(Control.SelectionStart, Control.SelectionLength);
		set
		{
			Control.SelectionStart = value.Start;
			Control.SelectionLength = value.Length();
		}
	}
	public bool ShowBorder
	{
		get => !Control.BorderThickness.ToEto().IsZero;
		set => Control.BorderThickness = new mux.Thickness(value ? 1 : 0);
	}
	public TextAlignment TextAlignment
	{
		get => Control.TextAlignment.ToEto();
		set => Control.TextAlignment = value.ToWinUI();
	}
	public AutoSelectMode AutoSelectMode { get; set; }
	public string Text
	{
		get => Control.Text;
		set => Control.Text = value;
	}
	public bool AlwaysShowSelection
	{
		get => Control.SelectionHighlightColorWhenNotFocused != null;
		set => Control.SelectionHighlightColorWhenNotFocused = value ? Control.SelectionHighlightColor : null;
	}

	public int GetCharacterIndex(PointF location) => GetCharacterIndex(Control, location);

	public PaddingF TextInset => GetTextInset(Control);

	public void SelectAll() => Control.SelectAll();

	/// <summary>Gets the insertion index nearest to <paramref name="location"/>, in the text box's own coordinates.</summary>
	internal static int GetCharacterIndex(muc.TextBox textBox, PointF location)
	{
		var length = textBox.Text?.Length ?? 0;
		if (length == 0)
			return 0;

		if (location.X <= GetInsertionX(textBox, 0, length))
			return 0;

		if (location.X >= GetInsertionX(textBox, length, length))
			return length;

		var low = 0;
		var high = length;
		while (low < high)
		{
			var mid = (low + high) / 2;
			if (location.X <= GetInsertionX(textBox, mid, length))
				high = mid;
			else
				low = mid + 1;
		}

		var right = low;
		var left = Math.Max(0, right - 1);
		return Math.Abs(location.X - GetInsertionX(textBox, left, length)) <= Math.Abs(GetInsertionX(textBox, right, length) - location.X) ? left : right;
	}

	// the text is laid out inside the border and padding of the default template
	internal static PaddingF GetTextInset(muc.TextBox textBox)
	{
		var border = textBox.BorderThickness;
		var padding = textBox.Padding;
		return new PaddingF(
			(float)(border.Left + padding.Left),
			(float)(border.Top + padding.Top),
			(float)(border.Right + padding.Right),
			(float)(border.Bottom + padding.Bottom)
		);
	}

	static double GetInsertionX(muc.TextBox textBox, int index, int length)
	{
		if (index <= 0)
			return textBox.GetRectFromCharacterIndex(0, false).Left;
		if (index >= length)
			return textBox.GetRectFromCharacterIndex(length - 1, true).Right;
		return textBox.GetRectFromCharacterIndex(index, false).Left;
	}

	protected override wf.Size DefaultSize => new wf.Size(100, Control.MinHeight);

	protected override muc.TextBox CreateControl() => new EtoTextBox { Handler = this };

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextBox.TextChangedEvent:
				Control.TextChanged += Control_TextChanged;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	private void Control_TextChanged(object sender, muc.TextChangedEventArgs e)
	{
		Callback.OnTextChanged(Widget, EventArgs.Empty);
	}
}
