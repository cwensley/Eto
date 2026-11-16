using Eto.CustomControls;
using Microsoft.UI.Xaml.Input;

namespace Eto.WinUI.Forms.Controls;

/// <summary>
/// Expand/collapse glyph shown before the content of the first column of a TreeGridView.
/// </summary>
public class TreeToggleButton : muc.Grid
{
	readonly muc.FontIcon _icon;
	bool _isExpanded;

	public TreeController? Controller { get; set; }

	public TreeToggleButton()
	{
		Width = TreeTogglePanel.LevelWidth;
		Background = new muxm.SolidColorBrush(mu.Colors.Transparent);
		_icon = new muc.FontIcon
		{
			FontSize = 10,
			Glyph = "", // ChevronRight
			HorizontalAlignment = mux.HorizontalAlignment.Center,
			VerticalAlignment = mux.VerticalAlignment.Center,
		};
		Children.Add(_icon);
		PointerPressed += OnPointerPressed;
		PointerReleased += OnPointerReleased;
		DoubleTapped += (sender, e) => e.Handled = true;
	}

	public bool IsExpanded
	{
		get => _isExpanded;
		set
		{
			_isExpanded = value;
			_icon.Glyph = value ? "" : ""; // ChevronDown : ChevronRight
		}
	}

	void OnPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		// don't let the grid select the row or begin editing
		e.Handled = true;
	}

	void OnPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		e.Handled = true;
		var position = e.GetCurrentPoint(this).Position;
		if (position.X < 0 || position.Y < 0 || position.X >= ActualWidth || position.Y >= ActualHeight)
			return;
		// toggle after the event, as the rows get regenerated
		DispatcherQueue.TryEnqueue(ToggleExpandCollapse);
	}

	void ToggleExpandCollapse()
	{
		if (Controller == null || DataContext is not ITreeGridItem item)
			return;
		var index = Controller.IndexOf(item);
		if (index < 0)
			return;
		if (IsExpanded)
		{
			if (Controller.CollapseRow(index))
				IsExpanded = false;
		}
		else if (Controller.ExpandRow(index))
			IsExpanded = true;
	}
}

public class TreeTogglePanel : muc.Grid
{
	public const int LevelWidth = 16;
	readonly TreeToggleButton _button;
	mux.FrameworkElement _content;

	public TreeTogglePanel(mux.FrameworkElement content, TreeController controller)
	{
		ColumnDefinitions.Add(new muc.ColumnDefinition { Width = mux.GridLength.Auto });
		ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
		_button = new TreeToggleButton { Controller = controller };
		Children.Add(_button);
		_content = content;
		SetColumn(content, 1);
		Children.Add(content);
		DataContextChanged += (sender, e) => Update(e.NewValue);
	}

	public mux.FrameworkElement Content => _content;

	public void Update(object? dataContext)
	{
		var controller = _button.Controller;
		if (dataContext is not ITreeGridItem item || controller == null)
		{
			_button.Visibility = mux.Visibility.Collapsed;
			return;
		}
		var index = controller.IndexOf(item);
		if (index < 0)
			return;
		_button.IsExpanded = controller.IsExpanded(index);
		// keep the space for the glyph so items line up with their expandable siblings
		_button.Opacity = item.Expandable ? 1 : 0;
		_button.IsHitTestVisible = item.Expandable;
		_button.Visibility = mux.Visibility.Visible;
		_button.Margin = new mux.Thickness(controller.LevelAtRow(index) * LevelWidth, 0, 0, 0);
	}

	public static bool IsOverExpander(mux.DependencyObject? source)
	{
		while (source != null)
		{
			if (source is TreeToggleButton button)
				return button.IsHitTestVisible;
			if (source is CommunityToolkit.WinUI.UI.Controls.DataGridCell)
				return false;
			source = muxm.VisualTreeHelper.GetParent(source);
		}
		return false;
	}
}
