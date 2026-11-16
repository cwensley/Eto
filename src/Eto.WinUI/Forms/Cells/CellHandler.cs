using Eto.WinUI.Forms.Controls;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

public interface ICellContainerHandler
{
	Grid? Grid { get; }
	IGridHandler? GridHandler { get; }
	mux.FrameworkElement SetupCell(ICellHandler cell, mux.FrameworkElement defaultContent, ctk.DataGridCell gridCell);
	void FormatCell(ICellHandler cell, mux.FrameworkElement element, ctk.DataGridCell gridCell, object? dataItem);
	void CellEdited(ICellHandler cell, mux.FrameworkElement element);
	bool Editable { get; }
	/// <summary>Updates all realized cells, e.g. after a property of the cell changed.</summary>
	void Refresh();
}

public interface ICellHandler : Cell.IHandler
{
	ICellContainerHandler? ContainerHandler { get; set; }

	/// <summary>Creates the element for a cell, which is reused for different items as rows are recycled.</summary>
	mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing);

	/// <summary>Updates an element created by <see cref="CreateElement"/> to show the specified item.</summary>
	void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing);

	/// <summary>Gets a value indicating the cell has a separate editing element.</summary>
	bool SupportsEditing { get; }

	object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs? args);
	void CommitCellEdit(mux.FrameworkElement editingElement, object? dataItem);
	void CancelCellEdit(mux.FrameworkElement editingElement, object? dataItem);

	/// <summary>Called when the user clicks a cell that is not being edited; set Handled to stop the default behaviour.</summary>
	void OnCellClick(GridCellMouseEventArgs args, mux.DependencyObject? originalSource, ctk.DataGridCell cell);
}

/// <summary>Cells that show the selected state themselves, so are refreshed when the selection changes.</summary>
public interface ISelectionAwareCellHandler
{
}

public abstract class CellHandler<TWidget, TCallback> : WidgetHandler<object, TWidget, TCallback>, ICellHandler
	where TWidget : Cell
	where TCallback : Cell.ICallback
{
	public ICellContainerHandler? ContainerHandler { get; set; }

	public IGridHandler? GridHandler => ContainerHandler?.GridHandler;

	protected override object CreateControl() => new object();

	public abstract mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing);

	public abstract void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing);

	public virtual bool SupportsEditing => false;

	public virtual object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs? args) => null;

	public virtual void CommitCellEdit(mux.FrameworkElement editingElement, object? dataItem)
	{
	}

	public virtual void CancelCellEdit(mux.FrameworkElement editingElement, object? dataItem)
	{
	}

	public virtual void OnCellClick(GridCellMouseEventArgs args, mux.DependencyObject? originalSource, ctk.DataGridCell cell)
	{
	}

	protected bool IsEditable => ContainerHandler?.Editable == true;

	protected void FormatCell(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem)
	{
		ContainerHandler?.FormatCell(this, element, cell, dataItem);
	}

	protected void CellEdited(mux.FrameworkElement element) => ContainerHandler?.CellEdited(this, element);

	protected void Refresh() => ContainerHandler?.Refresh();

	protected static T? FindChild<T>(mux.FrameworkElement element)
		where T : class
	{
		return element as T ?? element.FindVisualChild<T>();
	}
}

/// <summary>
/// Column used by every grid column, which generates its cells from the column's current <see cref="Cell"/>.
/// </summary>
public class EtoDataGridColumn : ctk.DataGridColumn
{
	public GridColumnHandler Handler { get; }

	public EtoDataGridColumn(GridColumnHandler handler)
	{
		Handler = handler;
	}

	ICellHandler? CellHandler => Handler.DataCellHandler;

	// the element the cell handler created, before it is wrapped by SetupCell (e.g. for the tree expander)
	static readonly mux.DependencyProperty InnerElementProperty = mux.DependencyProperty.RegisterAttached(
		"EtoInnerElement", typeof(mux.FrameworkElement), typeof(EtoDataGridColumn), new mux.PropertyMetadata(null));

	public static mux.FrameworkElement? GetInnerElement(mux.FrameworkElement? element)
	{
		if (element == null)
			return null;
		return element.GetValue(InnerElementProperty) as mux.FrameworkElement ?? element;
	}

	mux.FrameworkElement Generate(ctk.DataGridCell cell, object dataItem, bool isEditing)
	{
		var handler = CellHandler;
		if (handler == null)
			return new muc.TextBlock();

		if (isEditing && !handler.SupportsEditing)
			isEditing = false;

		var element = handler.CreateElement(cell, isEditing);
		var editing = isEditing;
		element.DataContextChanged += (sender, e) =>
		{
			// rows are recycled, so the element is updated whenever it is given a new item
			if (ReferenceEquals(CellHandler, handler))
				handler.ConfigureElement(sender, cell, e.NewValue, editing);
		};
		handler.ConfigureElement(element, cell, dataItem, isEditing);

		var container = Handler.SetupCell(handler, element, cell);
		if (!ReferenceEquals(container, element))
			container.SetValue(InnerElementProperty, element);
		return container;
	}

	protected override mux.FrameworkElement GenerateElement(ctk.DataGridCell cell, object dataItem) => Generate(cell, dataItem, false);

	protected override mux.FrameworkElement GenerateEditingElement(ctk.DataGridCell cell, object dataItem) => Generate(cell, dataItem, true);

	protected override object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs editingEventArgs)
	{
		return CellHandler?.PrepareCellForEdit(GetInnerElement(editingElement)!, editingEventArgs);
	}

	protected override void CancelCellEdit(mux.FrameworkElement editingElement, object uneditedValue)
	{
		var element = GetInnerElement(editingElement)!;
		CellHandler?.CancelCellEdit(element, element.DataContext);
	}

	internal void CommitCellEdit(mux.FrameworkElement editingElement)
	{
		var element = GetInnerElement(editingElement)!;
		CellHandler?.CommitCellEdit(element, element.DataContext);
	}

	/// <summary>Updates all generated cells of this column, e.g. after the data changed.</summary>
	internal void RefreshCell(ctk.DataGridRow row)
	{
		var handler = CellHandler;
		var content = GetCellContent(row);
		if (handler == null || content == null)
			return;
		var cell = content.FindVisualParent<ctk.DataGridCell>();
		if (cell == null)
			return;
		var element = GetInnerElement(content)!;
		var isEditing = handler.SupportsEditing && Handler.GridHandler?.IsEditingElement(element) == true;
		handler.ConfigureElement(element, cell, row.DataContext, isEditing);
	}
}
