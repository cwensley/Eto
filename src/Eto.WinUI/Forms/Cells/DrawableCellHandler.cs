using Eto.WinUI.Drawing;
using Microsoft.Graphics.Canvas.UI.Xaml;
using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

public class DrawableCellHandler : CellHandler<DrawableCell, DrawableCell.ICallback>, DrawableCell.IHandler, ISelectionAwareCellHandler
{
	// CanvasControl is sealed, so the state for each cell is kept in its Tag
	class CellState
	{
		public object? Item { get; set; }
		public bool IsSelected { get; set; }
	}

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		var canvas = new CanvasControl { MinHeight = 16, Tag = new CellState() };
		canvas.Draw += Canvas_Draw;
		return canvas;
	}

	void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
	{
		if (sender.Tag is not CellState cellState)
			return;
		var graphics = new Graphics(new GraphicsHandler(args.DrawingSession));
		var state = cellState.IsSelected ? CellStates.Selected : CellStates.None;
#pragma warning disable 618
		var paintArgs = new DrawableCellPaintEventArgs(graphics, new RectangleF(0, 0, (float)sender.ActualWidth, (float)sender.ActualHeight), state, cellState.Item);
#pragma warning restore 618
		Callback.OnPaint(Widget, paintArgs);
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		if (element is CanvasControl canvas && canvas.Tag is CellState state)
		{
			state.Item = dataItem;
			state.IsSelected = GridHandler?.IsRowSelected(dataItem) == true;
			canvas.Invalidate();
		}
		FormatCell(element, cell, dataItem);
	}
}

public class ProgressCellHandler : CellHandler<ProgressCell, ProgressCell.ICallback>, ProgressCell.IHandler
{
	float? GetValue(object? dataItem)
	{
		var progress = dataItem != null ? Widget.Binding?.GetValue(dataItem) : null;
		if (progress.HasValue)
			progress = Math.Max(0f, Math.Min(1f, progress.Value));
		return progress;
	}

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		var grid = new muc.Grid { Margin = new mux.Thickness(4, 2, 4, 2) };
		grid.Children.Add(new muc.ProgressBar { Minimum = 0, Maximum = 1, VerticalAlignment = mux.VerticalAlignment.Stretch, MinHeight = 16 });
		grid.Children.Add(new muc.TextBlock { HorizontalAlignment = mux.HorizontalAlignment.Center, VerticalAlignment = mux.VerticalAlignment.Center });
		return grid;
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		var value = GetValue(dataItem);
		if (element is muc.Grid grid)
		{
			var progressBar = grid.Children.OfType<muc.ProgressBar>().FirstOrDefault();
			var text = grid.Children.OfType<muc.TextBlock>().FirstOrDefault();
			if (progressBar != null)
			{
				progressBar.Visibility = value.HasValue ? mux.Visibility.Visible : mux.Visibility.Collapsed;
				progressBar.Value = value ?? 0;
			}
			if (text != null)
				text.Text = value.HasValue ? $"{value.Value * 100:0}%" : string.Empty;
		}
		FormatCell(element, cell, dataItem);
	}
}
