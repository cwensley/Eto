using System.ComponentModel;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using mui = Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

public interface IWinUIWindow
{
	mui.Window Control { get; }
	mui.FrameworkElement ContainerControl { get; }
}

public class WinUIWindow<TControl, TWidget, TCallback> : WinUIPanel<TControl, TWidget, TCallback>, Window.IHandler, IWinUIWindow//, IInputBindingHost
	where TControl : mui.Window
	where TWidget : Window
	where TCallback : Window.ICallback
{
	// focusable so the window itself can have keyboard focus when no control in it does
	readonly muc.UserControl _root = new() { IsTabStop = false, UseSystemFocusVisuals = false };
	readonly muc.Grid _rootGrid = new();
	readonly muc.TextBlock _title = new();
	readonly muc.CommandBar _titleBar = new();
	muc.MenuBar? _menuBarHost;
	mui.FrameworkElement? _content;
	MenuBar? _menu;
	Eto.Forms.ToolBar? _toolBar;

	bool _closed;
	bool _pendingShow;
	bool _isActive;
	bool _cloaked;
	bool _finishingShow;
	bool _changingState;
	bool _hasShown;
	WindowState _lastReportedState;
	bool _enabled = true;
	bool _autoSize;
	bool _resizable = true;
	bool _maximizable = true;
	bool _minimizable = true;
	bool _closeable = true;
	bool _showInTaskbar = true;
	bool _topmost;
	double _opacity = 1;
	WindowStyle _windowStyle;
	WindowState _windowState;
	WindowState _stateBeforeFullScreen;
	Rectangle? _restoreBounds;
	Point? _location;
	Icon? _icon;
	float _lastScale;

	public override IntPtr NativeHandle => WindowNative.GetWindowHandle(Control);
	public override mui.FrameworkElement ContainerControl => _root;
	public Eto.Forms.ToolBar? ToolBar
	{
		get => _toolBar;
		set
		{
			if (_toolBar?.Handler is ToolBars.ToolBarHandler oldHandler)
			{
				oldHandler.DockChanged -= ToolBar_DockChanged;
				_rootGrid.Children.Remove(oldHandler.Control);
			}
			_toolBar = value;
			if (_toolBar?.Handler is ToolBars.ToolBarHandler handler)
			{
				handler.DockChanged += ToolBar_DockChanged;
				SetToolBarRow(handler);
				_rootGrid.Children.Add(handler.Control);
			}
		}
	}

	void ToolBar_DockChanged(object? sender, EventArgs e)
	{
		if (sender is ToolBars.ToolBarHandler handler)
			SetToolBarRow(handler);
	}

	static void SetToolBarRow(ToolBars.ToolBarHandler handler) => muc.Grid.SetRow(handler.Control, handler.Dock == ToolBarDock.Bottom ? 4 : 2);

	AppWindow AppWindow => Control.AppWindow;

	OverlappedPresenter? Overlapped => AppWindow.Presenter as OverlappedPresenter;

	/// <summary>Physical pixels per logical pixel for this window.</summary>
	float Scale
	{
		get
		{
			var scale = _root.XamlRoot?.RasterizationScale;
			if (scale > 0)
				return (float)scale.Value;
			var dpi = WindowNativeMethods.GetDpiForWindow(NativeHandle);
			return dpi > 0 ? dpi / 96f : 1f;
		}
	}

	public double Opacity
	{
		get => _opacity;
		set
		{
			_opacity = value;
			SetOpacity();
		}
	}

	public string Title
	{
		get => _title.Text;
		set
		{
			_title.Text = value;
			AppWindow.Title = value ?? string.Empty;
		}
	}

	public Screen Screen => Screen.FromRectangle(Bounds);

	Rectangle Bounds => ((Window.IHandler)this).Bounds;

	public MenuBar Menu
	{
		get => _menu!;
		set
		{
			_menu = value;
			if (_menuBarHost != null)
				_rootGrid.Children.Remove(_menuBarHost);

			if (value?.ControlObject is muc.MenuBar menuBar)
			{
				_menuBarHost = menuBar;
				muc.Grid.SetRow(_menuBarHost, 1);
				_rootGrid.Children.Add(_menuBarHost);
			}
			else
				_menuBarHost = null;
		}
	}

	public Icon Icon
	{
		// WinUI only takes an .ico file or HICON for the window icon
		get => _icon!;
		set => _icon = value;
	}

	public bool Resizable
	{
		get => _resizable;
		set
		{
			_resizable = value;
			SetStyle();
		}
	}

	public bool Maximizable
	{
		get => _maximizable;
		set
		{
			_maximizable = value;
			SetStyle();
		}
	}

	public bool Minimizable
	{
		get => _minimizable;
		set
		{
			_minimizable = value;
			SetStyle();
		}
	}

	static readonly object AllowFullScreenToggle_Key = new();

	public bool AllowFullScreenToggle
	{
		get => Widget.Properties.Get<bool>(AllowFullScreenToggle_Key);
		set
		{
			if (Widget.Properties.TrySet(AllowFullScreenToggle_Key, value))
			{
				if (value)
					Widget.KeyDown += Widget_KeyDownFullScreen;
				else
					Widget.KeyDown -= Widget_KeyDownFullScreen;
			}
		}
	}

	void Widget_KeyDownFullScreen(object? sender, KeyEventArgs e)
	{
		if (e.Handled || e.KeyData != Keys.F11)
			return;
		WindowState = WindowState == WindowState.FullScreen ? _stateBeforeFullScreen : WindowState.FullScreen;
		e.Handled = true;
	}

	public bool Closeable
	{
		// the caption buttons are drawn by the system, so the close button can't be hidden on its own
		get => _closeable;
		set => _closeable = value;
	}

	public bool ShowInTaskbar
	{
		get => _showInTaskbar;
		set
		{
			_showInTaskbar = value;
			AppWindow.IsShownInSwitchers = value;
		}
	}

	public bool Topmost
	{
		get => _topmost;
		set
		{
			_topmost = value;
			if (Overlapped is OverlappedPresenter presenter)
				presenter.IsAlwaysOnTop = value;
		}
	}

	public WindowState WindowState
	{
		get
		{
			// minimized windows aren't visible, so this goes by whether it was shown
			if (_closed || !_hasShown)
				return _windowState;
			if (AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
				return WindowState.FullScreen;
			return Overlapped?.State switch
			{
				OverlappedPresenterState.Maximized => WindowState.Maximized,
				OverlappedPresenterState.Minimized => WindowState.Minimized,
				_ => WindowState.Normal
			};
		}
		set
		{
			if (_closed || !_hasShown)
			{
				_windowState = value;
				return;
			}
			ApplyWindowState(value);
		}
	}

	void ApplyWindowState(WindowState value)
	{
		var current = WindowState;
		if (current == value)
			return;
		// going between some states takes more than one step, but should only be reported once
		_changingState = true;
		try
		{
			SetWindowState(current, value);
		}
		finally
		{
			_changingState = false;
		}
		RaiseWindowStateChanged();
	}

	void RaiseWindowStateChanged()
	{
		var state = WindowState;
		if (state == _lastReportedState)
			return;
		_lastReportedState = state;
		Callback.OnWindowStateChanged(Widget, EventArgs.Empty);
	}

	void SetWindowState(WindowState current, WindowState value)
	{
		if (value == WindowState.FullScreen)
		{
			_stateBeforeFullScreen = current;
			if (current == WindowState.Normal)
				_restoreBounds = Bounds;
			AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
			return;
		}

		if (current == WindowState.FullScreen)
		{
			AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
			SetStyle();
		}
		var presenter = Overlapped;
		if (presenter == null)
			return;
		switch (value)
		{
			case WindowState.Maximized:
				if (presenter.State == OverlappedPresenterState.Restored)
					_restoreBounds = Bounds;
				presenter.Maximize();
				break;
			case WindowState.Minimized:
				if (presenter.State == OverlappedPresenterState.Restored)
					_restoreBounds = Bounds;
				presenter.Minimize();
				break;
			default:
				presenter.Restore();
				break;
		}
	}

	public Rectangle RestoreBounds => WindowState == WindowState.Normal ? Bounds : _restoreBounds ?? Bounds;

	public WindowStyle WindowStyle
	{
		get => _windowStyle;
		set
		{
			_windowStyle = value;
			SetStyle();
		}
	}

	public float LogicalPixelSize => Scale;

	public bool MovableByWindowBackground { get; set; }

	public bool AutoSize
	{
		get => _autoSize;
		set
		{
			_autoSize = value;
			if (value && Control.Visible)
				SizeToContent();
		}
	}

	public override Color BackgroundColor
	{
		get => _rootGrid.Background.ToEtoColor();
		set => _rootGrid.Background = value.ToWinUIBrush();
	}

	public override bool Enabled
	{
		get => _enabled;
		set
		{
			_enabled = value;
			if (_closed)
				return;
			WindowNativeMethods.EnableWindow(NativeHandle, value);
			if (value && _isActive)
				EnsureFocus();
		}
	}

	public override bool HasFocus => _isActive && !_closed;

	public override void Focus()
	{
		if (!_closed)
			Control.Activate();
	}

	Point Window.IHandler.Location
	{
		get
		{
			if (_location != null && (_closed || !Control.Visible))
				return _location.Value;
			return Point.Round(ScreenHandler.PhysicalToLogical(AppWindow.Position.ToEto()));
		}
		set
		{
			_location = value;
			if (!_closed)
				MoveTo(value);
		}
	}

	void MoveTo(Point location)
	{
		var physical = ScreenHandler.LogicalToPhysical(location);
		AppWindow.Move(new Windows.Graphics.PointInt32((int)Math.Round(physical.X), (int)Math.Round(physical.Y)));
	}

	Rectangle Window.IHandler.Bounds
	{
		get => new Rectangle(((Window.IHandler)this).Location, Size);
		set
		{
			((Window.IHandler)this).Location = value.Location;
			Size = value.Size;
		}
	}

	public override Size Size
	{
		get
		{
			var user = UserPreferredSize;
			if ((_closed || !Control.Visible) && !double.IsNaN(user.Width) && !double.IsNaN(user.Height))
				return user.ToEtoSize();
			var size = AppWindow.Size;
			return Size.Round(new SizeF(size.Width, size.Height) / Scale);
		}
		set
		{
			UserPreferredSize = new sw.Size(value.Width < 0 ? double.NaN : value.Width, value.Height < 0 ? double.NaN : value.Height);
			ResizeTo(UserPreferredSize);
		}
	}

	public override int Width
	{
		get => Size.Width;
		set
		{
			UserPreferredSize = new sw.Size(value < 0 ? double.NaN : value, UserPreferredSize.Height);
			ResizeTo(UserPreferredSize);
		}
	}

	public override int Height
	{
		get => Size.Height;
		set
		{
			UserPreferredSize = new sw.Size(UserPreferredSize.Width, value < 0 ? double.NaN : value);
			ResizeTo(UserPreferredSize);
		}
	}

	public override Size ClientSize
	{
		get
		{
			var client = AppWindow.ClientSize;
			var size = new SizeF(client.Width, client.Height) / Scale;
			return Size.Round(size - new SizeF(0, (float)ChromeHeight));
		}
		set
		{
			var nonClient = NonClientSize;
			Size = Size.Round(new SizeF(value.Width + nonClient.Width, value.Height + nonClient.Height + (float)ChromeHeight));
		}
	}

	/// <summary>Height of the title and menu bars above the content, in logical pixels.</summary>
	double ChromeHeight
	{
		get
		{
			double height = 0;
			if (_titleBar.Visibility == mux.Visibility.Visible)
				height += MeasureHeight(_titleBar);
			if (_menuBarHost != null)
				height += MeasureHeight(_menuBarHost);
			return height;
		}
	}

	static double MeasureHeight(mux.FrameworkElement element)
	{
		if (element.ActualHeight > 0)
			return element.ActualHeight;
		element.Measure(WinUIConversions.PositiveInfinitySize);
		return element.DesiredSize.Height;
	}

	/// <summary>Size of the window frame around the client area, in logical pixels.</summary>
	SizeF NonClientSize
	{
		get
		{
			var size = AppWindow.Size;
			var client = AppWindow.ClientSize;
			return new SizeF(size.Width - client.Width, size.Height - client.Height) / Scale;
		}
	}

	void ResizeTo(sw.Size size)
	{
		if (_closed)
			return;
		if (double.IsNaN(size.Width) || double.IsNaN(size.Height))
		{
			// the other dimension comes from the content once it's shown
			if (Control.Visible)
				SizeToContent();
			return;
		}
		var scale = Scale;
		AppWindow.Resize(new Windows.Graphics.SizeInt32((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale)));
	}

	public override SizeF GetPreferredSize(SizeF availableSize)
	{
		var user = UserPreferredSize;
		var nonClient = NonClientSize;
		var available = new sw.Size(
			double.IsNaN(user.Width) ? double.PositiveInfinity : Math.Max(0, user.Width - nonClient.Width),
			double.IsNaN(user.Height) ? double.PositiveInfinity : Math.Max(0, user.Height - nonClient.Height));
		_root.Measure(available);
		var desired = _root.DesiredSize;
		if (!_root.IsLoaded && IsPaddingMissing())
		{
			var padding = Padding;
			desired = new sw.Size(desired.Width + padding.Horizontal, desired.Height + padding.Vertical);
		}
		return new SizeF(
			double.IsNaN(user.Width) ? (float)desired.Width + nonClient.Width : (float)user.Width,
			double.IsNaN(user.Height) ? (float)desired.Height + nonClient.Height : (float)user.Height);
	}

	// the padding is template bound, which only takes effect once it's loaded
	bool IsPaddingMissing()
	{
		var border = _content as Controls.EtoContentControl
			?? (_content as muc.Panel)?.Children.OfType<Controls.EtoContentControl>().FirstOrDefault();
		if (border == null)
			return false;
		var padding = border.Padding;
		if (padding == default)
			return false;
		var templateRoot = border.ApplyTemplate() || muxm.VisualTreeHelper.GetChildrenCount(border) > 0
			? muxm.VisualTreeHelper.GetChild(border, 0) as muc.Border
			: null;
		return templateRoot == null || templateRoot.Padding != padding;
	}

	void SizeToContent()
	{
		var user = UserPreferredSize;
		if (!_autoSize && !double.IsNaN(user.Width) && !double.IsNaN(user.Height))
			return;
		var size = GetPreferredSize(SizeF.PositiveInfinity);
		var scale = Scale;
		AppWindow.Resize(new Windows.Graphics.SizeInt32((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale)));
	}

	protected override TControl CreateControl() => (TControl)new mui.Window();

	public void BringToFront()
	{
		if (!_closed)
			AppWindow.MoveInZOrderAtTop();
	}

	public void Close()
	{
		if (!_closed)
			Control.Close();
	}

	public void SendToBack()
	{
		if (!_closed)
			AppWindow.MoveInZOrderAtBottom();
	}

	public void SetOwner(Window owner)
	{
		if (_closed)
			return;
		// an owned window stays above its owner and minimizes with it
		var ownerHandle = owner?.NativeHandle ?? IntPtr.Zero;
		WindowNativeMethods.SetWindowLongPtr(NativeHandle, WindowNativeMethods.GWLP_HWNDPARENT, ownerHandle);
	}

	public override void SetContainerContent(mui.FrameworkElement content)
	{
		if (_content != null)
			_rootGrid.Children.Remove(_content);
		_content = content;
		muc.Grid.SetRow(content, 3);
		_rootGrid.Children.Add(content);
	}

	public override bool Visible
	{
		get => !_closed && Control.Visible;
		set
		{
			if (_closed)
				return;
			if (value)
			{
				if (!Control.Visible)
					AppWindow.Show(true);
			}
			else
				AppWindow.Hide();
		}
	}

	mui.Window IWinUIWindow.Control => Control;

	protected virtual bool IsAttached => false;

	protected override void Initialize()
	{
		base.Initialize();

		Control.Closed += Control_Closed;
		Control.Activated += Control_Activated;
		Control.VisibilityChanged += Control_VisibilityChanged;
		AppWindow.Closing += AppWindow_Closing;

		// a native window wrapped by WinUIHelpers.ToEtoWindow keeps its own content
		if (IsAttached)
			return;

		Control.ExtendsContentIntoTitleBar = true;

		// a ThemeResource can only be referenced from XAML, and it lets the background follow the window's theme
		_rootGrid.Style = (mui.Style)mui.Markup.XamlReader.Load(
			"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Grid'>" +
			"<Setter Property='Background' Value='{ThemeResource ApplicationPageBackgroundThemeBrush}'/></Style>");

		// Define rows: title, menu, top toolbar, content, bottom toolbar
		_rootGrid.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		_rootGrid.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		_rootGrid.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		_rootGrid.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Star) });
		_rootGrid.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });

		// add title to the toolbar
		_title.FontSize = 14;
		_title.Margin = new mui.Thickness(8);

		_titleBar.Content = _title;
		// the title bar only shows the title, so keep it out of the focus order
		_titleBar.IsTabStop = false;
		_titleBar.OverflowButtonVisibility = muc.CommandBarOverflowButtonVisibility.Collapsed;

		muc.Grid.SetRow(_titleBar, 0);
		_rootGrid.Children.Add(_titleBar);

		_root.Content = _rootGrid;
		_root.LostFocus += Root_LostFocus;
		_root.Loaded += Root_Loaded;
		Control.Content = _root;
		ApplicationHandler.ApplyTheme(this, ApplicationHandler.ElementTheme);
	}

	void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
	{
		// only for the user closing it, Close() still works
		if (!_closeable)
			args.Cancel = true;
	}

	/// <summary>
	/// Shows the window, sizing it to its content first when needed.
	/// </summary>
	protected void ShowWindow(bool activate)
	{
		if (_closed)
			return;
		if (Control.Visible)
		{
			if (activate)
				Control.Activate();
			return;
		}

		_pendingShow = true;
		_hasShown = true;
		// hidden until it's sized to its content, so it doesn't show at the wrong size first
		SetCloaked(true);
		// size it now too, so the first layout (and paint) isn't at the default window size
		SizeToContent();
		if (_location != null)
			MoveTo(_location.Value);
		else
			CenterWindow();

		if (activate)
			Control.Activate();
		else
			AppWindow.Show(false);

		if (_pendingShow && _root.IsLoaded)
			OnShowLoaded();
	}

	void CenterWindow()
	{
		// centered on the owner, or the screen with the mouse when there isn't one
		var size = GetPreferredSize(SizeF.PositiveInfinity);
		var owner = Widget.Owner;
		RectangleF area;
		if (owner != null && owner.Loaded)
			area = owner.Bounds;
		else
			area = (Screen.FromPoint(Mouse.Position) ?? Screen.PrimaryScreen).WorkingArea;
		var location = new PointF(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2);
		MoveTo(Point.Round(location));
	}

	void Root_Loaded(object sender, mui.RoutedEventArgs e)
	{
		if (_pendingShow)
			OnShowLoaded();
	}

	void OnShowLoaded()
	{
		_pendingShow = false;
		SizeToContent();
		ApplyWindowState(_windowState);
		SetOpacity();

		// wait for the layout at the new size, so controls already have their final size in LoadComplete
		_finishingShow = true;
		var target = _root.XamlRoot?.Size;
		if (target != null && (Math.Abs(_root.ActualWidth - target.Value.Width) > 0.5 || Math.Abs(_root.ActualHeight - target.Value.Height) > 0.5))
		{
			_root.SizeChanged += Root_SizeChangedWhileShowing;
			// in case it never gets that size, e.g. when it's maximized or minimized instead
			var timer = Control.DispatcherQueue.CreateTimer();
			timer.Interval = TimeSpan.FromMilliseconds(250);
			timer.IsRepeating = false;
			timer.Tick += (sender, e) => FinishShow();
			timer.Start();
			_finishShowTimer = timer;
			return;
		}
		FinishShow();
	}

	mud.DispatcherQueueTimer? _finishShowTimer;

	void Root_SizeChangedWhileShowing(object sender, mux.SizeChangedEventArgs e) => FinishShow();

	void FinishShow()
	{
		if (!_finishingShow)
			return;
		_finishingShow = false;
		_root.SizeChanged -= Root_SizeChangedWhileShowing;
		_finishShowTimer?.Stop();
		_finishShowTimer = null;
		if (_closed || Widget.IsDisposed)
			return;
		Callback.OnLoadComplete(Widget, EventArgs.Empty);

		SetCloaked(false);
		RaiseShown();
	}

	void RaiseShown()
	{
		// after the layout and render that follow showing it, like WPF
		Control.DispatcherQueue.TryEnqueue(mud.DispatcherQueuePriority.Low, () =>
		{
			if (_closed || Widget.IsDisposed || !Control.Visible)
				return;
			EnsureFocus();
			Callback.OnShown(Widget, EventArgs.Empty);
		});
	}

	void Control_VisibilityChanged(object sender, mui.WindowVisibilityChangedEventArgs args)
	{
		// the first show is raised once it's loaded and sized
		if (args.Visible && !_pendingShow)
			RaiseShown();
	}

	void Control_Activated(object sender, mui.WindowActivatedEventArgs args)
	{
		var wasActive = _isActive;
		_isActive = args.WindowActivationState != mui.WindowActivationState.Deactivated;
		if (_isActive == wasActive || Widget.IsDisposed)
			return;
		if (_isActive)
		{
			EnsureFocus();
			Callback.OnGotFocus(Widget, EventArgs.Empty);
		}
		else
		{
			// closing and disposing in one go must not raise it on a disposed window
			Application.Instance.AsyncInvoke(() =>
			{
				if (!Widget.IsDisposed)
					Callback.OnLostFocus(Widget, EventArgs.Empty);
			});
		}
	}

	/// <summary>
	/// Gives the window itself keyboard focus when nothing in it has it, otherwise it gets no key input at all.
	/// </summary>
	void EnsureFocus()
	{
		if (_closed || !_enabled || !_isActive || _root.XamlRoot == null)
			return;
		var focused = mux.Input.FocusManager.GetFocusedElement(_root.XamlRoot);
		if (focused != null)
			return;
		_root.IsTabStop = true;
		_root.Focus(mux.FocusState.Programmatic);
	}

	void Root_LostFocus(object sender, mui.RoutedEventArgs e)
	{
		// only focusable while it has focus, so it isn't a stop when tabbing through the controls
		_root.IsTabStop = false;
	}

	void Control_Closed(object sender, mui.WindowEventArgs args)
	{
		if (_closed)
			return;
		var closing = new CancelEventArgs();
		if (!Widget.IsDisposed)
			Callback.OnClosing(Widget, closing);
		if (closing.Cancel)
		{
			args.Handled = true;
			return;
		}
		_windowState = WindowState;
		_closed = true;
		_isActive = false;
		// Windows destroys owned windows with their owner without WinUI knowing, so close them properly first
		foreach (var owned in Application.Instance?.Windows.Where(r => r.Owner == Widget).ToList() ?? [])
			owned.Close();
		// WinUI quits when its last window closes, which would never happen with the hidden measuring window open
		if (Widget == Application.Instance?.MainForm)
			MeasureHost.Shutdown();
		OnClosed();
		Callback.OnClosed(Widget, EventArgs.Empty);
	}

	/// <summary>Called when the window has closed, before the Closed event.</summary>
	protected virtual void OnClosed()
	{
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !_closed && Control != null)
			Control.Close();
		base.Dispose(disposing);
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Eto.Forms.Control.ShownEvent:
			case Window.ClosedEvent:
			case Window.ClosingEvent:
			case Eto.Forms.Control.GotFocusEvent:
			case Eto.Forms.Control.LostFocusEvent:
				// always raised
				break;
			case Window.LocationChangedEvent:
			case Eto.Forms.Control.SizeChangedEvent:
			case Window.WindowStateChangedEvent:
				AppWindow.Changed -= AppWindow_Changed;
				AppWindow.Changed += AppWindow_Changed;
				break;
			case Window.LogicalPixelSizeChangedEvent:
				_lastScale = Scale;
				_root.Loaded += (sender, e) =>
				{
					if (_root.XamlRoot != null)
					{
						_root.XamlRoot.Changed -= XamlRoot_Changed;
						_root.XamlRoot.Changed += XamlRoot_Changed;
					}
				};
				break;
			case Window.PreviewKeyDownEvent:
				_root.PreviewKeyDown += Root_PreviewKeyDown;
				break;
			case Window.PreviewKeyUpEvent:
				_root.PreviewKeyUp += Root_PreviewKeyUp;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	public override bool DetachEvent(string id)
	{
		switch (id)
		{
			case Window.PreviewKeyDownEvent:
				_root.PreviewKeyDown -= Root_PreviewKeyDown;
				return true;
			case Window.PreviewKeyUpEvent:
				_root.PreviewKeyUp -= Root_PreviewKeyUp;
				return true;
			default:
				return base.DetachEvent(id);
		}
	}

	void Root_PreviewKeyDown(object sender, mux.Input.KeyRoutedEventArgs e)
	{
		var args = e.ToEto(KeyEventType.KeyDown);
		if (args.KeyData != Keys.None)
			Callback.OnPreviewKeyDown(Widget, new KeyMonitorEventArgs(args.KeyData, KeyEventType.KeyDown));
	}

	void Root_PreviewKeyUp(object sender, mux.Input.KeyRoutedEventArgs e)
	{
		var args = e.ToEto(KeyEventType.KeyUp);
		if (args.KeyData != Keys.None)
			Callback.OnPreviewKeyUp(Widget, new KeyMonitorEventArgs(args.KeyData, KeyEventType.KeyUp));
	}

	void XamlRoot_Changed(mux.XamlRoot sender, mux.XamlRootChangedEventArgs args)
	{
		var scale = Scale;
		if (scale != _lastScale)
		{
			_lastScale = scale;
			Callback.OnLogicalPixelSizeChanged(Widget, EventArgs.Empty);
		}
	}

	void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
	{
		if (Widget.IsDisposed)
			return;
		if (args.DidPositionChange)
			Callback.OnLocationChanged(Widget, EventArgs.Empty);
		if (args.DidSizeChange)
			Callback.OnSizeChanged(Widget, EventArgs.Empty);
		if (args.DidPresenterChange && !_changingState)
			RaiseWindowStateChanged();
	}

	void SetStyle()
	{
		if (_closed)
			return;
		var presenter = Overlapped;
		if (presenter == null)
			return;
		var hasBorder = _windowStyle != WindowStyle.None;
		presenter.SetBorderAndTitleBar(hasBorder, hasBorder);
		presenter.IsResizable = _resizable;
		presenter.IsMaximizable = _maximizable && _windowStyle == WindowStyle.Default;
		presenter.IsMinimizable = _minimizable && _windowStyle == WindowStyle.Default;
		presenter.IsAlwaysOnTop = _topmost;
		_titleBar.Visibility = hasBorder ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	void SetOpacity()
	{
		if (_closed)
			return;
		var hwnd = NativeHandle;
		var exStyle = WindowNativeMethods.GetWindowLongPtr(hwnd, WindowNativeMethods.GWL_EXSTYLE).ToInt64();
		if (_opacity >= 1)
		{
			if ((exStyle & WindowNativeMethods.WS_EX_LAYERED) != 0)
				WindowNativeMethods.SetWindowLongPtr(hwnd, WindowNativeMethods.GWL_EXSTYLE, (IntPtr)(exStyle & ~WindowNativeMethods.WS_EX_LAYERED));
			return;
		}
		WindowNativeMethods.SetWindowLongPtr(hwnd, WindowNativeMethods.GWL_EXSTYLE, (IntPtr)(exStyle | WindowNativeMethods.WS_EX_LAYERED));
		WindowNativeMethods.SetLayeredWindowAttributes(hwnd, 0, (byte)Math.Round(Math.Max(0, _opacity) * 255), WindowNativeMethods.LWA_ALPHA);
	}

	void SetCloaked(bool cloaked)
	{
		if (_cloaked == cloaked || _closed)
			return;
		_cloaked = cloaked;
		int value = cloaked ? 1 : 0;
		WindowNativeMethods.DwmSetWindowAttribute(NativeHandle, WindowNativeMethods.DWMWA_CLOAK, ref value, sizeof(int));
	}
}
