using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

public class CheckBoxCellHandler : CellHandler<CheckBoxCell, CheckBoxCell.ICallback>, CheckBoxCell.IHandler
{
	bool? GetValue(object? dataItem) => dataItem != null ? Widget.Binding?.GetValue(dataItem) : null;

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		return new muc.CheckBox
		{
			HorizontalAlignment = mux.HorizontalAlignment.Center,
			VerticalAlignment = mux.VerticalAlignment.Center,
			// the check box is toggled by OnCellClick, so the grid still selects the row
			IsHitTestVisible = false,
			IsTabStop = false,
			MinWidth = 0,
			Padding = new mux.Thickness(0),
		};
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		if (element is muc.CheckBox checkBox)
		{
			var value = GetValue(dataItem);
			checkBox.IsThreeState = value == null;
			checkBox.IsChecked = value;
		}
		FormatCell(element, cell, dataItem);
	}

	public override void OnCellClick(GridCellMouseEventArgs args, mux.DependencyObject? originalSource, ctk.DataGridCell cell)
	{
		// toggle with a single click, like other platforms
		if (!IsEditable || args.Item == null || args.Buttons != MouseButtons.Primary)
			return;
		var checkBox = (cell.Content as mux.FrameworkElement) is { } content ? FindChild<muc.CheckBox>(content) : null;
		if (checkBox == null || GridHandler == null)
			return;
		// only when clicking on the check box itself, not anywhere in the cell
		var bounds = checkBox.TransformToVisual(GridHandler.Control).TransformBounds(new wf.Rect(0, 0, checkBox.ActualWidth, checkBox.ActualHeight));
		if (!bounds.Contains(args.Location.ToWinUI()))
			return;
		var value = !(checkBox.IsChecked ?? false);
		Widget.Binding?.SetValue(args.Item, value);
		checkBox.IsThreeState = false;
		checkBox.IsChecked = value;
		CellEdited(checkBox);
		args.Handled = true;
	}
}
