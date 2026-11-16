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
	readonly muc.Grid _rootGrid = new();
	readonly muc.TextBlock _title = new();
	readonly muc.CommandBar _titleBar = new();
	muc.MenuBar? _menuBarHost;
	mui.FrameworkElement? _content;
	MenuBar? _menu;
	Eto.Forms.ToolBar? _toolBar;

	public override IntPtr NativeHandle => WindowNative.GetWindowHandle(Control);
	public override mui.FrameworkElement ContainerControl => _rootGrid;
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

	public double Opacity { get; set; }
	public string Title
	{
		get => _title.Text;
		set => _title.Text = value;	
	}
	public Screen Screen { get; }
	public MenuBar Menu
	{
		get => _menu;
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
	public Icon Icon { get; set; }
	public bool Resizable { get; set; }
	public bool Maximizable { get; set; }
	public bool AllowFullScreenToggle { get; set; }
	public bool Minimizable { get; set; }
	public bool Closeable { get; set; }
	public bool ShowInTaskbar { get; set; }
	public bool Topmost { get; set; }
	public WindowState WindowState { get; set; }
	public Rectangle RestoreBounds { get; }
	public WindowStyle WindowStyle { get; set; }
	public float LogicalPixelSize { get; }
	public bool MovableByWindowBackground { get; set; }
	public bool AutoSize { get; set; }

	public override Color BackgroundColor
	{
		get => _rootGrid.Background.ToEtoColor();
		set => _rootGrid.Background = value.ToWinUIBrush();
	}

	Point Window.IHandler.Location
	{
		get => Control.AppWindow.Position.ToEto();
		set => Control.AppWindow.Move(value.ToWinUIPointInt32());
	}

	Rectangle Window.IHandler.Bounds
	{
		get => new Rectangle(Control.AppWindow.Position.ToEto(), new Size(Control.AppWindow.Size.Width, Control.AppWindow.Size.Height));
		// AppWindow.MoveAndResize sets the position and size in a single operation.
		set => Control.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(value.X, value.Y, value.Width, value.Height));
	}

	protected override TControl CreateControl() => (TControl)new mui.Window();

	public void BringToFront() => Control.AppWindow.MoveInZOrderAtTop();

	public void Close() => Control.Close();

	public void SendToBack() => Control.AppWindow.MoveInZOrderAtBottom();

	public void SetOwner(Window owner)
	{
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
		get => Control.Visible;
		set
		{
			if (value)
				Control.AppWindow.Show();
			else
				Control.AppWindow.Hide();
		}
	}

	mui.Window IWinUIWindow.Control => Control;

	protected override void Initialize()
	{
		base.Initialize();

		Control.ExtendsContentIntoTitleBar = true;

		_rootGrid.Background = mux.Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] as muxm.Brush;

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


		muc.Grid.SetRow(_titleBar, 0);
		_rootGrid.Children.Add(_titleBar);

		Control.Content = _rootGrid;

	}

}
