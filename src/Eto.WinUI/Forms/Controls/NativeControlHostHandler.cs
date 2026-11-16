namespace Eto.WinUI.Forms.Controls;

public class NativeControlHostHandler : WinUIFrameworkElement<mux.FrameworkElement, NativeControlHost, NativeControlHost.ICallback>, NativeControlHost.IHandler
{
	readonly EtoContentControl _container;

	public NativeControlHostHandler()
	{
		_container = new EtoContentControl { Handler = this };
	}

	public NativeControlHostHandler(mux.FrameworkElement nativeControl)
		: this()
	{
		Control = nativeControl;
		_container.Content = nativeControl;
	}

	public override mux.FrameworkElement ContainerControl
	{
		get
		{
			// created lazily, as subclasses provide their control through OnCreateNativeControl
			if (_container.Content == null)
				_container.Content = Control;
			return _container;
		}
	}

	// an empty host has nothing to measure, so give it a sane default like the other platforms
	protected override sw.Size DefaultSize => Control is EmptyHost ? new sw.Size(100, 100) : base.DefaultSize;

	public override Color BackgroundColor
	{
		get => _container.Background.ToEtoColor();
		set => _container.Background = value.ToWinUIBrush();
	}

	protected override mux.FrameworkElement CreateControl()
	{
		var args = new CreateNativeControlArgs();
		Callback.OnCreateNativeControl(Widget, args);
		return CreateHost(args.NativeControl);
	}

	public void Create(object nativeControl)
	{
		Control = CreateHost(nativeControl);
		_container.Content = Control;
	}

	static mux.FrameworkElement CreateHost(object? nativeControl) => nativeControl switch
	{
		null => new EmptyHost(),
		mux.FrameworkElement element => element,
		// WinUI has no equivalent of HwndHost to embed a win32 child window
		_ => throw new NotSupportedException($"Native control of type {nativeControl.GetType()} is not supported by this platform")
	};

	class EmptyHost : muc.Grid
	{
	}
}
