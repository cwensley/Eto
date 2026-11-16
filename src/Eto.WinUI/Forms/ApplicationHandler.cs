using System.Runtime.ExceptionServices;

namespace Eto.WinUI.Forms;

public class ApplicationHandler : WidgetHandler<mux.Application, Application, Application.ICallback>, Application.IHandler
{
	mud.DispatcherQueue? _dispatcher;
	Thread? _mainThread;

	public bool QuitIsSupported => true;
	public Keys CommonModifier => Keys.Control;
	public Keys AlternateModifier => Keys.Alt;
	public string? BadgeLabel { get; set; }
	public bool IsActive { get; }

	public ApplicationHandler()
	{
		Control = mux.Application.Current;
	}

	public void AsyncInvoke(Action action)
	{
		_dispatcher.TryEnqueue(new mud.DispatcherQueueHandler(action));
	}

	public void Attach(object context)
	{
		Control = context as mux.Application;
		InitializeApplication();
	}

	void InitializeApplication()
	{
		_dispatcher ??= mud.DispatcherQueue.GetForCurrentThread();
		_mainThread = Thread.CurrentThread;

		Control.Resources.MergedDictionaries.Add(
			new mux.ResourceDictionary
			{

				Source = new Uri("ms-appx:///Eto.WinUI/Forms/Controls/BindingTemplates.xaml")
			});

		// create it now, as it only goes live once the dispatcher gets to it
		MeasureHost.Prepare();

		Callback.OnInitialized(Widget, EventArgs.Empty);
	}

	protected override void Initialize()
	{
		base.Initialize();
		_dispatcher = mud.DispatcherQueue.GetForCurrentThread();
		_mainThread = Thread.CurrentThread;
	}

	public void Invoke(Action action)
	{

		if (_dispatcher == null || Thread.CurrentThread == _mainThread)
			action();
		else
		{
			Exception? exception = null;
			using var mre = new ManualResetEvent(false);
			_dispatcher.TryEnqueue(() =>
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					exception = ex;
				}
				finally
				{
					mre.Set();
				}
			});
			mre.WaitOne();
			// rethrow on the calling thread; left on the dispatcher it would end the app instead
			if (exception != null)
				ExceptionDispatchInfo.Capture(exception).Throw();
		}
	}

	public void OnMainFormChanged()
	{
		//mux.Application.Current..MainWindow = Widget.MainForm.ToNative();
	}

	public void Open(string url)
	{
		Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	}

	public void Quit()
	{
		Control.Exit();
	}

	public void Restart()
	{
	}

	public void Run()
	{
		if (Control != null)
		{
			Callback.OnInitialized(Widget, EventArgs.Empty);
			return;
		}

		// No WinUI Application exists yet, so Eto owns startup like a XAML-generated Main would.
		WinRT.ComWrappersSupport.InitializeComWrappers();
		mux.Application.Start(p =>
		{
			_dispatcher = mud.DispatcherQueue.GetForCurrentThread();
			SynchronizationContext.SetSynchronizationContext(new mud.DispatcherQueueSynchronizationContext(_dispatcher));
			Control = new EtoApplication(this);
		});
	}

	internal void OnLaunched()
	{
		// Eto apps quit explicitly or via the main form, not when the last window closes
		if (Widget.MainForm == null)
			Control.DispatcherShutdownMode = mux.DispatcherShutdownMode.OnExplicitShutdown;
		InitializeApplication();
	}

	public void RunIteration()
	{
		//var frame = new DispatcherFrame();
		//Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
		//Dispatcher.PushFrame(frame);
		//WpfFrameworkElementHelper.ShouldCaptureMouse = false;
	}

	Theme? _theme;
	Windows.UI.ViewManagement.UISettings? _uiSettings;
	ThemeStyle? _lastSystemThemeStyle;

	public Theme Theme
	{
		get => _theme ??= Themes.System;
		set
		{
			_theme = value ?? Themes.System;
			ApplyTheme();
			Callback.OnThemeChanged(Widget, EventArgs.Empty);
		}
	}

	/// <summary>
	/// Gets the theme to request on window content so it follows <see cref="Application.Theme"/>.
	/// </summary>
	internal static mux.ElementTheme ElementTheme => ThemeHandler.GetElementTheme((Application.Instance?.Handler as ApplicationHandler)?._theme);

	/// <summary>
	/// Gets whether the application currently renders light or dark.
	/// </summary>
	internal static ThemeStyle CurrentThemeStyle => ElementTheme switch
	{
		mux.ElementTheme.Light => ThemeStyle.Light,
		mux.ElementTheme.Dark => ThemeStyle.Dark,
		_ => ThemeHandler.SystemThemeStyle
	};

	void ApplyTheme()
	{
		var elementTheme = ElementTheme;
		foreach (var window in Widget.Windows)
		{
			if (window.Handler is IWinUIWindow handler)
				ApplyTheme(handler, elementTheme);
		}
	}

	internal static void ApplyTheme(IWinUIWindow window, mux.ElementTheme elementTheme)
	{
		if (window.Control.Content is mux.FrameworkElement content)
			content.RequestedTheme = elementTheme;
		// caption buttons are drawn by the system so don't follow the content's theme on their own
		window.Control.AppWindow.TitleBar.PreferredTheme = elementTheme switch
		{
			mux.ElementTheme.Light => mu.Windowing.TitleBarTheme.Light,
			mux.ElementTheme.Dark => mu.Windowing.TitleBarTheme.Dark,
			_ => mu.Windowing.TitleBarTheme.UseDefaultAppMode
		};
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Application.ThemeChangedEvent:
				// also raised by the Theme setter; this adds changes to the Windows light/dark setting
				_lastSystemThemeStyle = ThemeHandler.SystemThemeStyle;
				_uiSettings = new Windows.UI.ViewManagement.UISettings();
				_uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void UISettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
	{
		// raised on a background thread, and for accent color changes too
		_dispatcher?.TryEnqueue(() =>
		{
			var style = ThemeHandler.SystemThemeStyle;
			if (style == _lastSystemThemeStyle)
				return;
			_lastSystemThemeStyle = style;
			if (ElementTheme == mux.ElementTheme.Default)
				Callback.OnThemeChanged(Widget, EventArgs.Empty);
		});
	}
}

/// <summary>
/// The WinUI application Eto creates when <see cref="Application.Run()"/> is called without one.
/// </summary>
partial class EtoApplication : mux.Application, mux.Markup.IXamlMetadataProvider
{
	readonly ApplicationHandler _handler;
	// Eto.WinUI's generated provider also covers WinUI's own controls and the toolkit controls Eto uses.
	// Created by name as it doesn't exist yet when the XAML compiler first compiles this assembly.
	readonly mux.Markup.IXamlMetadataProvider _provider = (mux.Markup.IXamlMetadataProvider)Activator.CreateInstance(
		typeof(EtoApplication).Assembly.GetType("Eto.WinUI.Eto_WinUI_XamlTypeInfo.XamlMetaDataProvider", true)!)!;

	public EtoApplication(ApplicationHandler handler)
	{
		_handler = handler;
	}

	protected override void OnLaunched(mux.LaunchActivatedEventArgs args)
	{
		Resources.MergedDictionaries.Add(new muc.XamlControlsResources());
		_handler.OnLaunched();
	}

	public mux.Markup.IXamlType GetXamlType(Type type) => _provider.GetXamlType(type);

	public mux.Markup.IXamlType GetXamlType(string fullName) => _provider.GetXamlType(fullName);

	public mux.Markup.XmlnsDefinition[] GetXmlnsDefinitions() => _provider.GetXmlnsDefinitions();
}
