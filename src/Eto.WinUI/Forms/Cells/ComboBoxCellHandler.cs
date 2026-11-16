using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

public class ComboBoxCellHandler : CellHandler<ComboBoxCell, ComboBoxCell.ICallback>, ComboBoxCell.IHandler
{
	IEnumerable<object>? _dataStore;

	public IEnumerable<object>? DataStore
	{
		get => _dataStore;
		set
		{
			_dataStore = value;
			Refresh();
		}
	}

	string? GetKey(object? item) => item == null ? null : Widget.ComboKeyBinding?.GetValue(item) ?? Convert.ToString(item);

	string? GetText(object? item) => item == null ? null : Widget.ComboTextBinding?.GetValue(item) ?? Convert.ToString(item);

	string? GetValue(object? dataItem)
	{
		if (dataItem == null || Widget.Binding == null)
			return null;
		var value = Widget.Binding.GetValue(dataItem);
		return value != null ? Convert.ToString(value) : null;
	}

	object? FindItem(string? key) => key == null ? null : _dataStore?.FirstOrDefault(r => GetKey(r) == key);

	public override bool SupportsEditing => true;

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		if (isEditing)
			return new muc.ComboBox { HorizontalAlignment = mux.HorizontalAlignment.Stretch, VerticalAlignment = mux.VerticalAlignment.Center };
		return new muc.TextBlock { Margin = CellDefaults.TextMargin, VerticalAlignment = mux.VerticalAlignment.Center, TextTrimming = mux.TextTrimming.CharacterEllipsis };
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		var key = GetValue(dataItem);
		if (element is muc.ComboBox comboBox)
		{
			var items = _dataStore?.ToList() ?? new List<object>();
			comboBox.ItemsSource = items.Select(r => new ComboItem(r, GetText(r))).ToList();
			var index = items.FindIndex(r => GetKey(r) == key);
			comboBox.SelectedIndex = index;
		}
		else if (element is muc.TextBlock textBlock)
		{
			var item = FindItem(key);
			textBlock.Text = (item != null ? GetText(item) : key) ?? string.Empty;
		}
		FormatCell(element, cell, dataItem);
	}

	public override object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs? args)
	{
		if (editingElement is muc.ComboBox comboBox)
		{
			comboBox.Focus(mux.FocusState.Programmatic);
			return comboBox.SelectedIndex;
		}
		return null;
	}

	public override void CommitCellEdit(mux.FrameworkElement editingElement, object? dataItem)
	{
		if (editingElement is muc.ComboBox comboBox && dataItem != null && comboBox.SelectedItem is ComboItem item)
			Widget.Binding?.SetValue(dataItem, GetKey(item.Item));
	}

	class ComboItem
	{
		public object Item { get; }
		public string Text { get; }

		public ComboItem(object item, string? text)
		{
			Item = item;
			Text = text ?? string.Empty;
		}

		public override string ToString() => Text;
	}
}
