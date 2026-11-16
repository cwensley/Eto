using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

static class CellDefaults
{
	// matches the padding of the DataGrid's own text column
	public static readonly mux.Thickness TextMargin = new mux.Thickness(12, 0, 12, 0);
	public static readonly mux.Thickness EditMargin = new mux.Thickness(0);
}

public class TextBoxCellHandler : CellHandler<TextBoxCell, TextBoxCell.ICallback>, TextBoxCell.IHandler
{
	TextAlignment _textAlignment;
	VerticalAlignment _verticalAlignment = VerticalAlignment.Center;

	public TextAlignment TextAlignment
	{
		get => _textAlignment;
		set
		{
			_textAlignment = value;
			Refresh();
		}
	}

	public VerticalAlignment VerticalAlignment
	{
		get => _verticalAlignment;
		set
		{
			_verticalAlignment = value;
			Refresh();
		}
	}

	public AutoSelectMode AutoSelectMode { get; set; } = AutoSelectMode.OnFocus;

	string? GetValue(object? dataItem) => dataItem != null ? Widget.Binding?.GetValue(dataItem) : null;

	public override bool SupportsEditing => true;

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		if (isEditing)
			return new muc.TextBox { Margin = CellDefaults.EditMargin };
		return new muc.TextBlock { Margin = CellDefaults.TextMargin, TextTrimming = mux.TextTrimming.CharacterEllipsis };
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		var text = GetValue(dataItem) ?? string.Empty;
		if (element is muc.TextBox textBox)
		{
			textBox.Text = text;
			textBox.TextAlignment = TextAlignment.ToWinUI();
			textBox.VerticalAlignment = VerticalAlignment.ToWinUI();
		}
		else if (element is muc.TextBlock textBlock)
		{
			textBlock.Text = text;
			textBlock.TextAlignment = TextAlignment.ToWinUI();
			textBlock.VerticalAlignment = VerticalAlignment.ToWinUI();
		}
		FormatCell(element, cell, dataItem);
	}

	public override object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs? args)
	{
		if (editingElement is muc.TextBox textBox)
		{
			var text = textBox.Text;
			textBox.Focus(mux.FocusState.Programmatic);
			if (AutoSelectMode == AutoSelectMode.Never)
				textBox.Select(text.Length, 0);
			else
				textBox.SelectAll();
			return text;
		}
		return null;
	}

	public override void CommitCellEdit(mux.FrameworkElement editingElement, object? dataItem)
	{
		if (editingElement is muc.TextBox textBox && dataItem != null)
			Widget.Binding?.SetValue(dataItem, textBox.Text);
	}
}
