using Eto.CustomControls;
using Eto.WinUI.Forms.Cells;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Controls;

public class TreeGridViewHandler : GridHandler<TreeGridView, TreeGridView.ICallback>, TreeGridView.IHandler, ITreeHandler
{
	readonly TreeController _controller;
	EtoGridCollection? _collection;
	ITreeGridItem? _lastSelected;
	List<object>? _selectionBeforeReset;
	int _resetDepth;

	public TreeGridViewHandler()
	{
		_controller = new TreeController { Handler = this };
	}

	protected override object? GetItemAtRow(int row)
	{
		if (row < 0 || row >= _controller.Count)
			return null;
		return _controller[row];
	}

	protected override int GetRowOfItem(object? item) => item is ITreeGridItem treeItem ? _controller.IndexOf(treeItem) : -1;

	protected override int RowCount => _collection != null ? _controller.Count : 0;

	protected override void Initialize()
	{
		base.Initialize();
		Control.PreviewKeyDown += Control_PreviewKeyDown;
	}

	void Control_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (e.Handled || IsEditing)
			return;
		var modifiers = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
			| Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
			| Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
		if (modifiers.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
			return;

		// handle expanding/collapsing via the keyboard
		if (Control.SelectedItem is not ITreeGridItem item)
			return;
		if (e.Key == Windows.System.VirtualKey.Right)
		{
			if (item.Expandable && !item.Expanded)
			{
				var index = _controller.IndexOf(item);
				if (index >= 0)
				{
					_controller.ExpandRow(index);
					e.Handled = true;
				}
			}
		}
		else if (e.Key == Windows.System.VirtualKey.Left)
		{
			if (!item.Expandable || !item.Expanded)
			{
				// select parent if not the top node
				var parent = item.Parent;
				if (parent != null && !ReferenceEquals(parent, DataStore))
				{
					SelectedItem = parent;
					e.Handled = true;
				}
			}
			else
			{
				var index = _controller.IndexOf(item);
				if (index >= 0)
				{
					_controller.CollapseRow(index);
					e.Handled = true;
				}
			}
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TreeGridView.ActivatedEvent:
				Control.KeyDown += (sender, e) =>
				{
					if (e.Key == Windows.System.VirtualKey.Enter && !IsEditing && SelectedItem is ITreeGridItem item)
					{
						Callback.OnActivated(Widget, new TreeGridViewItemEventArgs(item));
						e.Handled = true;
					}
				};
				Control.DoubleTapped += (sender, e) =>
				{
					if (TreeTogglePanel.IsOverExpander(e.OriginalSource as mux.DependencyObject))
						return;
					if (SelectedItem is ITreeGridItem item)
					{
						Callback.OnActivated(Widget, new TreeGridViewItemEventArgs(item));
						e.Handled = true;
					}
				};
				break;
			case TreeGridView.ExpandingEvent:
				_controller.Expanding += (sender, e) => Callback.OnExpanding(Widget, e);
				break;
			case TreeGridView.ExpandedEvent:
				_controller.Expanded += (sender, e) => Callback.OnExpanded(Widget, e);
				break;
			case TreeGridView.CollapsingEvent:
				_controller.Collapsing += (sender, e) => Callback.OnCollapsing(Widget, e);
				break;
			case TreeGridView.CollapsedEvent:
				_controller.Collapsed += (sender, e) => Callback.OnCollapsed(Widget, e);
				break;
			case TreeGridView.SelectedItemChangedEvent:
				// raised from OnSelectionChanged
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	protected override void OnSelectionChanged()
	{
		base.OnSelectionChanged();
		var item = SelectedItem;
		if (!ReferenceEquals(_lastSelected, item))
		{
			_lastSelected = item;
			Callback.OnSelectedItemChanged(Widget, EventArgs.Empty);
		}
	}

	public ITreeGridStore<ITreeGridItem>? DataStore
	{
		get => _controller.Store;
		set
		{
			_collection?.Unregister();
			_collection = null;
			Control.ItemsSource = null;
			_controller.InitializeItems(value);
			if (value != null)
			{
				_collection = new EtoGridCollection(_controller);
				Control.ItemsSource = _collection;
				// subscribe after the grid so we see the rows after it has processed the change
				_collection.CollectionChanged += Collection_CollectionChanged;
			}
			EnsureSelection();
			UpdateLastSelected();
		}
	}

	void UpdateLastSelected()
	{
		var item = SelectedItem;
		if (!ReferenceEquals(_lastSelected, item))
		{
			_lastSelected = item;
			if (!SkipSelectionChanged)
				Callback.OnSelectedItemChanged(Widget, EventArgs.Empty);
		}
	}

	void Collection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RestoreSelectionAfterChange();
		DispatcherQueueUpdateToggles();
	}

	public ITreeGridItem? SelectedItem
	{
		get => Control.SelectedItem as ITreeGridItem;
		set
		{
			if (value != null && _collection != null)
			{
				_controller.ExpandToItem(value);
				var row = _controller.IndexOf(value);
				if (row >= 0)
				{
					if (AllowMultipleSelection)
						SelectedRows = new[] { row };
					else
						Control.SelectedItem = value;
					Control.ScrollIntoView(value, null);
				}
			}
			else
				UnselectAll();
		}
	}

	public IEnumerable<object> SelectedItems => Control.SelectedItems.Cast<object>().ToList();

	public override mux.FrameworkElement SetupCell(GridColumnHandler column, mux.FrameworkElement defaultContent, ctk.DataGridCell cell)
	{
		// only the first column shows the expander
		if (Widget.Columns.Count == 0 || !ReferenceEquals(column, Widget.Columns[0].Handler))
			return defaultContent;
		return new TreeTogglePanel(defaultContent, _controller);
	}

	bool ITreeHandler.PreResetTree(object? item, int row)
	{
		if (_resetDepth++ > 0)
			return true;
		// the grid clears its selection when the rows are reset, so restore it afterwards
		_selectionBeforeReset = Control.SelectedItems.Cast<object>().ToList();
		BeginSelectionUpdate();
		return true;
	}

	void ITreeHandler.PostResetTree(object? item, int row)
	{
		if (--_resetDepth > 0)
			return;
		var selection = _selectionBeforeReset ?? new List<object>();
		_selectionBeforeReset = null;
		try
		{
			var rows = selection.OfType<ITreeGridItem>().Select(r => _controller.IndexOf(r)).Where(r => r >= 0).ToList();
			if (AllowMultipleSelection)
			{
				Control.SelectedItems.Clear();
				foreach (var r in rows)
					Control.SelectedItems.Add(_controller[r]);
			}
			else
				Control.SelectedIndex = rows.Count > 0 ? rows[0] : -1;
		}
		finally
		{
			EndSelectionUpdate();
		}
		EnsureSelection();
	}

	public void ReloadData()
	{
		_controller.ReloadData();
		RefreshCells();
	}

	public void ReloadItem(ITreeGridItem item, bool reloadChildren)
	{
		if (!reloadChildren)
		{
			_controller.ReloadItem(item);
			RefreshCells();
		}
		else
			ReloadData();
	}

	public TreeGridCell GetCellAt(PointF location)
	{
		var info = GetCellInfo(location);
		return new TreeGridCell(info.Column, info.ColumnIndex, info.CellType, info.Item);
	}

	public TreeGridViewDragInfo? GetDragInfo(DragEventArgs args) => args.ControlObject as TreeGridViewDragInfo;

	bool _updateTogglesQueued;

	void DispatcherQueueUpdateToggles()
	{
		// rows that stay realized keep the same item, so update their expanders after the grid has processed the change
		if (_updateTogglesQueued)
			return;
		_updateTogglesQueued = true;
		Control.DispatcherQueue.TryEnqueue(() =>
		{
			_updateTogglesQueued = false;
			UpdateToggles();
		});
	}

	void UpdateToggles()
	{
		if (Control.Columns.FirstOrDefault() is not EtoDataGridColumn column)
			return;
		foreach (var row in LoadedRows)
		{
			if (column.GetCellContent(row) is TreeTogglePanel panel)
				panel.Update(row.DataContext);
		}
	}
}
