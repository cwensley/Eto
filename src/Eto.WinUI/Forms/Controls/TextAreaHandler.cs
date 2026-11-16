using CommunityToolkit.WinUI;

namespace Eto.WinUI.Forms.Controls;

public class EtoTextBox : muc.TextBox
{
	public IWinUIFrameworkElement? Handler { get; set; }

	protected override wf.Size MeasureOverride(wf.Size availableSize)
	{
		return Handler?.MeasureOverride(availableSize, base.MeasureOverride) ?? base.MeasureOverride(availableSize);
	}
}

public class TextAreaHandler : WinUIControl<muc.TextBox, TextArea, TextArea.ICallback>, TextArea.IHandler
{
	protected override sw.Size DefaultSize => new sw.Size(100, 60);

	protected override bool PreventUserResize => true;

	public bool ReadOnly
	{
		get => Control.IsReadOnly;
		set => Control.IsReadOnly = value;
	}
	public string Text
	{
		get => NormalizeText(Control.Text);
		set
		{
			Control.Text = value ?? string.Empty;
			Control.Select(Control.Text.Length, 0);
			CheckSelectionChanged();
		}
	}

	// TextBox separates lines with \r
	static string NormalizeText(string? text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r\n", "\n").Replace('\r', '\n');

	int _lastCaretIndex;
	Range<int> _lastSelection;

	void CheckSelectionChanged()
	{
		var selection = Selection;
		if (_lastSelection != selection)
		{
			_lastSelection = selection;
			Callback.OnSelectionChanged(Widget, EventArgs.Empty);
		}
		var caretIndex = CaretIndex;
		if (_lastCaretIndex != caretIndex)
		{
			_lastCaretIndex = caretIndex;
			Callback.OnCaretIndexChanged(Widget, EventArgs.Empty);
		}
	}
	public bool Wrap
	{
		get => Control.TextWrapping != mux.TextWrapping.NoWrap;
		set => Control.TextWrapping = value ? mux.TextWrapping.Wrap : mux.TextWrapping.NoWrap;
	}
	public string SelectedText
	{
		get => NormalizeText(Control.SelectedText);
		set
		{
			Control.SelectedText = value ?? string.Empty;
			CheckSelectionChanged();
		}
	}

	public Range<int> Selection
	{
		get => Eto.Forms.Range.FromLength(Control.SelectionStart, Control.SelectionLength);
		set
		{
			Control.Select(value.Start, value.Length());
			CheckSelectionChanged();
		}
	}
	public int CaretIndex
	{
		get => Control.SelectionStart;
		set
		{
			Control.Select(value, 0);
			CheckSelectionChanged();
		}
	}
	public bool AcceptsTab { get; set; }
	public bool AcceptsReturn
	{
		get => Control.AcceptsReturn;
		set => Control.AcceptsReturn = value;
	}
	public TextReplacements TextReplacements { get; set; }
	public TextReplacements SupportedTextReplacements { get; }
	public TextAlignment TextAlignment
	{
		get => Control.TextAlignment.ToEto();
		set => Control.TextAlignment = value.ToWinUI();
	}
	public bool SpellCheck
	{
		get => Control.IsSpellCheckEnabled;
		set => Control.IsSpellCheckEnabled = value;
	}
	public bool SpellCheckIsSupported => true;
	public BorderType Border { get; set; }
	public int TextLength => Control.Text.Length;

	protected override muc.TextBox CreateControl() => new EtoTextBox { Handler = this };

	protected override void Initialize()
	{
		base.Initialize();
		Control.AcceptsReturn = true;
		UserPreferredSize = DefaultSize;
		// TextChanging is raised synchronously, including for programmatic changes, unlike TextChanged
		Control.TextChanging += (sender, e) => Callback.OnTextChanged(Widget, EventArgs.Empty);
		// SelectionChanged is only raised asynchronously once loaded, so programmatic changes report themselves
		Control.SelectionChanged += (sender, e) => CheckSelectionChanged();
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextControl.TextChangedEvent:
			case TextArea.SelectionChangedEvent:
			case TextArea.CaretIndexChangedEvent:
				// always hooked up
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	public void Append(string text, bool scrollToCursor)
	{
		// insert at the end, as setting Text re-lays out all of it and scrolls back to the top
		// SelectedText is ignored while read only
		var readOnly = Control.IsReadOnly;
		Control.IsReadOnly = false;
		Control.Select(Control.Text.Length, 0);
		Control.SelectedText = text;
		Control.IsReadOnly = readOnly;
		Control.Select(Control.Text.Length, 0);
		CheckSelectionChanged();
		if (scrollToCursor)
		{
			ScrollToEnd();
		}
	}

	public void SelectAll()
	{
		Control.SelectAll();
		CheckSelectionChanged();
	}

	public void ScrollTo(Range<int> range)
	{
		var scrollViewer = ScrollViewer;
		var length = Control.Text.Length;
		if (scrollViewer == null || length == 0)
			return;

		// TextBox has no API to scroll to a position, so bring the character's bounds into the viewport
		var index = Math.Max(0, Math.Min(range.Start, length - 1));
		var rect = Control.GetRectFromCharacterIndex(index, range.Start >= length);
		var location = Control.TransformToVisual(scrollViewer).TransformPoint(new wf.Point(rect.X, rect.Y));

		double? horizontalOffset = null;
		if (location.X < 0)
			horizontalOffset = scrollViewer.HorizontalOffset + location.X;
		else if (location.X + rect.Width > scrollViewer.ViewportWidth)
			horizontalOffset = scrollViewer.HorizontalOffset + location.X + rect.Width - scrollViewer.ViewportWidth;

		double? verticalOffset = null;
		if (location.Y < 0)
			verticalOffset = scrollViewer.VerticalOffset + location.Y;
		else if (location.Y + rect.Height > scrollViewer.ViewportHeight)
			verticalOffset = scrollViewer.VerticalOffset + location.Y + rect.Height - scrollViewer.ViewportHeight;

		if (horizontalOffset != null || verticalOffset != null)
			scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, true);
	}

	muc.ScrollViewer? ScrollViewer => Control.FindDescendant<muc.ScrollViewer>();

	public void ScrollToStart()
	{
		ScrollViewer?.ChangeView(null, 0, null, true);
	}

	public void ScrollToEnd()
	{
		var scrollViewer = ScrollViewer;
		scrollViewer?.ChangeView(null, scrollViewer.ExtentHeight, null, true);
	}
}
