using Eto.WinUI.Forms.Controls;
using muxi = Microsoft.UI.Xaml.Input;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

public class CustomCellHandler : CellHandler<CustomCell, CustomCell.ICallback>, CustomCell.IHandler, ISelectionAwareCellHandler
{
	static readonly string s_defaultId = Guid.NewGuid().ToString();
	readonly Dictionary<string, Stack<Control>> _cache = new();

	class WinUICellEventArgs : CellEventArgs
	{
		public WinUICellEventArgs(Grid? grid, CustomCell cell, int column)
			: base(grid, cell, -1, column, null, CellStates.None, null)
		{
		}

		public void Update(int row, object? item, bool isSelected)
		{
			Row = row;
			Item = item;
			IsSelected = isSelected;
		}

		public void SetIsEditing(bool isEditing) => IsEditing = isEditing;

		public void SetControl(Control? control) => Control = control;
	}

	/// <summary>Hosts the Eto control created for a cell.</summary>
	class CellBorder : muc.Grid
	{
		public mux.UIElement? Child
		{
			get => Children.Count > 0 ? Children[0] : null;
			set
			{
				Children.Clear();
				if (value != null)
					Children.Add(value);
			}
		}

		public Control? Control { get; set; }
		public string? Identifier { get; set; }
		public WinUICellEventArgs? Args { get; set; }
		public bool NeedsConfigure { get; set; }
	}

	Stack<Control> GetCache(string? id)
	{
		id ??= s_defaultId;
		if (!_cache.TryGetValue(id, out var stack))
			_cache[id] = stack = new Stack<Control>();
		return stack;
	}

	int ColumnIndex
	{
		get
		{
			var grid = ContainerHandler?.Grid;
			if (grid == null || ContainerHandler is not GridColumnHandler columnHandler)
				return -1;
			return grid.Columns.IndexOf(columnHandler.Widget);
		}
	}

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		var border = new CellBorder
		{
			HorizontalAlignment = mux.HorizontalAlignment.Stretch,
			VerticalAlignment = mux.VerticalAlignment.Stretch,
		};
		border.Loaded += Border_Loaded;
		border.Unloaded += Border_Unloaded;
		border.GotFocus += Border_GotFocus;
		border.LostFocus += Border_LostFocus;
		return border;
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		if (element is not CellBorder border)
			return;

		var args = border.Args ??= new WinUICellEventArgs(ContainerHandler?.Grid, Widget, ColumnIndex);
		var row = element.FindVisualParent<ctk.DataGridRow>();
		args.Update(row?.GetIndex() ?? -1, dataItem, GridHandler?.IsRowSelected(dataItem) == true);

		var id = Callback.OnGetIdentifier(Widget, args);
		var child = border.Control;
		if (child == null || id != border.Identifier)
		{
			if (child != null)
			{
				// keep the old control for other cells with the same identifier
				border.Child = null;
				if (child.Loaded)
					child.DetachNative();
				GetCache(border.Identifier).Push(child);
			}
			var cache = GetCache(id);
			child = cache.Count > 0 ? cache.Pop() : Callback.OnCreateCell(Widget, args);
			border.Control = child;
			border.Identifier = id;
			args.SetControl(child);
			border.Child = child.ToNative();
			if (border.IsLoaded && child?.Loaded == false)
				child.AttachNative();
		}

		Callback.OnConfigureCell(Widget, args, child!);
		border.NeedsConfigure = false;
		FormatCell(element, cell, dataItem);
	}

	void Border_Loaded(object sender, mux.RoutedEventArgs e)
	{
		if (sender is not CellBorder border || border.Control is not Control control)
			return;
		if (!control.Loaded)
			control.AttachNative();
		if (border.NeedsConfigure && border.Args != null)
		{
			border.Args.Update(border.Args.Row, border.DataContext, GridHandler?.IsRowSelected(border.DataContext) == true);
			Callback.OnConfigureCell(Widget, border.Args, control);
			border.NeedsConfigure = false;
		}
	}

	void Border_Unloaded(object sender, mux.RoutedEventArgs e)
	{
		if (sender is not CellBorder border || border.Control is not Control control || !control.Loaded)
			return;
		control.DetachNative();
		if (border.Args != null)
		{
			// clear the item so bindings don't keep long lived objects alive
			border.Args.Update(border.Args.Row, null, false);
			Callback.OnConfigureCell(Widget, border.Args, control);
			border.NeedsConfigure = true;
		}
	}

	void Border_GotFocus(object sender, mux.RoutedEventArgs e)
	{
		if (sender is not CellBorder border || border.Args is not WinUICellEventArgs args || args.IsEditing || !IsEditable)
			return;
		args.SetIsEditing(true);
		args.Handled = false;
		Callback.OnBeginEdit(Widget, args);
	}

	void Border_LostFocus(object sender, mux.RoutedEventArgs e)
	{
		if (sender is not CellBorder border || border.Args is not WinUICellEventArgs args || !args.IsEditing)
			return;
		// focus moved within the cell, so we're still editing
		if (border.XamlRoot != null && muxi.FocusManager.GetFocusedElement(border.XamlRoot) is mux.DependencyObject focused && focused.FindVisualParent<CellBorder>() == border)
			return;
		args.SetIsEditing(false);
		args.Handled = false;
		Callback.OnCommitEdit(Widget, args);
		CellEdited(border);
	}
}
