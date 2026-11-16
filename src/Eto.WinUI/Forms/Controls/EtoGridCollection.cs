using Microsoft.UI.Xaml.Data;
using Windows.Foundation.Collections;

namespace Eto.WinUI.Forms.Controls;

/// <summary>
/// Passes a data store to the DataGrid as its collection view.
/// </summary>
/// <remarks>
/// The DataGrid wraps a plain list in a view that starts with its first item current, which it then selects, and
/// moves the current item as rows are removed, which resets the selection. Eto grids start with no selection and
/// keep it as the data changes, so this view starts before the first item and only moves when the grid asks it to.
///
/// It only listens weakly to the data store, so a long lived store doesn't keep the grid alive, and splits changes of
/// multiple items as the DataGrid only supports single item changes.
/// </remarks>
class EtoGridCollection : IList, ICollectionView, INotifyCollectionChanged
{
	readonly IList _collection;
	WeakEventListener? _listener;
	object? _currentItem;
	int _currentPosition = -1;

	public EtoGridCollection(IList collection)
	{
		_collection = collection;
		if (collection is INotifyCollectionChanged notify)
			_listener = WeakEventListener.Create(notify, this, static (EtoGridCollection c, object s, NotifyCollectionChangedEventArgs e) => c.OnCollectionChanged(e));
	}

	public IList Collection => _collection;

	public void Unregister()
	{
		_listener?.Detach();
		_listener = null;
	}

	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add when e.NewItems != null && e.NewItems.Count > 1 && e.NewStartingIndex >= 0:
				for (int i = 0; i < e.NewItems.Count; i++)
					RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems[i], e.NewStartingIndex + i));
				break;
			case NotifyCollectionChangedAction.Remove when e.OldItems != null && e.OldItems.Count > 1 && e.OldStartingIndex >= 0:
				for (int i = e.OldItems.Count - 1; i >= 0; i--)
					RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, e.OldItems[i], e.OldStartingIndex + i));
				break;
			case NotifyCollectionChangedAction.Add when e.NewStartingIndex < 0:
			case NotifyCollectionChangedAction.Remove when e.OldStartingIndex < 0:
			case NotifyCollectionChangedAction.Replace when (e.NewItems?.Count ?? 0) != 1 || e.NewStartingIndex < 0:
			case NotifyCollectionChangedAction.Move:
				RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				break;
			default:
				RaiseCollectionChanged(e);
				break;
		}
	}

	void RaiseCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		// keep the current item without telling the grid, otherwise it changes the selection to match
		if (_currentItem != null)
		{
			_currentPosition = _collection.IndexOf(_currentItem);
			if (_currentPosition < 0)
				_currentItem = null;
		}
		else
			_currentPosition = -1;
		CollectionChanged?.Invoke(this, e);
	}

	#region IList

	public object? this[int index]
	{
		get => _collection[index];
		set => _collection[index] = value;
	}

	public bool IsReadOnly => _collection.IsReadOnly;
	public bool IsFixedSize => _collection.IsFixedSize;
	public int Count => _collection.Count;
	public object SyncRoot => _collection.SyncRoot;
	public bool IsSynchronized => _collection.IsSynchronized;
	public int Add(object? value) => _collection.Add(value);
	public void Clear() => _collection.Clear();
	public bool Contains(object? value) => _collection.Contains(value);
	public void CopyTo(Array array, int index) => _collection.CopyTo(array, index);
	public IEnumerator GetEnumerator() => _collection.GetEnumerator();
	public int IndexOf(object? value) => _collection.IndexOf(value);
	public void Insert(int index, object? value) => _collection.Insert(index, value);
	public void Remove(object? value) => _collection.Remove(value);
	public void RemoveAt(int index) => _collection.RemoveAt(index);

	#endregion

	#region IList<object>

	object IList<object>.this[int index]
	{
		get => _collection[index]!;
		set => _collection[index] = value;
	}

	void ICollection<object>.Add(object item) => _collection.Add(item);
	bool ICollection<object>.Contains(object item) => _collection.Contains(item);
	void ICollection<object>.CopyTo(object[] array, int arrayIndex) => _collection.CopyTo(array, arrayIndex);
	bool ICollection<object>.Remove(object item)
	{
		var index = _collection.IndexOf(item);
		if (index < 0)
			return false;
		_collection.RemoveAt(index);
		return true;
	}
	int IList<object>.IndexOf(object item) => _collection.IndexOf(item);
	void IList<object>.Insert(int index, object item) => _collection.Insert(index, item);
	IEnumerator<object> IEnumerable<object>.GetEnumerator() => _collection.Cast<object>().GetEnumerator();

	#endregion

	#region ICollectionView

	// changes are raised through INotifyCollectionChanged, which the grid prefers
	event VectorChangedEventHandler<object>? IObservableVector<object>.VectorChanged
	{
		add { }
		remove { }
	}

	public event EventHandler<object>? CurrentChanged;
	public event CurrentChangingEventHandler? CurrentChanging;

	public IObservableVector<object>? CollectionGroups => null;
	public object? CurrentItem => _currentItem;
	public int CurrentPosition => _currentPosition;
	public bool HasMoreItems => false;
	public bool IsCurrentAfterLast => _currentPosition >= _collection.Count;
	public bool IsCurrentBeforeFirst => _currentPosition < 0;

	public wf.IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) => throw new NotSupportedException();

	public bool MoveCurrentTo(object item)
	{
		if (item != null && ReferenceEquals(item, _currentItem) && _currentPosition >= 0)
			return true;
		return MoveCurrentToPosition(item != null ? _collection.IndexOf(item) : -1);
	}

	public bool MoveCurrentToPosition(int index)
	{
		if (index < -1 || index > _collection.Count)
			return false;
		var changing = new CurrentChangingEventArgs(true);
		CurrentChanging?.Invoke(this, changing);
		if (changing.Cancel)
			return false;
		_currentPosition = index;
		_currentItem = index >= 0 && index < _collection.Count ? _collection[index] : null;
		CurrentChanged?.Invoke(this, null!);
		return _currentItem != null;
	}

	public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);
	public bool MoveCurrentToLast() => MoveCurrentToPosition(_collection.Count - 1);
	public bool MoveCurrentToNext() => _currentPosition < _collection.Count && MoveCurrentToPosition(_currentPosition + 1);
	public bool MoveCurrentToPrevious() => _currentPosition >= 0 && MoveCurrentToPosition(_currentPosition - 1);

	#endregion
}
