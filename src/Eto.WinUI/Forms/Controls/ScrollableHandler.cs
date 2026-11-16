using Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms.Controls;

public class ScrollableHandler : WinUIPanel<muc.ScrollViewer, Scrollable, Scrollable.ICallback>, Scrollable.IHandler
{
	readonly EtoContentControl _container;
	bool _expandContentWidth = true;
	bool _expandContentHeight = true;
	float _minimumZoom = 1f;
	float _maximumZoom = 1f;

	public ScrollableHandler()
	{
		_container = new EtoContentControl { Handler = this };
	}

	protected override muc.ScrollViewer CreateControl() => new()
	{
		HorizontalScrollBarVisibility = muc.ScrollBarVisibility.Auto,
		VerticalScrollBarVisibility = muc.ScrollBarVisibility.Auto,
		HorizontalScrollMode = muc.ScrollMode.Auto,
		VerticalScrollMode = muc.ScrollMode.Auto,
		ZoomMode = muc.ZoomMode.Disabled,
		IsTabStop = false
	};

	protected override void Initialize()
	{
		base.Initialize();
		_container.Content = Control;
		// the content is measured at its own size so it can be bigger than the viewport, not at the size of the scrollable
		ContentBorder.Handler = null;
		ContentBorder.HorizontalAlignment = mux.HorizontalAlignment.Left;
		ContentBorder.VerticalAlignment = mux.VerticalAlignment.Top;
		Control.SizeChanged += (sender, e) =>
		{
			UpdateExpand();
			// the width expanded content is measured at depends on the viewport
			_container.InvalidateMeasure();
		};
		Border = BorderType.Bezel;
	}

	// expand the content to the viewport; scroll bars overlay the content in WinUI, so it's the size of the viewer
	void UpdateExpand()
	{
		SetMin(_expandContentWidth ? Control.ActualWidth : 0, true);
		SetMin(_expandContentHeight ? Control.ActualHeight : 0, false);
	}

	public override wf.Size MeasureOverride(wf.Size constraint, Func<wf.Size, wf.Size> measure)
	{
		// start from the scroll size each time, so content that changed size is measured again
		ContentBorder.Width = _scrollSize.Width;
		ContentBorder.Height = _scrollSize.Height;
		if (_expandContentWidth && double.IsNaN(_scrollSize.Width))
			ConstrainExpandedWidth(Control.ActualWidth);
		return base.MeasureOverride(constraint, measure);
	}

	// Content expanded to the viewport width is displayed at that width, so measure it there when it fits or wraps
	// to fit, otherwise wrapping content reports the height it has at its narrower width. Content that is wider
	// and doesn't wrap is left as is so it scrolls horizontally.
	void ConstrainExpandedWidth(double viewport)
	{
		if (double.IsNaN(viewport) || double.IsInfinity(viewport) || viewport <= 0)
			return;
		ContentBorder.Measure(new wf.Size(double.PositiveInfinity, double.PositiveInfinity));
		var natural = ContentBorder.DesiredSize;
		var constrain = natural.Width <= viewport + 0.5;
		if (!constrain)
		{
			ContentBorder.Measure(new wf.Size(viewport, double.PositiveInfinity));
			constrain = ContentBorder.DesiredSize.Height > natural.Height + 0.5;
		}
		if (constrain)
			ContentBorder.Width = viewport;
	}

	void SetMin(double value, bool width)
	{
		var current = width ? ContentBorder.MinWidth : ContentBorder.MinHeight;
		// only when it changes, as it measures again
		if (Math.Abs(current - value) < 0.5)
			return;
		if (width)
			ContentBorder.MinWidth = value;
		else
			ContentBorder.MinHeight = value;
	}

	public override mux.FrameworkElement ContainerControl => _container;

	public override void SetContainerContent(FrameworkElement content) => Control.Content = content;

	public void UpdateScrollSizes()
	{
		ContentBorder.InvalidateMeasure();
		Control.InvalidateMeasure();
		Control.UpdateLayout();
	}

	public Point ScrollPosition
	{
		get => new Point((int)Math.Round(Control.HorizontalOffset), (int)Math.Round(Control.VerticalOffset));
		set
		{
			Control.UpdateLayout();
			Control.ChangeView(value.X, value.Y, null, true);
		}
	}

	public Size ScrollSize
	{
		get => new Size((int)Math.Round(Control.ExtentWidth), (int)Math.Round(Control.ExtentHeight));
		set
		{
			_scrollSize = new wf.Size(value.Width >= 0 ? value.Width : double.NaN, value.Height >= 0 ? value.Height : double.NaN);
			_container.InvalidateMeasure();
		}
	}

	wf.Size _scrollSize = new wf.Size(double.NaN, double.NaN);

	static mux.Style? s_borderStyle;

	static readonly object Border_Key = new object();

	public BorderType Border
	{
		get => Widget.Properties.Get(Border_Key, BorderType.Bezel);
		set
		{
			Widget.Properties.Set(Border_Key, value);
			if (value == BorderType.None)
			{
				_container.BorderThickness = new Thickness(0);
				return;
			}
			// a ThemeResource can only be referenced from XAML, and it lets the border follow the theme
			s_borderStyle ??= (mux.Style)mux.Markup.XamlReader.Load(
				"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'>" +
				"<Setter Property='BorderBrush' Value='{ThemeResource ControlStrokeColorDefaultBrush}'/></Style>");
			_container.Style = s_borderStyle;
			_container.BorderThickness = new Thickness(1);
		}
	}

	public override Size ClientSize
	{
		get
		{
			if (!Control.IsLoaded)
				return base.ClientSize;
			return new Size((int)Math.Round(Control.ViewportWidth), (int)Math.Round(Control.ViewportHeight));
		}
		set => base.ClientSize = value;
	}

	public Rectangle VisibleRect => new Rectangle(ScrollPosition, ClientSize);

	public bool ExpandContentWidth
	{
		get => _expandContentWidth;
		set
		{
			if (_expandContentWidth == value)
				return;
			_expandContentWidth = value;
			UpdateExpand();
			_container.InvalidateMeasure();
		}
	}

	public bool ExpandContentHeight
	{
		get => _expandContentHeight;
		set
		{
			if (_expandContentHeight == value)
				return;
			_expandContentHeight = value;
			UpdateExpand();
		}
	}

	public float MinimumZoom
	{
		get => _minimumZoom;
		set
		{
			_minimumZoom = value;
			UpdateZoomMode();
		}
	}

	public float MaximumZoom
	{
		get => _maximumZoom;
		set
		{
			_maximumZoom = value;
			UpdateZoomMode();
		}
	}

	public float Zoom
	{
		get => Control.ZoomFactor;
		set => Control.ChangeView(null, null, value, true);
	}

	void UpdateZoomMode()
	{
		Control.MinZoomFactor = _minimumZoom;
		Control.MaxZoomFactor = Math.Max(_minimumZoom, _maximumZoom);
		Control.ZoomMode = _maximumZoom > _minimumZoom ? muc.ZoomMode.Enabled : muc.ZoomMode.Disabled;
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Scrollable.ScrollEvent:
				Control.ViewChanged += (sender, e) => Callback.OnScroll(Widget, new ScrollEventArgs(ScrollPosition));
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
