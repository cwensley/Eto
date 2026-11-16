using Eto.WinUI.Forms;
using Microsoft.UI.Xaml;
using cwc = CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using muxcp = Microsoft.UI.Xaml.Controls.Primitives;
using Eto.WinUI.Drawing;

namespace Eto.WinUI.Forms.Controls;

public class EtoGrid : muc.Grid //, IEtoWpfControl
{
	public IWinUIFrameworkElement? Handler { get; set; }

	internal sw.Size BaseMeasureOverride(sw.Size constraint) => base.MeasureOverride(constraint);

	protected override sw.Size MeasureOverride(sw.Size constraint)
	{
		return Handler?.MeasureOverride(constraint, base.MeasureOverride) ?? base.MeasureOverride(constraint);
	}
}

public class SplitterHandler : WinUIContainer<EtoGrid, Splitter, Splitter.ICallback>, Splitter.IHandler
{
	readonly cwc.GridSplitter _splitter;
	readonly cwc.DockPanel _pane1;
	readonly cwc.DockPanel _pane2;
	Orientation _orientation;
	SplitterFixedPanel _fixedPanel;
	int? _position;
	int _splitterWidth = 5;
	double _relative = double.NaN;
	bool _panel1Visible, _panel2Visible;
	int _panel1MinimumSize, _panel2MinimumSize;
	Control? _panel1, _panel2;
	//PropertyChangeNotifier _panel1VisibilityNotifier;
	//PropertyChangeNotifier _panel2VisibilityNotifier;


	//public override sw.Size MeasureOverride(sw.Size constraint, Func<sw.Size, sw.Size> measure)
	//{
	//	return base.MeasureOverride(constraint, BetterMeasure);
	//}

	public override FrameworkElement ContainerControl => Control;

	private sw.Size BetterMeasure(sw.Size constraint)
	{
		// call base measure override so everything gets set up correctly
		var size = Control.BaseMeasureOverride(constraint);

		if (FixedPanel == SplitterFixedPanel.Panel2)
		{
			// provide our own measuring when FixedPanel is Panel2, as WPF gets it wrong sometimes
			var panel1Size = _panel1?.GetContainerControl()?.DesiredSize ?? sw.Size.Empty;
			var panel2Size = _panel2?.GetContainerControl()?.DesiredSize ?? sw.Size.Empty;
			if (Orientation == Orientation.Horizontal)
				size.Width = Math.Max(size.Width, panel1Size.Width + panel2Size.Width + SplitterWidth);
			else
				size.Height = Math.Max(size.Height, panel1Size.Height + panel2Size.Height + SplitterWidth);
		}
		return size;
	}

	public SplitterHandler()
	{
		Control = new EtoGrid { Handler = this };

		Control.ColumnDefinitions.Add(new muc.ColumnDefinition());
		Control.ColumnDefinitions.Add(new muc.ColumnDefinition() { Width = mux.GridLength.Auto });
		Control.ColumnDefinitions.Add(new muc.ColumnDefinition());

		Control.RowDefinitions.Add(new muc.RowDefinition());
		Control.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		Control.RowDefinitions.Add(new muc.RowDefinition());

		_splitter = new cwc.GridSplitter
		{
			Background = SystemColorsHandler.ControlFillColorDefaultBrush,
			IsThumbVisible = true,
			ResizeBehavior = cwc.GridSplitter.GridResizeBehavior.PreviousAndNext,
			// the default style has a minimum size larger than SplitterWidth
			MinWidth = 0,
			MinHeight = 0,
			Padding = new mux.Thickness(0)
		};
		_pane1 = new cwc.DockPanel { LastChildFill = true };
		_pane2 = new cwc.DockPanel { LastChildFill = true };


		Control.Children.Add(_pane1);
		Control.Children.Add(_splitter);
		Control.Children.Add(_pane2);

		UpdateOrientation();
		Control.SizeChanged += Control_SizeChanged;

		//_panel1VisibilityNotifier = new PropertyChangeNotifier(mux.UIElement.VisibilityProperty);
		//_panel1VisibilityNotifier.ValueChanged += HandlePanel1IsVisibleChanged;

		//_panel2VisibilityNotifier = new PropertyChangeNotifier(mux.UIElement.VisibilityProperty);
		//_panel2VisibilityNotifier.ValueChanged += HandlePanel2IsVisibleChanged;
	}

