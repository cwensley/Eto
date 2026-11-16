namespace Eto.WinUI.Forms.Controls;

public class GridViewHandler : GridHandler<GridView, GridView.ICallback>, GridView.IHandler
{
	IEnumerable<object>? _store;
	IList? _list;

	protected override object? GetItemAtRow(int row)
	{
		if (_list == null || row < 0 || row >= _list.Count)
			return null;
		return _list[row];
	}

	protected override int GetRowOfItem(object? item) => _list?.IndexOf(item) ?? -1;

	protected override int RowCount => _list?.Count ?? 0;

	public IEnumerable<object>? DataStore
	{
		get => _store;
		set
		{
			if (Control.ItemsSource is EtoGridCollection old)
				old.Unregister();
			if (_list is INotifyCollectionChanged oldList)
				oldList.CollectionChanged -= List_CollectionChanged;

			_store = value;
			if (_store == null)
			{
				_list = null;
				Control.ItemsSource = null;
				CheckSelectionChanged();
				return;
			}
			// must use an observable list so the grid updates when the collection changes
			if (_store is IList list && _store is INotifyCollectionChanged)
				_list = new EtoGridCollection(list);
			else
				_list = new EtoGridCollection(new ObservableCollection<object>(_store));
			Control.ItemsSource = _list;
			// subscribe after the grid so we see the selection after it has processed the change
			if (_list is INotifyCollectionChanged newList)
				newList.CollectionChanged += List_CollectionChanged;
			CheckSelectionChanged();
			EnsureSelection();
		}
	}

	void List_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RestoreSelectionAfterChange();

	public IEnumerable<object> SelectedItems => Control.SelectedItems.Cast<object>().ToList();

	public void ReloadData(IEnumerable<int> rows)
	{
		SkipSelectionChanged = true;
		try
		{
			RefreshCells();
		}
		finally
		{
			SkipSelectionChanged = false;
		}
	}

	public GridCell GetCellAt(PointF location)
	{
		var info = GetCellInfo(location);
		return new GridCell(info.Column, info.ColumnIndex, info.RowIndex, info.CellType, info.Item);
	}

	public GridViewDragInfo? GetDragInfo(DragEventArgs args) => args.ControlObject as GridViewDragInfo;
}
