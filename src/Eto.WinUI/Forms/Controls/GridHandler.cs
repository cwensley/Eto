using Eto.WinUI.Forms.Cells;
using Microsoft.UI.Xaml.Input;
using muxi = Microsoft.UI.Xaml.Input;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Controls;

public interface IGridHandler
{
	ctk.DataGrid Control { get; }
	Grid Widget { get; }
	bool Loaded { get; }
	mux.FrameworkElement SetupCell(GridColumnHandler column, mux.FrameworkElement defaultContent, ctk.DataGridCell cell);
	void FormatCell(GridColumnHandler column, ICellHandler cell, mux.FrameworkElement element, ctk.DataGridCell gridCell, object? dataItem);
	void CellEdited(GridColumnHandler column, mux.FrameworkElement element);
	void RefreshColumn(GridColumnHandler column);
	bool IsEditingElement(mux.FrameworkElement element);
	bool IsRowSelected(object? item);
}

static class GridExtensions
{
	public static mux.HorizontalAlignment ToWinUIHorizontal(this TextAlignment value) => value switch
	{
		TextAlignment.Center => mux.HorizontalAlignment.Center,
		TextAlignment.Right => mux.HorizontalAlignment.Right,
		_ => mux.HorizontalAlignment.Left
	};

	public static IEnumerable<T> FindVisualChildren<T>(this mux.DependencyObject element)
		where T : mux.DependencyObject
	{
		var count = muxm.VisualTreeHelper.GetChildrenCount(element);
		for (int i = 0; i < count; i++)
		{
			var child = muxm.VisualTreeHelper.GetChild(element, i);
			if (child is T found)
				yield return found;
			foreach (var descendant in child.FindVisualChildren<T>())
				yield return descendant;
		}
	}

	public static Windows.UI.Text.FontWeight ToWinUIFontWeight(this Font font) =>
		font.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
}