	// set once the grid has its first size, as Loaded is raised asynchronously so a splitter added to a shown
	// window would still be unpositioned after an explicit UpdateLayout()
	bool _isLaidOut;

	private void Control_SizeChanged(object sender, mux.SizeChangedEventArgs e)
	{
		if (!_isLaidOut)
		{
			// only set initially, subsequent loads should keep the last position
			_isLaidOut = true;
			ResetMinMax();
			SetInitialPosition();
		}
		ResetMinMax();
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Splitter.PositionChangedEvent:
			case Splitter.PositionChangingEvent:
			case Splitter.PositionChangeStartedEvent:
			case Splitter.PositionChangeCompletedEvent:
				if (_dragEventsAttached)
					break;
				_dragEventsAttached = true;
				// GridSplitter resizes the columns itself, so report the position as the first pane changes size
				_splitter.ManipulationStarted += (sender, e) =>
				{
					_isDragging = true;
					_dragPosition = Position;
					Callback.OnPositionChangeStarted(Widget, EventArgs.Empty);
				};
				_splitter.ManipulationCompleted += (sender, e) =>
				{
					_isDragging = false;
					Callback.OnPositionChangeCompleted(Widget, EventArgs.Empty);
				};
				_pane1.SizeChanged += (sender, e) => HandleDragPositionChanged();
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	bool _dragEventsAttached;
	bool _isDragging;
	int _dragPosition;

	void HandleDragPositionChanged()
	{
		if (!_isDragging || PositionChangedEnabled > 0)
			return;
		var newPosition = Position;
		if (newPosition == _dragPosition)
			return;
		var args = new SplitterPositionChangingEventArgs(newPosition);
		Callback.OnPositionChanging(Widget, args);
		if (args.Cancel)
		{
			// put it back, GridSplitter can't be stopped mid drag
			PositionChangedEnabled++;
			SetPosition(_dragPosition);
			PositionChangedEnabled--;
			return;
		}
		_dragPosition = newPosition;
		Callback.OnPositionChanged(Widget, EventArgs.Empty);
	}

	static object PositionChangedEnabled_Key = new object();
	int PositionChangedEnabled
	{
		get { return Widget.Properties.Get(PositionChangedEnabled_Key, 0); }
		set { Widget.Properties.Set(PositionChangedEnabled_Key, value, 0); }
	}

	void SetInitialPosition()
	{
		_panel1Visible = _panel1?.Visible ?? false;
		_panel2Visible = _panel2?.Visible ?? false;

		// controls should be stretched to fit panels
		SetStretch(_panel1);
		SetStretch(_panel2);
		UpdateColumnSizing(false);

		if (_position != null)
		{
			var pos = _position.Value;
			if (_fixedPanel != SplitterFixedPanel.Panel1)
			{
				var size = GetAvailableSize(false);
				var want = GetAvailableSize(true);
				if (size != want)
				{
					if (FixedPanel == SplitterFixedPanel.Panel2)
						pos += (int)Math.Round(size - want);
					else
					{
						SetRelative(pos / (double)want);
						return;
					}
				}

			}
			SetPosition(pos);
		}
		else if (!double.IsNaN(_relative))
		{
			SetRelative(_relative);
		}
		else if (_fixedPanel == SplitterFixedPanel.Panel1)
		{
			var size1 = _panel1?.GetPreferredSize() ?? SizeF.Empty;
			SetRelative(_orientation == Orientation.Horizontal ? size1.Width : size1.Height);
		}
		else if (_fixedPanel == SplitterFixedPanel.Panel2)
		{
			var size2 = _panel2?.GetPreferredSize() ?? SizeF.Empty;
			SetRelative(_orientation == Orientation.Horizontal ? size2.Width : size2.Height);
		}
		else
		{
			var size1 = _panel1?.GetPreferredSize() ?? SizeF.Empty;
			var size2 = _panel2?.GetPreferredSize() ?? SizeF.Empty;
			SetRelative(_orientation == Orientation.Horizontal
				? size1.Width / (double)(size1.Width + size2.Width)
				: size1.Height / (double)(size1.Height + size2.Height));
		}
	}

	static void SetStretch(Control panel)
	{
		if (panel != null)
		{
			var control = panel.GetContainerControl();
			control.VerticalAlignment = mux.VerticalAlignment.Stretch;
			control.HorizontalAlignment = mux.HorizontalAlignment.Stretch;
			/*
			((mux.FrameworkElement)panel.ControlObject).Width = double.NaN;
			((mux.FrameworkElement)panel.ControlObject).Height = double.NaN;
			 * */
		}
	}

	void UpdateOrientation()
	{
		if (_orientation == Orientation.Horizontal)
		{
			_splitter.ResizeDirection = cwc.GridSplitter.GridResizeDirection.Columns;
			_splitter.HorizontalAlignment = mux.HorizontalAlignment.Center;
			_splitter.VerticalAlignment = mux.VerticalAlignment.Stretch;

			_splitter.SetValue(muc.Grid.RowSpanProperty, 3);
			_pane1.SetValue(muc.Grid.RowSpanProperty, 3);
			_pane2.SetValue(muc.Grid.RowSpanProperty, 3);

			_splitter.SetValue(muc.Grid.ColumnSpanProperty, 1);
			_pane1.SetValue(muc.Grid.ColumnSpanProperty, 1);
			_pane2.SetValue(muc.Grid.ColumnSpanProperty, 1);

			muc.Grid.SetColumn(_splitter, 1);
			muc.Grid.SetRow(_splitter, 0);
			muc.Grid.SetColumn(_pane2, 2);
			muc.Grid.SetRow(_pane2, 0);

			_splitter.Width = _splitterWidth;
			_splitter.Height = double.NaN;
		}
		else
		{
			_splitter.ResizeDirection = cwc.GridSplitter.GridResizeDirection.Rows;
			_splitter.HorizontalAlignment = mux.HorizontalAlignment.Stretch;
			_splitter.VerticalAlignment = mux.VerticalAlignment.Top;
			_pane2.VerticalAlignment = mux.VerticalAlignment.Stretch;

			_splitter.SetValue(muc.Grid.RowSpanProperty, 1);
			_pane1.SetValue(muc.Grid.RowSpanProperty, 1);
			_pane2.SetValue(muc.Grid.RowSpanProperty, 1);

			_splitter.SetValue(muc.Grid.ColumnSpanProperty, 3);
			_pane1.SetValue(muc.Grid.ColumnSpanProperty, 3);
			_pane2.SetValue(muc.Grid.ColumnSpanProperty, 3);

			muc.Grid.SetColumn(_splitter, 0);
			muc.Grid.SetRow(_splitter, 1);
			muc.Grid.SetColumn(_pane2, 0);
			muc.Grid.SetRow(_pane2, 2);

			_splitter.Width = double.NaN;
			_splitter.Height = _splitterWidth;
		}
		UpdateColumnSizing(_position.HasValue || !double.IsNaN(_relative));
	}

	void UpdateColumnSizing(bool updatePosition)
	{
		if (updatePosition && _position == null && double.IsNaN(_relative))
			UpdateRelative();

		//SetLength(1, mux.GridLength.Auto);
		switch (FixedPanel)
		{
			case SplitterFixedPanel.Panel1:
				SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
				break;
			case SplitterFixedPanel.Panel2:
				SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
				break;
			case SplitterFixedPanel.None:
				SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
				SetLength(2, new mux.GridLength(1, mux.GridUnitType.Star));
				break;
		}

		if (updatePosition)
		{
			SetPositionOrRelative();
		}
	}

	void SetPositionOrRelative()
	{
		if (_position.HasValue)
			SetPosition(_position.Value);
		else if (!double.IsNaN(_relative))
			SetRelative(_relative);
		else
			SetHiddenPanels();
	}

	public Orientation Orientation
	{
		get
		{
			switch (_splitter.ResizeDirection)
			{
				case cwc.GridSplitter.GridResizeDirection.Columns:
					return Orientation.Horizontal;
				case cwc.GridSplitter.GridResizeDirection.Rows:
					return Orientation.Vertical;
				default:
					throw new NotSupportedException();
			}
		}
		set
		{
			_orientation = value;
			UpdateOrientation();
		}
	}

	public SplitterFixedPanel FixedPanel
	{
		get { return _fixedPanel; }
		set
		{
			if (_isLaidOut)
			{
				if (HasHiddenPanels)
					UpdateRelativePosition(value);
				else
					_position = Position;
			}
			else if (WasLoaded)
				UpdateRelativePosition(value);

			_fixedPanel = value;

			if (_fixedPanel == SplitterFixedPanel.None)
			{
				// positionchanged events get triggered here when the fixed panel is none
				HandleEvent(Splitter.PositionChangingEvent);
			}

			UpdateColumnSizing(_isLaidOut);
		}
	}

	private void UpdateRelativePosition(SplitterFixedPanel newFixedPanel)
	{
		if (double.IsNaN(_relative))
			return;

		// translate relative position from old fixed panel to new fixed panel
		var width = _orientation == Orientation.Horizontal ? Control.ActualWidth : Control.ActualHeight;
		switch (_fixedPanel)
		{
			case SplitterFixedPanel.Panel1:
				switch (newFixedPanel)
				{
					case SplitterFixedPanel.Panel2:
						_relative = width - _relative - SplitterWidth;
						break;
					case SplitterFixedPanel.None:
						_relative = _relative / width;
						break;
				}
				break;
			case SplitterFixedPanel.Panel2:
				switch (newFixedPanel)
				{
					case SplitterFixedPanel.Panel1:
						_relative = width - _relative - SplitterWidth;
						break;
					case SplitterFixedPanel.None:
						_relative = (width - _relative) / width;
						break;
				}
				break;
			case SplitterFixedPanel.None:
				switch (newFixedPanel)
				{
					case SplitterFixedPanel.Panel1:
						_relative = width * _relative;
						break;
					case SplitterFixedPanel.Panel2:
						_relative = width - (width * _relative);
						break;
				}
				break;
		}
	}

	double DoublePosition
	{
		get
		{
			if (_position != null)
				return _position.Value;
			if (!_isLaidOut && !WasLoaded)
				return 0;
			if (_splitter.ResizeDirection == cwc.GridSplitter.GridResizeDirection.Columns)
				return Control.ColumnDefinitions[0].ActualWidth;
			return Control.RowDefinitions[0].ActualHeight;
		}
	}

	public int Position
	{
		get => (int)Math.Round(DoublePosition);
		set
		{
			SetPosition(value);
		}
	}

	public int SplitterWidth
	{
		get { return _splitterWidth; }
		set
		{
			if (_splitterWidth == value)
				return;
			_splitterWidth = value;
			if (_orientation == Orientation.Horizontal)
				_splitter.Width = value;
			else
				_splitter.Height = value;
		}
	}

	double GetAvailableSize()
	{
		return GetAvailableSize(!_isLaidOut);
	}

	double GetAvailableSize(bool desired)
	{
		if (desired)
		{
			//var size = UserPreferredSize;
			//var pick = Orientation == Orientation.Horizontal ? size.Width : size.Height;
			//if (pick >= 0)
			//	return pick - _splitterWidth;
		}
		var width = Orientation == Orientation.Horizontal ? Control.ActualWidth : Control.ActualHeight;
		if (double.IsNaN(width))
			width = Orientation == Orientation.Horizontal ? Control.Width : Control.Height;
		return width - _splitterWidth;
	}

	void UpdateRelative()
	{
		var pos = Position;
		if (_fixedPanel == SplitterFixedPanel.Panel1)
			_relative = pos;
		else
		{
			var sz = GetAvailableSize();
			if (_fixedPanel == SplitterFixedPanel.Panel2)
				// layout rounding can leave fractional sizes, but fixed sizes are whole pixels
				_relative = sz <= 0 ? 0 : Math.Round(sz - DoublePosition);
			else
				_relative = sz <= 0 ? 0.5 : pos / (double)sz;
		}
	}

	public double RelativePosition
	{
		get
		{
			if (double.IsNaN(_relative) || Widget.Loaded)
				UpdateRelative();
			return _relative;
		}
		set
		{
			if (_relative == value)
				return;
			SetRelative(value);
			Callback.OnPositionChanged(Widget, EventArgs.Empty);
		}
	}

	double GetSize(int panel)
	{
		if (_splitter.ResizeDirection == cwc.GridSplitter.GridResizeDirection.Columns)
			return Control.ColumnDefinitions[panel].ActualWidth;
		else
			return Control.RowDefinitions[panel].ActualHeight;
	}


	mux.GridLength GetLength(int panel)
	{
		if (_splitter.ResizeDirection == cwc.GridSplitter.GridResizeDirection.Columns)
			return Control.ColumnDefinitions[panel].Width;
		else
			return Control.RowDefinitions[panel].Height;
	}

	void SetLength(int panel, mux.GridLength value)
	{
		if (_splitter.ResizeDirection == cwc.GridSplitter.GridResizeDirection.Columns)
			Control.ColumnDefinitions[panel].Width = value;
		else
			Control.RowDefinitions[panel].Height = value;
	}

	bool HasHiddenPanels
	{
		get { return _panel1 == null || !_panel1.Visible || _panel2 == null || !_panel2.Visible; }
	}

	bool SetHiddenPanels()
	{
		if (!Widget.Loaded)
			return false;
		if (_panel1 == null || !_panel1.Visible)
		{
			SetLength(0, new mux.GridLength(0, mux.GridUnitType.Pixel));
			SetLength(1, new mux.GridLength(0, mux.GridUnitType.Pixel));
			SetLength(2, new mux.GridLength(1, mux.GridUnitType.Star));
			return true;
		}
		if (_panel2 == null || !_panel2.Visible)
		{
			SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
			SetLength(1, new mux.GridLength(0, mux.GridUnitType.Pixel));
			SetLength(2, new mux.GridLength(0, mux.GridUnitType.Pixel));
			return true;
		}
		return false;
	}

	void SetPosition(int newPosition)
	{
		if (SetHiddenPanels())
			return;
		SetLength(1, mux.GridLength.Auto);
		if (!_isLaidOut)
		{
			_position = newPosition;
			_relative = double.NaN;
			SetLength(0, new mux.GridLength(Math.Max(0, newPosition), mux.GridUnitType.Pixel));
			SetLength(2, new mux.GridLength(1, mux.GridUnitType.Star));
			return;
		}

		_position = null;
		var size = GetAvailableSize(false);
		_relative = _fixedPanel == SplitterFixedPanel.Panel1 ? Math.Max(0, newPosition)
			: _fixedPanel == SplitterFixedPanel.Panel2 ? Math.Max(0, size - newPosition)
			: size <= 0 ? 0.5 : Math.Max(0.0, Math.Min(1.0, newPosition / (double)size));
		if (_fixedPanel == SplitterFixedPanel.Panel1)
		{
			SetLength(0, new mux.GridLength(Math.Max(0, newPosition), mux.GridUnitType.Pixel));
			SetLength(2, new mux.GridLength(1, mux.GridUnitType.Star));
		}
		else if (_fixedPanel == SplitterFixedPanel.Panel2)
		{
			SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
			SetLength(2, new mux.GridLength(Math.Max(0, size - newPosition), mux.GridUnitType.Pixel));
		}
		else
		{
			SetLength(0, new mux.GridLength(Math.Max(0, newPosition), mux.GridUnitType.Star));
			SetLength(2, new mux.GridLength(Math.Max(0, size - newPosition), mux.GridUnitType.Star));
		}
	}

	void SetRelative(double newRelative)
	{
		if (SetHiddenPanels())
			return;
		_position = null;
		_relative = newRelative;
		PositionChangedEnabled++;
		SetLength(1, mux.GridLength.Auto);
		if (_fixedPanel == SplitterFixedPanel.Panel1)
		{
			SetLength(0, new mux.GridLength(Math.Max(0, _relative), mux.GridUnitType.Pixel));
			SetLength(2, new mux.GridLength(1, mux.GridUnitType.Star));
		}
		else if (_fixedPanel == SplitterFixedPanel.Panel2)
		{
			SetLength(0, new mux.GridLength(1, mux.GridUnitType.Star));
			SetLength(2, new mux.GridLength(Math.Max(0, _relative), mux.GridUnitType.Pixel));
		}
		else
		{
			SetLength(0, new mux.GridLength(Math.Max(0, _relative), mux.GridUnitType.Star));
			SetLength(2, new mux.GridLength(Math.Max(0, 1 - _relative), mux.GridUnitType.Star));
		}
		PositionChangedEnabled--;
	}

	//public override void SetScale(bool xscale, bool yscale)
	//{
	//	base.SetScale(xscale, yscale);
	//	var control = _panel1.GetWpfFrameworkElement();
	//	if (control != null)
	//		control.SetScale(true, true);
	//	control = _panel2.GetWpfFrameworkElement();
	//	if (control != null)
	//		control.SetScale(true, true);
	//}

	public Control? Panel1
	{
		get { return _panel1; }
		set
		{
			//_panel1VisibilityNotifier.PropertySource = null;

			_panel1 = value;
			_pane1.Children.Clear();
			if (_panel1 != null)
			{
				var control = _panel1.GetFrameworkElement();
				SetStretch(_panel1);
				//if (Widget.Loaded)
				//	control.SetScale(true, true);

				_pane1.Children.Add(control.ContainerControl);
				//_panel1VisibilityNotifier.PropertySource = control.ContainerControl;
				HandlePanelVisibleChanged(ref _panel1Visible, _panel1);
			}
			else
			{
				SetHiddenPanels();
			}
		}
	}

	public Control Panel2
	{
		get { return _panel2; }
		set
		{
			//_panel2VisibilityNotifier.PropertySource = null;

			_panel2 = value;
			_pane2.Children.Clear();
			if (_panel2 != null)
			{
				var control = _panel2.GetFrameworkElement();
				SetStretch(_panel2);
				//if (Widget.Loaded)
				//	control.SetScale(true, true);
				_pane2.Children.Add(control.ContainerControl);

				//_panel2VisibilityNotifier.PropertySource = control.ContainerControl;
				HandlePanelVisibleChanged(ref _panel2Visible, _panel2);
			}
			else
			{
				SetHiddenPanels();
			}
		}
	}

	void HandlePanel2IsVisibleChanged(object sender, mux.DependencyPropertyChangedEventArgs e)
	{
		HandlePanelVisibleChanged(ref _panel2Visible, _panel2);
	}

	void HandlePanel1IsVisibleChanged(object sender, mux.DependencyPropertyChangedEventArgs e)
	{
		HandlePanelVisibleChanged(ref _panel1Visible, _panel1);
	}

	void HandlePanelVisibleChanged(ref bool isVisible, Control panel)
	{
		if ((_isLaidOut || WasLoaded) && isVisible != panel.Visible)
		{
			isVisible = panel.Visible;
			if (!panel.Visible)
			{
				_position = null;
				if ((_panel1 != null && _panel1.Visible) || (_panel2 != null && _panel2.Visible))
					UpdateRelative();
				SetHiddenPanels();
			}
			else if (!double.IsNaN(_relative))
			{
				SetRelative(_relative);
			}
		}
	}

	public void Remove(mux.FrameworkElement child)
	{
		if (_pane1.Children.Contains(child))
		{
			//_panel1VisibilityNotifier.PropertySource = null;
			_panel1 = null;
			_pane1.Children.Remove(child);
		}
		else if (_pane2.Children.Contains(child))
		{
			//_panel2VisibilityNotifier.PropertySource = null;
			_panel2 = null;
			_pane2.Children.Remove(child);
		}
	}

	static readonly object WasLoaded_Key = new object();

	bool WasLoaded
	{
		get { return Widget.Properties.Get<bool>(WasLoaded_Key); }
		set { Widget.Properties.Set(WasLoaded_Key, value); }
	}

	private void ResetMinMax()
	{
		if (Orientation == Orientation.Horizontal)
		{
			Control.ColumnDefinitions[0].MinWidth = _panel1MinimumSize;
			Control.ColumnDefinitions[2].MinWidth = _panel2MinimumSize;
			Control.RowDefinitions[0].MinHeight = 0;
			Control.RowDefinitions[2].MinHeight = 0;
		}
		else
		{
			Control.ColumnDefinitions[0].MinWidth = 0;
			Control.ColumnDefinitions[2].MinWidth = 0;
			Control.RowDefinitions[0].MinHeight = Panel1MinimumSize;
			Control.RowDefinitions[2].MinHeight = _panel2MinimumSize;
		}

		if (_isLaidOut)
		{
			var available = this.GetAvailableSize();
			// need to set the max height of resizing panel otherwise it can grow outside of window
			if (Orientation == Orientation.Horizontal)
			{
				Control.ColumnDefinitions[0].MaxWidth = FixedPanel == SplitterFixedPanel.Panel1 ? Math.Max(0, available - Panel2MinimumSize) : double.PositiveInfinity;
				Control.ColumnDefinitions[2].MaxWidth = FixedPanel == SplitterFixedPanel.Panel2 ? Math.Max(0, available - Panel1MinimumSize) : double.PositiveInfinity;
				Control.RowDefinitions[0].MaxHeight = double.PositiveInfinity;
				Control.RowDefinitions[2].MaxHeight = double.PositiveInfinity;
			}
			else
			{
				Control.ColumnDefinitions[0].MaxWidth = double.PositiveInfinity;
				Control.ColumnDefinitions[2].MaxWidth = double.PositiveInfinity;
				Control.RowDefinitions[0].MaxHeight = FixedPanel == SplitterFixedPanel.Panel1 ? Math.Max(0, available - Panel2MinimumSize) : double.PositiveInfinity;
				Control.RowDefinitions[2].MaxHeight = FixedPanel == SplitterFixedPanel.Panel2 ? Math.Max(0, available - Panel1MinimumSize) : double.PositiveInfinity;
			}
		}
		else
		{
			// need to set the max height of panel 2 otherwise it will grow beyond the bounds
			Control.ColumnDefinitions[0].MaxWidth = double.PositiveInfinity;
			Control.ColumnDefinitions[2].MaxWidth = double.PositiveInfinity;
			Control.RowDefinitions[0].MaxHeight = double.PositiveInfinity;
			Control.RowDefinitions[2].MaxHeight = double.PositiveInfinity;
		}
	}

	public int Panel1MinimumSize
	{
		get { return _panel1MinimumSize; }
		set
		{
			_panel1MinimumSize = value;
			ResetMinMax();
		}
	}

	public int Panel2MinimumSize
	{
		get { return _panel2MinimumSize; }
		set
		{
			_panel2MinimumSize = value;
			ResetMinMax();
		}
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		WasLoaded = false;
	}

	public override void OnUnLoad(EventArgs e)
	{
		base.OnUnLoad(e);
		WasLoaded = true;
		if (double.IsNaN(_relative) || !HasHiddenPanels)
			UpdateRelative();
		_position = null;
	}
}