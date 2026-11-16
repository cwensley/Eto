using Eto.WinUI.Forms.Cells;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Controls;

public interface IGridColumnHandler : GridColumn.IHandler
{
	EtoDataGridColumn Control { get; }
}

public class GridColumnHandler : WidgetHandler<EtoDataGridColumn, GridColumn>, IGridColumnHandler, ICellContainerHandler
{
	Cell? _dataCell;
	string? _headerText;

	static readonly object AutoSize_Key = new object();
	static readonly object Expand_Key = new object();
	static readonly object Width_Key = new object();
	static readonly object HeaderTextAlignment_Key = new object();
	static readonly object HeaderToolTip_Key = new object();
	static readonly object CellToolTipBinding_Key = new object();

	public IGridHandler? GridHandler { get; private set; }

	protected override EtoDataGridColumn CreateControl() => new EtoDataGridColumn(this);

	protected override void Initialize()
	{
		base.Initialize();
		DataCell = new TextBoxCell();
		Editable = false;
		Sortable = false;
		SetWidth();
	}

	public string HeaderText
	{
		get => _headerText ?? string.Empty;
		set
		{
			_headerText = value;
			Control.Header = value;
		}
	}

	public bool Resizable
	{
		get => Control.CanUserResize;
		set => Control.CanUserResize = value;
	}

	public bool Sortable
	{
		get => Control.CanUserSort;
		set => Control.CanUserSort = value;
	}

	bool IsGridLoaded => GridHandler?.Loaded == true;

	public bool AutoSize
	{
		get
		{
			if (IsGridLoaded && !Expand)
				return Control.Width.IsAuto;
			return Widget.Properties.Get<bool>(AutoSize_Key, true);
		}
		set
		{
			if (Widget.Properties.TrySet(AutoSize_Key, value, true) || (value && IsGridLoaded))
				SetWidth();
		}
	}

	public bool Expand
	{
		get => Widget.Properties.Get<bool>(Expand_Key);
		set
		{
			if (Widget.Properties.TrySet(Expand_Key, value))
				SetWidth();
		}
	}

	public int Width
	{
		get
		{
			if (IsGridLoaded && Control.ActualWidth > 0)
				return (int)Math.Round(Control.ActualWidth);
			return Widget.Properties.Get(Width_Key, -1);
		}
		set
		{
			Widget.Properties.Set(Width_Key, value, -1);
			if (value == -1)
			{
				AutoSize = true;
				return;
			}
			Widget.Properties.Set(AutoSize_Key, false, true);
			SetWidth();
		}
	}

	void SetWidth()
	{
		if (Expand)
		{
			// auto size first so the column gets its content width, then switch to star once loaded
			if (AutoSize && !IsGridLoaded)
				Control.Width = ctk.DataGridLength.Auto;
			else
				Control.Width = new ctk.DataGridLength(1, ctk.DataGridLengthUnitType.Star);
		}
		else if (Widget.Properties.Get<bool>(AutoSize_Key, true))
			Control.Width = ctk.DataGridLength.Auto;
		else
			Control.Width = new ctk.DataGridLength(Widget.Properties.Get(Width_Key, -1));
	}

	internal void OnLoad()
	{
		if (Expand && AutoSize)
			SetWidth();
	}

	public Cell? DataCell
	{
		get => _dataCell;
		set
		{
			if (_dataCell?.Handler is ICellHandler oldHandler)
				oldHandler.ContainerHandler = null;
			_dataCell = value;
			if (_dataCell?.Handler is ICellHandler cellHandler)
				cellHandler.ContainerHandler = this;
			GridHandler?.RefreshColumn(this);
		}
	}

	internal ICellHandler? DataCellHandler => _dataCell?.Handler as ICellHandler;

	public bool Editable
	{
		get => !Control.IsReadOnly;
		set => Control.IsReadOnly = !value;
	}

	public bool Visible
	{
		get => Control.Visibility == mux.Visibility.Visible;
		set => Control.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	public void Setup(IGridHandler? gridHandler)
	{
		GridHandler = gridHandler;
		SetHeaderStyle();
	}

	public TextAlignment HeaderTextAlignment
	{
		get => Widget.Properties.Get<TextAlignment>(HeaderTextAlignment_Key);
		set
		{
			if (Widget.Properties.TrySet(HeaderTextAlignment_Key, value))
				SetHeaderStyle();
		}
	}

	public string HeaderToolTip
	{
		get => Widget.Properties.Get<string>(HeaderToolTip_Key);
		set
		{
			if (Widget.Properties.TrySet(HeaderToolTip_Key, value))
				SetHeaderStyle();
		}
	}

	void SetHeaderStyle()
	{
		var alignment = HeaderTextAlignment;
		var toolTip = HeaderToolTip;
		if (alignment == TextAlignment.Left && string.IsNullOrEmpty(toolTip))
		{
			Control.HeaderStyle = null;
			return;
		}

		var style = new mux.Style(typeof(ctk.Primitives.DataGridColumnHeader));
		if (GridHandler?.Control.ColumnHeaderStyle is mux.Style baseStyle)
			style.BasedOn = baseStyle;
		if (alignment != TextAlignment.Left)
			style.Setters.Add(new mux.Setter(muc.Control.HorizontalContentAlignmentProperty, alignment.ToWinUIHorizontal()));
		if (!string.IsNullOrEmpty(toolTip))
			style.Setters.Add(new mux.Setter(muc.ToolTipService.ToolTipProperty, toolTip));
		Control.HeaderStyle = style;
	}

	public int MinWidth
	{
		get => (int)Control.MinWidth;
		set => Control.MinWidth = value;
	}

	public int MaxWidth
	{
		get => double.IsInfinity(Control.MaxWidth) ? int.MaxValue : (int)Control.MaxWidth;
		set => Control.MaxWidth = value == int.MaxValue ? double.PositiveInfinity : value;
	}

	public int DisplayIndex
	{
		get => Control.DisplayIndex;
		set => Control.DisplayIndex = value;
	}

	public IIndirectBinding<string> CellToolTipBinding
	{
		get => Widget.Properties.Get<IIndirectBinding<string>>(CellToolTipBinding_Key);
		set => Widget.Properties.Set(CellToolTipBinding_Key, value);
	}

	public Grid? Grid => GridHandler?.Widget;

	bool ICellContainerHandler.Editable => Editable;

	public mux.FrameworkElement SetupCell(ICellHandler cell, mux.FrameworkElement defaultContent, ctk.DataGridCell gridCell)
	{
		return GridHandler?.SetupCell(this, defaultContent, gridCell) ?? defaultContent;
	}

	public void FormatCell(ICellHandler cell, mux.FrameworkElement element, ctk.DataGridCell gridCell, object? dataItem)
	{
		var toolTipBinding = CellToolTipBinding;
		if (toolTipBinding != null)
			muc.ToolTipService.SetToolTip(element, dataItem != null ? toolTipBinding.GetValue(dataItem) : null);
		GridHandler?.FormatCell(this, cell, element, gridCell, dataItem);
	}

	public void CellEdited(ICellHandler cell, mux.FrameworkElement element)
	{
		GridHandler?.CellEdited(this, element);
	}

	public void Refresh() => GridHandler?.RefreshColumn(this);

	EtoDataGridColumn IGridColumnHandler.Control => Control;
}