// The DataGrid is wrapped rather than subclassed, as XAML can't apply the DataGrid's style to a subclass it has no metadata for.
public abstract class GridHandler<TWidget, TCallback> : WinUIFrameworkElement<ctk.DataGrid, TWidget, TCallback>, Grid.IHandler, IGridHandler
	where TWidget : Grid
	where TCallback : Grid.ICallback
{
	readonly HashSet<ctk.DataGridRow> _loadedRows = new();
	mux.FrameworkElement? _editingElement;
	bool _isEditing;
	bool _allowEmptySelection = true;
	Dictionary<GridColumn, double>? _columnWidths;

	protected bool SkipSelectionChanged { get; set; }

	protected override sw.Size DefaultSize => new sw.Size(100, 100);

	readonly EtoContentControl _container = new();

	public override mux.FrameworkElement ContainerControl => _container;

	public override mux.FrameworkElement FocusControl => Control;

	public override bool Enabled
	{
		get => Control.IsEnabled;
		set => Control.IsEnabled = value;
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override bool HasFocus
	{
		get
		{
			if (Control.XamlRoot == null)
				return false;
			var focused = muxi.FocusManager.GetFocusedElement(Control.XamlRoot) as mux.DependencyObject;
			while (focused != null)
			{
				if (ReferenceEquals(focused, Control))
					return true;
				focused = muxm.VisualTreeHelper.GetParent(focused);
			}
			return false;
		}
	}

	public override void Focus() => Control.Focus(mux.FocusState.Programmatic);

	protected override ctk.DataGrid CreateControl() => new ctk.DataGrid
	{
		HeadersVisibility = ctk.DataGridHeadersVisibility.Column,
		AutoGenerateColumns = false,
		CanUserReorderColumns = false,
		CanUserSortColumns = true,
		SelectionMode = ctk.DataGridSelectionMode.Single,
		GridLinesVisibility = ctk.DataGridGridLinesVisibility.None,
		IsReadOnly = false,
	};

	protected ColumnCollection Columns { get; private set; } = null!;

	protected abstract object? GetItemAtRow(int row);

	protected abstract int GetRowOfItem(object? item);

	protected abstract int RowCount { get; }

	protected IList? ItemsList => Control.ItemsSource as IList;

	protected override void Initialize()
	{
		base.Initialize();
		_container.Handler = this;
		_container.Content = Control;
		Columns = new ColumnCollection { Handler = this };
		Columns.Register(Widget.Columns);

		Control.Loaded += Control_Loaded;
		Control.LoadingRow += Control_LoadingRow;
		Control.UnloadingRow += Control_UnloadingRow;
		Control.PreparingCellForEdit += Control_PreparingCellForEdit;
		Control.CellEditEnding += Control_CellEditEnding;
		Control.CellEditEnded += Control_CellEditEnded;
		Control.SelectionChanged += Control_SelectionChanged;
		// the grid handles pointer presses itself, so we need to see handled events too
		Control.AddHandler(mux.UIElement.PointerReleasedEvent, new PointerEventHandler(Control_PointerReleased), true);
	}

	void Control_Loaded(object sender, mux.RoutedEventArgs e)
	{
		// expanded columns don't get auto sized, so we flip to star width after they are auto sized.
		foreach (var col in Widget.Columns)
		{
			if (col.Handler is GridColumnHandler columnHandler)
				columnHandler.OnLoad();
		}
	}

	void Control_LoadingRow(object? sender, ctk.DataGridRowEventArgs e)
	{
		var row = e.Row;
		_loadedRows.Add(row);
		if (IsEventHandled(Grid.RowFormattingEvent))
		{
			row.ClearValue(muc.Control.BackgroundProperty);
			Callback.OnRowFormatting(Widget, new RowFormatEventArgs(row, row.DataContext, row.GetIndex()));
		}
	}

	void Control_UnloadingRow(object? sender, ctk.DataGridRowEventArgs e)
	{
		_loadedRows.Remove(e.Row);
	}

	protected IEnumerable<ctk.DataGridRow> LoadedRows => _loadedRows;

	void Control_PreparingCellForEdit(object? sender, ctk.DataGridPreparingCellForEditEventArgs e)
	{
		_editingElement = EtoDataGridColumn.GetInnerElement(e.EditingElement);
		_isEditing = true;
		if (IsEventHandled(Grid.CellEditingEvent))
		{
			var row = e.Row.GetIndex();
			var column = GetColumn(e.Column);
			Callback.OnCellEditing(Widget, new GridViewCellEventArgs(column, row, column != null ? Widget.Columns.IndexOf(column) : -1, e.Row.DataContext));
		}
	}

	void Control_CellEditEnding(object? sender, ctk.DataGridCellEditEndingEventArgs e)
	{
		if (e.EditAction == ctk.DataGridEditAction.Commit && e.Column is EtoDataGridColumn column && e.EditingElement != null)
			column.CommitCellEdit(e.EditingElement);
	}

	void Control_CellEditEnded(object? sender, ctk.DataGridCellEditEndedEventArgs e)
	{
		_editingElement = null;
		_isEditing = false;
		if (e.EditAction == ctk.DataGridEditAction.Commit)
			RaiseCellEdited(GetColumn(e.Column), e.Row.GetIndex(), e.Row.DataContext);
	}

	void Control_SelectionChanged(object sender, muc.SelectionChangedEventArgs e)
	{
		if (!_allowEmptySelection && Control.SelectedItems.Count == 0 && RowCount > 0 && e.RemovedItems.Count > 0)
		{
			// don't let the user clear the selection
			Control.SelectedItem = e.RemovedItems[0];
			return;
		}
		CheckSelectionChanged();
	}

	List<object> _selection = new();
	int _selectionUpdating;

	/// <summary>
	/// Raises SelectionChanged if the selection differs from when it was last raised.
	/// </summary>
	/// <remarks>
	/// The DataGrid doesn't reliably raise its own event (e.g. for programmatic changes before it is loaded),
	/// so we compare after anything that could change it.
	/// </remarks>
	protected void CheckSelectionChanged()
	{
		if (_selectionUpdating > 0)
			return;
		var current = Control.SelectedItems.Cast<object>().ToList();
		if (current.Count == _selection.Count && new HashSet<object>(current).SetEquals(_selection))
			return;
		_selection = current;
		RefreshSelectedState();
		if (!SkipSelectionChanged)
			OnSelectionChanged();
	}

	/// <summary>
	/// Keeps the selected items that are still in the data store after it changed.
	/// </summary>
	/// <remarks>
	/// The DataGrid resets its current cell when rows before it are removed, which can drop other selected rows.
	/// </remarks>
	protected void RestoreSelectionAfterChange()
	{
		if (_selectionUpdating > 0)
			return;
		var expected = _selection.Where(r => GetRowOfItem(r) >= 0).ToList();
		var current = Control.SelectedItems.Cast<object>().ToList();
		if (current.Count != expected.Count || !new HashSet<object>(current).SetEquals(expected))
		{
			SetSelection(() =>
			{
				if (AllowMultipleSelection)
				{
					foreach (var item in current.Where(r => !expected.Contains(r)))
						Control.SelectedItems.Remove(item);
					foreach (var item in expected.Where(r => !current.Contains(r)))
						Control.SelectedItems.Add(item);
				}
				else
					Control.SelectedItem = expected.FirstOrDefault();
			});
		}
		else
			CheckSelectionChanged();
	}

	// raise a single selection changed event for a programmatic change of multiple rows
	protected void SetSelection(Action action)
	{
		BeginSelectionUpdate();
		try
		{
			action();
		}
		finally
		{
			EndSelectionUpdate();
		}
	}

	protected void BeginSelectionUpdate() => _selectionUpdating++;

	protected void EndSelectionUpdate()
	{
		_selectionUpdating--;
		CheckSelectionChanged();
	}

	protected virtual void OnSelectionChanged()
	{
		Callback.OnSelectionChanged(Widget, EventArgs.Empty);
	}

	void RefreshSelectedState()
	{
		// cells that draw themselves need to know when the selection changes
		foreach (var column in Widget.Columns)
		{
			if (column.Handler is GridColumnHandler columnHandler && columnHandler.DataCellHandler is ISelectionAwareCellHandler)
			{
				foreach (var row in _loadedRows)
					columnHandler.Control.RefreshCell(row);
			}
		}
	}

	public bool IsRowSelected(object? item) => item != null && Control.SelectedItems.Contains(item);

	public bool IsEditingElement(mux.FrameworkElement element) => ReferenceEquals(element, _editingElement);

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Grid.ColumnHeaderClickEvent:
				Control.Sorting += (sender, e) =>
				{
					var column = GetColumn(e.Column);
					if (column != null)
						Callback.OnColumnHeaderClick(Widget, new GridColumnEventArgs(column));
				};
				break;
			case Grid.CellEditingEvent:
			case Grid.CellEditedEvent:
			case Grid.CellFormattingEvent:
			case Grid.RowFormattingEvent:
			case Grid.SelectionChangedEvent:
				// handled in the native event handlers
				break;
			case Grid.CellClickEvent:
				Control.AddHandler(mux.UIElement.PointerPressedEvent, new PointerEventHandler(Control_PointerPressedCellClick), true);
				break;
			case Grid.CellDoubleClickEvent:
				Control.AddHandler(mux.UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(Control_DoubleTapped), true);
				break;
			case Grid.ColumnOrderChangedEvent:
				Control.ColumnReordered += (sender, e) =>
				{
					var column = GetColumn(e.Column);
					if (column != null)
						Callback.OnColumnOrderChanged(Widget, new GridColumnEventArgs(column));
				};
				break;
			case Grid.ColumnWidthChangedEvent:
				Control.LayoutUpdated += Control_LayoutUpdated;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void Control_LayoutUpdated(object? sender, object e)
	{
		// the columns don't notify when their width changes, so check after each layout
		_columnWidths ??= new Dictionary<GridColumn, double>();
		foreach (var column in Widget.Columns)
		{
			if (column.Handler is not GridColumnHandler columnHandler)
				continue;
			var width = columnHandler.Control.ActualWidth;
			if (_columnWidths.TryGetValue(column, out var oldWidth) && oldWidth != width)
			{
				_columnWidths[column] = width;
				Callback.OnColumnWidthChanged(Widget, new GridColumnEventArgs(column));
			}
			else
				_columnWidths[column] = width;
		}
	}

	void Control_PointerPressedCellClick(object sender, PointerRoutedEventArgs e)
	{
		var args = CreateCellMouseArgs(e.OriginalSource as mux.DependencyObject, e.GetCurrentPoint(Control), e.KeyModifiers, out var isValid);
		if (!isValid || TreeTogglePanel.IsOverExpander(e.OriginalSource as mux.DependencyObject))
			return;
		Callback.OnCellClick(Widget, args);
		if (args.Handled)
			e.Handled = true;
	}

	void Control_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		var source = e.OriginalSource as mux.DependencyObject;
		if (TreeTogglePanel.IsOverExpander(source))
			return;
		var row = GetRowOfElement(source, out var dataGridColumn, out var isValid);
		if (!isValid)
			return;
		var column = GetColumn(dataGridColumn);
		var location = e.GetPosition(Control).ToEto();
		var args = new GridCellMouseEventArgs(column, row?.GetIndex() ?? -1, column != null ? Widget.Columns.IndexOf(column) : -1, row?.DataContext, MouseButtons.Primary, Keys.None, location);
		Callback.OnCellDoubleClick(Widget, args);
		if (args.Handled)
			e.Handled = true;
	}

	void Control_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		var source = e.OriginalSource as mux.DependencyObject;
		var cell = source?.FindVisualParent<ctk.DataGridCell>();
		if (cell == null || _isEditing || TreeTogglePanel.IsOverExpander(source))
			return;

		var point = e.GetCurrentPoint(Control);
		if (point.Properties.PointerUpdateKind != Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
			return;
		var args = CreateCellMouseArgs(source, point, e.KeyModifiers, out var isValid);
		if (!isValid || args.GridColumn?.Handler is not GridColumnHandler columnHandler)
			return;

		// let cells like the check box toggle with a single click
		columnHandler.DataCellHandler?.OnCellClick(args, source, cell);
	}

	GridCellMouseEventArgs CreateCellMouseArgs(mux.DependencyObject? source, Microsoft.UI.Input.PointerPoint point, Windows.System.VirtualKeyModifiers modifiers, out bool isValid)
	{
		var row = GetRowOfElement(source, out var dataGridColumn, out isValid);
		var column = GetColumn(dataGridColumn);
		var columnIndex = column != null ? Widget.Columns.IndexOf(column) : -1;
		var buttons = MouseButtons.None;
		if (point.Properties.IsLeftButtonPressed || point.Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased)
			buttons |= MouseButtons.Primary;
		if (point.Properties.IsRightButtonPressed || point.Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased)
			buttons |= MouseButtons.Alternate;
		if (point.Properties.IsMiddleButtonPressed || point.Properties.PointerUpdateKind == Microsoft.UI.Input.PointerUpdateKind.MiddleButtonReleased)
			buttons |= MouseButtons.Middle;
		var keys = Keys.None;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
			keys |= Keys.Control;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift))
			keys |= Keys.Shift;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Menu))
			keys |= Keys.Alt;
		return new GridCellMouseEventArgs(column, row?.GetIndex() ?? -1, columnIndex, row?.DataContext, buttons, keys, point.Position.ToEto());
	}

	ctk.DataGridRow? GetRowOfElement(mux.DependencyObject? source, out ctk.DataGridColumn? column, out bool isValid)
	{
		column = null;
		var dep = source;
		while (dep != null)
		{
			if (dep is ctk.DataGridCell cell)
			{
				column = cell.Content is mux.FrameworkElement content ? ctk.DataGridColumn.GetColumnContainingElement(content) : null;
				break;
			}
			if (dep is ctk.Primitives.DataGridColumnHeader header)
			{
				column = GetHeaderColumn(header);
				isValid = false;
				return null;
			}
			if (ReferenceEquals(dep, Control))
			{
				// clicked in the grid, but not on a row or cell
				isValid = true;
				return null;
			}
			if (dep is muc.Primitives.ScrollBar)
			{
				isValid = false;
				return null;
			}
			dep = muxm.VisualTreeHelper.GetParent(dep);
		}

		while (dep != null && dep is not ctk.DataGridRow)
			dep = muxm.VisualTreeHelper.GetParent(dep);

		isValid = dep != null;
		return dep as ctk.DataGridRow;
	}

	ctk.DataGridColumn? GetHeaderColumn(ctk.Primitives.DataGridColumnHeader header)
	{
		// headers are kept in column order, followed by a filler header
		if (muxm.VisualTreeHelper.GetParent(header) is muc.Panel panel)
		{
			var index = panel.Children.IndexOf(header);
			if (index >= 0 && index < Control.Columns.Count)
				return Control.Columns[index];
		}
		return null;
	}

	protected GridColumn? GetColumn(ctk.DataGridColumn? dataGridColumn)
	{
		if (dataGridColumn is EtoDataGridColumn column)
			return column.Handler.Widget;
		return null;
	}

	public bool ShowHeader
	{
		get => Control.HeadersVisibility.HasFlag(ctk.DataGridHeadersVisibility.Column);
		set => Control.HeadersVisibility = value ? ctk.DataGridHeadersVisibility.Column : ctk.DataGridHeadersVisibility.None;
	}

	public bool AllowColumnReordering
	{
		get => Control.CanUserReorderColumns;
		set => Control.CanUserReorderColumns = value;
	}

	protected class ColumnCollection : EnumerableChangedHandler<GridColumn, GridColumnCollection>
	{
		public GridHandler<TWidget, TCallback> Handler { get; set; } = null!;

		public override void AddItem(GridColumn item)
		{
			var colhandler = (GridColumnHandler)item.Handler;
			colhandler.Setup(Handler);
			Handler.Control.Columns.Add(colhandler.Control);
		}

		public override void InsertItem(int index, GridColumn item)
		{
			var colhandler = (GridColumnHandler)item.Handler;
			colhandler.Setup(Handler);
			Handler.Control.Columns.Insert(index, colhandler.Control);
		}

		public override void RemoveItem(int index)
		{
			if (Handler.Control.Columns[index] is EtoDataGridColumn column)
				column.Handler.Setup(null);
			Handler.Control.Columns.RemoveAt(index);
		}

		public override void RemoveAllItems()
		{
			foreach (var column in Handler.Control.Columns.OfType<EtoDataGridColumn>())
				column.Handler.Setup(null);
			Handler.Control.Columns.Clear();
		}
	}

	public bool AllowMultipleSelection
	{
		get => Control.SelectionMode == ctk.DataGridSelectionMode.Extended;
		set => Control.SelectionMode = value ? ctk.DataGridSelectionMode.Extended : ctk.DataGridSelectionMode.Single;
	}

	public IEnumerable<int> SelectedRows
	{
		get
		{
			var rows = new List<int>();
			foreach (var item in Control.SelectedItems)
			{
				var row = GetRowOfItem(item);
				if (row >= 0)
					rows.Add(row);
			}
			return rows;
		}
		set
		{
			var rows = value?.Where(r => r >= 0 && r < RowCount).ToList();
			if (rows == null || rows.Count == 0)
			{
				UnselectAll();
				return;
			}
			SetSelection(() =>
			{
				if (AllowMultipleSelection)
				{
					// only change what's different, clearing first can lose rows when the grid re-selects its current row
					var items = rows.Select(GetItemAtRow).Where(r => r != null).Cast<object>().ToList();
					var current = Control.SelectedItems.Cast<object>().ToList();
					foreach (var item in current.Where(r => !items.Contains(r)))
						Control.SelectedItems.Remove(item);
					foreach (var item in items.Where(r => !current.Contains(r)))
						Control.SelectedItems.Add(item);
				}
				else
					Control.SelectedIndex = rows[0];
			});
		}
	}

	public int RowHeight
	{
		get => double.IsNaN(Control.RowHeight) ? -1 : (int)Control.RowHeight;
		set => Control.RowHeight = value <= -1 ? double.NaN : value;
	}

	public void SelectAll()
	{
		if (!AllowMultipleSelection)
			return;
		SetSelection(() =>
		{
			for (int i = 0; i < RowCount; i++)
			{
				var item = GetItemAtRow(i);
				if (item != null && !Control.SelectedItems.Contains(item))
					Control.SelectedItems.Add(item);
			}
		});
	}

	public void SelectRow(int row)
	{
		if (row < 0 || row >= RowCount)
			return;
		SetSelection(() =>
		{
			if (AllowMultipleSelection)
			{
				var item = GetItemAtRow(row);
				if (item != null && !Control.SelectedItems.Contains(item))
					Control.SelectedItems.Add(item);
			}
			else
				Control.SelectedIndex = row;
		});
	}

	public void UnselectRow(int row)
	{
		if (row < 0 || row >= RowCount)
			return;
		SetSelection(() =>
		{
			if (AllowMultipleSelection)
				Control.SelectedItems.Remove(GetItemAtRow(row));
			else if (Control.SelectedIndex == row)
				Control.SelectedItem = null;
		});
	}

	public void UnselectAll()
	{
		var allowEmpty = _allowEmptySelection;
		_allowEmptySelection = true;
		try
		{
			SetSelection(() =>
			{
				if (Control.SelectedItems.Count == 0)
					return;
				if (AllowMultipleSelection)
					Control.SelectedItems.Clear();
				else
					Control.SelectedItem = null;
			});
		}
		finally
		{
			_allowEmptySelection = allowEmpty;
		}
	}

	public void BeginEdit(int row, int column)
	{
		CommitEdit();
		if (row < 0 || row >= RowCount || column < 0 || column >= Control.Columns.Count)
			return;
		var item = GetItemAtRow(row);
		var dataGridColumn = Control.Columns[column];
		if (!SelectedRows.Contains(row))
		{
			UnselectAll();
			SelectRow(row);
		}
		Control.ScrollIntoView(item, dataGridColumn);
		Control.CurrentColumn = dataGridColumn;
		Control.Focus(mux.FocusState.Programmatic);
		Control.BeginEdit();
	}

	public bool CommitEdit() => !_isEditing || Control.CommitEdit(ctk.DataGridEditingUnit.Row, true);

	public bool CancelEdit() => !_isEditing || Control.CancelEdit(ctk.DataGridEditingUnit.Row);

	public bool IsEditing => _isEditing;

	public virtual mux.FrameworkElement SetupCell(GridColumnHandler column, mux.FrameworkElement defaultContent, ctk.DataGridCell cell)
	{
		return defaultContent;
	}

	public virtual void FormatCell(GridColumnHandler column, ICellHandler cell, mux.FrameworkElement element, ctk.DataGridCell gridCell, object? dataItem)
	{
		if (!IsEventHandled(Grid.CellFormattingEvent) || dataItem == null)
			return;
		// we always reset values so recycled cells don't keep formatting from other items
		gridCell.ClearValue(muc.Control.ForegroundProperty);
		gridCell.ClearValue(muc.Control.BackgroundProperty);
		gridCell.ClearValue(muc.Control.FontFamilyProperty);
		gridCell.ClearValue(muc.Control.FontStyleProperty);
		gridCell.ClearValue(muc.Control.FontWeightProperty);
		gridCell.ClearValue(muc.Control.FontSizeProperty);
		Callback.OnCellFormatting(Widget, new CellFormatEventArgs(column.Widget, gridCell, dataItem, GetRowOfItem(dataItem)));
	}

	public void CellEdited(GridColumnHandler column, mux.FrameworkElement element)
	{
		var row = element.FindVisualParent<ctk.DataGridRow>();
		var item = element.DataContext;
		RaiseCellEdited(column.Widget, row?.GetIndex() ?? GetRowOfItem(item), item);
	}

	void RaiseCellEdited(GridColumn? column, int row, object? item)
	{
		var columnIndex = column != null ? Widget.Columns.IndexOf(column) : -1;
		Callback.OnCellEdited(Widget, new GridViewCellEventArgs(column, row, columnIndex, item));
	}

	public void RefreshColumn(GridColumnHandler column)
	{
		foreach (var row in _loadedRows.ToList())
			column.Control.RefreshCell(row);
	}

	/// <summary>Updates the content of all realized cells from the data store.</summary>
	protected void RefreshCells()
	{
		foreach (var row in _loadedRows.ToList())
		{
			if (IsEventHandled(Grid.RowFormattingEvent))
			{
				row.ClearValue(muc.Control.BackgroundProperty);
				Callback.OnRowFormatting(Widget, new RowFormatEventArgs(row, row.DataContext, row.GetIndex()));
			}
			foreach (var column in Control.Columns.OfType<EtoDataGridColumn>())
				column.RefreshCell(row);
		}
	}

	class CellFormatEventArgs : GridCellFormatEventArgs
	{
		readonly ctk.DataGridCell _cell;
		Font? _font;

		public CellFormatEventArgs(GridColumn column, ctk.DataGridCell cell, object item, int row)
			: base(column, item, row)
		{
			_cell = cell;
		}

		public override Font Font
		{
			get => _font!;
			set
			{
				_font = value;
				if (value == null)
				{
					_cell.ClearValue(muc.Control.FontFamilyProperty);
					_cell.ClearValue(muc.Control.FontSizeProperty);
					_cell.ClearValue(muc.Control.FontWeightProperty);
					_cell.ClearValue(muc.Control.FontStyleProperty);
					return;
				}
				_cell.FontFamily = new muxm.FontFamily(value.FamilyName);
				// Eto font sizes are in points
				_cell.FontSize = value.Size * 96.0 / 72.0;
				_cell.FontWeight = value.ToWinUIFontWeight();
				_cell.FontStyle = value.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
			}
		}

		public override Color BackgroundColor
		{
			get => _cell.Background.ToEtoColor();
			set => _cell.Background = value.ToWinUIBrush();
		}

		public override Color ForegroundColor
		{
			get => _cell.Foreground.ToEtoColor();
			set => _cell.Foreground = value.ToWinUIBrush();
		}
	}

	class RowFormatEventArgs : GridRowFormatEventArgs
	{
		readonly ctk.DataGridRow _row;

		public RowFormatEventArgs(ctk.DataGridRow row, object item, int index)
			: base(item, index)
		{
			_row = row;
		}

		public override Color BackgroundColor
		{
			get => _row.Background.ToEtoColor();
			set => _row.Background = value.ToWinUIBrush();
		}
	}

	public void ScrollToRow(int row)
	{
		if (row < 0 || row >= RowCount)
			return;
		Control.ScrollIntoView(GetItemAtRow(row), null);
	}

	public GridLines GridLines
	{
		get => Control.GridLinesVisibility switch
		{
			ctk.DataGridGridLinesVisibility.All => GridLines.Both,
			ctk.DataGridGridLinesVisibility.Horizontal => GridLines.Horizontal,
			ctk.DataGridGridLinesVisibility.Vertical => GridLines.Vertical,
			_ => GridLines.None
		};
		set => Control.GridLinesVisibility = value switch
		{
			GridLines.Both => ctk.DataGridGridLinesVisibility.All,
			GridLines.Horizontal => ctk.DataGridGridLinesVisibility.Horizontal,
			GridLines.Vertical => ctk.DataGridGridLinesVisibility.Vertical,
			_ => ctk.DataGridGridLinesVisibility.None
		};
	}

	static readonly object Border_Key = new object();

	public BorderType Border
	{
		get => Widget.Properties.Get(Border_Key, BorderType.Bezel);
		set
		{
			Widget.Properties.Set(Border_Key, value, BorderType.Bezel);
			Control.BorderThickness = value == BorderType.None ? new mux.Thickness(0) : new mux.Thickness(1);
		}
	}

	public bool AllowEmptySelection
	{
		get => _allowEmptySelection;
		set
		{
			_allowEmptySelection = value;
			if (!value)
				EnsureSelection();
		}
	}

	protected void EnsureSelection()
	{
		if (!_allowEmptySelection && Control.SelectedItems.Count == 0 && RowCount > 0)
			SelectRow(0);
	}

	bool IGridHandler.Loaded => Widget.Loaded;

	Grid IGridHandler.Widget => Widget;

	public override void Invalidate(bool invalidateChildren)
	{
		RefreshCells();
		base.Invalidate(invalidateChildren);
	}

	public override void Invalidate(Rectangle rect, bool invalidateChildren)
	{
		RefreshCells();
		base.Invalidate(rect, invalidateChildren);
	}

	protected (GridColumn? Column, int ColumnIndex, int RowIndex, GridCellType CellType, object? Item) GetCellInfo(PointF location)
	{
		var point = Control.TransformToVisual(null).TransformPoint(location.ToWinUI());
		var elements = muxm.VisualTreeHelper.FindElementsInHostCoordinates(point, Control, true);
		foreach (var element in elements)
		{
			if (element is ctk.Primitives.DataGridColumnHeader header)
			{
				var headerColumn = GetColumn(GetHeaderColumn(header));
				var headerIndex = headerColumn != null ? Widget.Columns.IndexOf(headerColumn) : -1;
				return (headerColumn, headerIndex, -1, GridCellType.ColumnHeader, null);
			}
			if (element is ctk.DataGridCell cell)
			{
				var dataGridColumn = cell.Content is mux.FrameworkElement content ? ctk.DataGridColumn.GetColumnContainingElement(content) : null;
				var column = GetColumn(dataGridColumn);
				var columnIndex = column != null ? Widget.Columns.IndexOf(column) : -1;
				var row = cell.FindVisualParent<ctk.DataGridRow>();
				var rowIndex = row?.GetIndex() ?? -1;
				var item = row?.DataContext;
				var cellType = rowIndex >= 0 && columnIndex >= 0 ? GridCellType.Data : GridCellType.None;
				return (column, columnIndex, rowIndex, cellType, item);
			}
		}
		return (null, -1, -1, GridCellType.None, null);
	}
}
