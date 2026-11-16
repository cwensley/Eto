using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace Eto.WinUI.Forms.Controls;

public class WebViewHandler : WinUIFrameworkElement<muc.WebView2, WebView, WebView.ICallback>, WebView.IHandler
{
	public override mux.FrameworkElement ContainerControl => Control;

	const string ResourceScheme = "eto";

	bool _ready;
	List<Action>? _delayedActions;
	Uri? _pendingUrl;
	Dictionary<string, Func<WebViewResourceRequest, WebViewResourceResponse>>? _resourceHandlers;
	HashSet<string>? _filteredHosts;
	bool _resourceRequestsHooked;
	bool _browserContextMenuEnabled = true;
	Color? _backgroundColor;

	/// <summary>
	/// Gets or sets the environment to create web views with, or null to use one Eto creates.
	/// </summary>
	/// <remarks>
	/// Set before any web view is created. An environment you create must register <see cref="CreateResourceSchemeRegistration"/>
	/// for <see cref="WebView.AddResourceHandler"/> to serve eto:// addresses; otherwise it serves https:// instead.
	/// </remarks>
	public static CoreWebView2Environment? CoreWebView2Environment { get; set; }

	/// <summary>
	/// Set to true when <see cref="CoreWebView2Environment"/> registers <see cref="CreateResourceSchemeRegistration"/>.
	/// </summary>
	public static bool CustomEnvironmentHasResourceScheme { get; set; }

	/// <summary>
	/// Creates the registration for the eto:// scheme served by <see cref="WebView.AddResourceHandler"/>.
	/// </summary>
	public static CoreWebView2CustomSchemeRegistration CreateResourceSchemeRegistration() => new CoreWebView2CustomSchemeRegistration(ResourceScheme)
	{
		// projected from a BOOL, hence int
		TreatAsSecure = 1,
		HasAuthorityComponent = true
	};

	static string? s_userDataFolder;

	/// <summary>
	/// Gets or sets the folder Eto's environment stores browser data in, defaulting to <c>%LOCALAPPDATA%\&lt;Company&gt;\&lt;Product&gt;\WebView2</c>.
	/// </summary>
	public static string? UserDataFolder
	{
		get => s_userDataFolder ??= GetDefaultUserDataFolder();
		set => s_userDataFolder = value;
	}

	static Task<CoreWebView2Environment>? s_defaultEnvironment;

	// Schemes can only be registered when an environment is created, so Eto creates the default one itself.
	static Task<CoreWebView2Environment> GetDefaultEnvironmentAsync()
	{
		if (s_defaultEnvironment == null)
		{
			var options = new CoreWebView2EnvironmentOptions();
			// the getter returns a copy, so assign the whole list
			options.CustomSchemeRegistrations = new List<CoreWebView2CustomSchemeRegistration> { CreateResourceSchemeRegistration() };
			s_defaultEnvironment = CoreWebView2Environment.CreateWithOptionsAsync(null, UserDataFolder, options).AsTask();
		}
		return s_defaultEnvironment;
	}

	static string? GetDefaultUserDataFolder()
	{
		try
		{
			// WebView2's own default is next to the .exe, which isn't writable for installed apps.
			string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName;
			var info = FileVersionInfo.GetVersionInfo(exePath);
			var company = SanitizeFolderName(info.CompanyName);
			var product = SanitizeFolderName(info.ProductName) ?? Path.GetFileNameWithoutExtension(exePath);
			var appFolder = company == null || company == product ? product : Path.Combine(company, product);
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appFolder, "WebView2");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Could not determine the WebView2 user data folder, using the WebView2 default: {ex}");
			return null;
		}
	}

	static string? SanitizeFolderName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return null;
		foreach (var ch in Path.GetInvalidFileNameChars())
			name = name.Replace(ch, '_');
		name = name.Trim().TrimEnd('.');
		return name.Length > 0 ? name : null;
	}

	protected override muc.WebView2 CreateControl() => new();

	protected override sw.Size DefaultSize => new sw.Size(100, 100);

	protected override void Initialize()
	{
		base.Initialize();
		Control.CoreWebView2Initialized += Control_CoreWebView2Initialized;
		Control.NavigationStarting += Control_NavigationStarting;
		Control.NavigationCompleted += Control_NavigationCompleted;
		Control.WebMessageReceived += Control_WebMessageReceived;
	}

	protected override void OnInitializeComplete()
	{
		base.OnInitializeComplete();
		// Wait until shown, as a pending initialization would keep a web view that's never shown alive.
		// This is also after styles are applied, as they might set the environment.
		if (Control.IsLoaded)
			InitializeAsync();
		else
			Control.Loaded += Control_LoadedInitialize;
	}

	void Control_LoadedInitialize(object sender, mux.RoutedEventArgs e)
	{
		Control.Loaded -= Control_LoadedInitialize;
		InitializeAsync();
	}

	public override SizeF GetPreferredSize(SizeF availableSize)
	{
		// apply the default and user size, as WebView2 has no size of its own
		return MeasureOverride(availableSize.ToWinUI(), size =>
		{
			Control.Measure(size);
			return Control.DesiredSize;
		}).ToEto();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// shuts down the browser for this view now rather than when collected
			Control.Loaded -= Control_LoadedInitialize;
			Control.Close();
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Override to initialize WebView2 yourself.
	/// </summary>
	protected virtual async Task OnInitializeWebView2Async()
	{
		var env = CoreWebView2Environment ?? await GetDefaultEnvironmentAsync();
		await Control.EnsureCoreWebView2Async(env);
	}

	async void InitializeAsync()
	{
		try
		{
			await OnInitializeWebView2Async();
		}
		catch (Exception ex)
		{
			// Initialization is aborted when the control is torn down first, which mustn't crash the app.
			if (!Widget.IsDisposed)
				Debug.WriteLine($"Failed to initialize WebView2: {ex}");
		}
	}

	bool UsesResourceScheme => CoreWebView2Environment == null || CustomEnvironmentHasResourceScheme;

	void Control_CoreWebView2Initialized(muc.WebView2 sender, muc.CoreWebView2InitializedEventArgs args)
	{
		if (args.Exception != null || Widget.IsDisposed)
			return;

		var core = TryGetCoreWebView2();
		if (core == null)
			return;

		core.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
		core.NewWindowRequested += CoreWebView2_NewWindowRequested;
		core.ContentLoading += CoreWebView2_ContentLoading;
		core.Settings.AreDefaultContextMenusEnabled = _browserContextMenuEnabled;
		// Gives pages the same window.eto.postMessage as other platforms, before any of their own scripts run.
		_ = core.AddScriptToExecuteOnDocumentCreatedAsync("window.eto = { postMessage: function(message) { window.chrome.webview.postMessage(message); } };");

		// Hosts must be intercepted before navigating to a Url set while initializing.
		if (_resourceHandlers != null)
		{
			foreach (var host in _resourceHandlers.Keys)
				UpdateResourceFilter(core, host);
		}

		_ready = true;
		var actions = _delayedActions;
		_delayedActions = null;
		if (actions != null)
		{
			foreach (var action in actions)
				action();
		}
	}

	void RunWhenReady(Action action)
	{
		if (_ready)
		{
			action();
			return;
		}
		_delayedActions ??= new List<Action>();
		_delayedActions.Add(action);
	}

	CoreWebView2? TryGetCoreWebView2()
	{
		try
		{
			return Control.CoreWebView2;
		}
		catch (Exception)
		{
			// the browser process has gone away
			return null;
		}
	}

	protected CoreWebView2? CoreWebView2 => _ready ? TryGetCoreWebView2() : null;

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case WebView.NavigatedEvent:
			case WebView.DocumentLoadedEvent:
			case WebView.DocumentLoadingEvent:
			case WebView.OpenNewWindowEvent:
			case WebView.DocumentTitleChangedEvent:
			case WebView.MessageReceivedEvent:
				// always hooked
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void CoreWebView2_ContentLoading(CoreWebView2 sender, CoreWebView2ContentLoadingEventArgs args)
	{
		Callback.OnNavigated(Widget, new WebViewLoadedEventArgs(Url));
	}

	void Control_NavigationStarting(muc.WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
	{
		if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
			return;
		var e = new WebViewLoadingEventArgs(uri, true);
		Callback.OnDocumentLoading(Widget, e);
		args.Cancel = e.Cancel;
	}

	void Control_NavigationCompleted(muc.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
	{
		var e = new WebViewLoadedEventArgs(Url);
		// raised after the navigation fully settles, like other platforms
		Application.Instance.AsyncInvoke(() =>
		{
			if (!Widget.IsDisposed)
				Callback.OnDocumentLoaded(Widget, e);
		});
	}

	void Control_WebMessageReceived(muc.WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
	{
		string? content;
		try
		{
			content = args.TryGetWebMessageAsString();
		}
		catch (ArgumentException)
		{
			// not a string, so pass the json
			content = null;
		}
		if (string.IsNullOrEmpty(content))
			content = args.WebMessageAsJson;
		if (content == null)
			return;
		Callback.OnMessageReceived(Widget, new WebViewMessageEventArgs(content));
	}

	void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
	{
		if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
			return;
		var e = new WebViewNewWindowEventArgs(uri, null);
		Callback.OnOpenNewWindow(Widget, e);
		args.Handled = e.Cancel;
	}

	void CoreWebView2_DocumentTitleChanged(CoreWebView2 sender, object args)
	{
		Callback.OnDocumentTitleChanged(Widget, new WebViewTitleEventArgs(sender.DocumentTitle));
	}

	public Uri? Url
	{
		get => _ready ? Control.Source : _pendingUrl;
		set
		{
			// Setting Source before initializing would create WebView2's default environment instead of ours.
			if (!_ready)
			{
				_pendingUrl = value;
				RunWhenReady(() =>
				{
					if (_pendingUrl == value)
					{
						_pendingUrl = null;
						Control.Source = value;
					}
				});
				return;
			}
			Control.Source = value;
		}
	}

	public string? DocumentTitle => CoreWebView2?.DocumentTitle;

	static string WrapScript(string script) => $"var _fn = function() {{ {script} }}; _fn();";

	public string? ExecuteScript(string script)
	{
		var task = ExecuteScriptAsync(script);
		// Eto's API is synchronous, so pump messages until the script (or initialization) finishes.
		while (!task.IsCompleted)
		{
			if (Widget.IsDisposed || (_ready && !Widget.Loaded))
				return null;
			ShellNative.PumpMessages(10);
		}
		return task.GetAwaiter().GetResult();
	}

	public async Task<string?> ExecuteScriptAsync(string script)
	{
		if (!_ready)
		{
			var tcs = new TaskCompletionSource<bool>();
			RunWhenReady(() => tcs.TrySetResult(true));
			await tcs.Task;
		}
		var result = await Control.ExecuteScriptAsync(WrapScript(script));
		return Decode(result);
	}

	static string? Decode(string? result)
	{
		// results are json encoded
		if (result == null)
			return null;
		if (result.Length >= 2 && result[0] == '"')
		{
			try
			{
				return JsonSerializer.Deserialize<string>(result);
			}
			catch (JsonException)
			{
				return result.Substring(1, result.Length - 2);
			}
		}
		return result;
	}

	public void Stop() => CoreWebView2?.Stop();

	public void Reload() => Control.Reload();

	public void GoBack() => Control.GoBack();

	public bool CanGoBack => Control.CanGoBack;

	public void GoForward() => Control.GoForward();

	public bool CanGoForward => Control.CanGoForward;

	public void LoadHtml(string html, Uri? baseUri)
	{
		if (!_ready)
		{
			_pendingUrl = null;
			RunWhenReady(() => LoadHtml(html, baseUri));
			return;
		}
		if (baseUri == null)
		{
			try
			{
				Control.NavigateToString(html);
				return;
			}
			catch (ArgumentException)
			{
				// NavigateToString rejects pages over 2 MB, so serve those from memory instead.
			}
		}

		var baseDirectory = baseUri != null && baseUri.IsFile ? baseUri.LocalPath : null;
		var htmlUri = AddResourceHandler(WebViewHtmlServer.Host, request => WebViewHtmlServer.Serve(html, baseDirectory!, request));
		// Navigate rather than setting Source, so loading new html at the same address still reloads.
		CoreWebView2?.Navigate(htmlUri.AbsoluteUri);
	}

	public void ShowPrintDialog() => CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);

	public bool BrowserContextMenuEnabled
	{
		get => _browserContextMenuEnabled;
		set
		{
			_browserContextMenuEnabled = value;
			var core = CoreWebView2;
			if (core != null)
				core.Settings.AreDefaultContextMenusEnabled = value;
		}
	}

	public bool SupportsResourceHandlers => true;

	public Uri AddResourceHandler(string host, Func<WebViewResourceRequest, WebViewResourceResponse> handler)
	{
		_resourceHandlers ??= new(StringComparer.OrdinalIgnoreCase);
		_resourceHandlers[host] = handler;
		var core = CoreWebView2;
		if (core != null)
			UpdateResourceFilter(core, host);
		return new Uri(GetResourceRoot(host));
	}

	public void RemoveResourceHandler(string host)
	{
		if (_resourceHandlers == null || !_resourceHandlers.Remove(host))
			return;
		var core = CoreWebView2;
		if (core != null)
			UpdateResourceFilter(core, host);
	}

	string GetResourceRoot(string host) => (UsesResourceScheme ? ResourceScheme : "https") + "://" + host.ToLowerInvariant() + "/";

	// Only hosts with a handler are intercepted, so other requests never pay for the round trip.
	void UpdateResourceFilter(CoreWebView2 core, string host)
	{
		if (!_resourceRequestsHooked)
		{
			core.WebResourceRequested += CoreWebView2_WebResourceRequested;
			_resourceRequestsHooked = true;
		}
		_filteredHosts ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var filter = GetResourceRoot(host) + "*";
		var hasHandler = _resourceHandlers != null && _resourceHandlers.ContainsKey(host);
		if (hasHandler && _filteredHosts.Add(host))
			core.AddWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.All);
		else if (!hasHandler && _filteredHosts.Remove(host))
			core.RemoveWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.All);
	}

	void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
	{
		if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri))
			return;
		if (_resourceHandlers == null || !_resourceHandlers.TryGetValue(uri.Host, out var handler))
			return;

		var environment = sender.Environment;
		WebViewResourceResponse? response;
		try
		{
			response = handler(new WebViewResourceRequest(uri, args.Request.Method));
		}
		catch (Exception ex)
		{
			// Letting this escape the WebView2 callback would crash the application.
			Debug.WriteLine("WebView resource handler failed for " + uri + ":\n" + ex);
			args.Response = environment.CreateWebResourceResponse(null, (int)HttpStatusCode.InternalServerError, "Internal Server Error", string.Empty);
			return;
		}

		if (response == null)
		{
			args.Response = environment.CreateWebResourceResponse(null, (int)HttpStatusCode.NotFound, "Not Found", string.Empty);
			return;
		}

		var headers = new List<string>();
		if (!string.IsNullOrEmpty(response.ContentType))
			headers.Add("Content-Type: " + response.ContentType);
		foreach (var header in response.Headers)
			headers.Add(header.Key + ": " + header.Value);
		var reasonPhrase = Enum.IsDefined(typeof(HttpStatusCode), response.StatusCode) ? ((HttpStatusCode)response.StatusCode).ToString() : string.Empty;
		args.Response = environment.CreateWebResourceResponse(ToRandomAccessStream(response.Content), response.StatusCode, reasonPhrase, string.Join("\r\n", headers));
	}

	static Windows.Storage.Streams.IRandomAccessStream? ToRandomAccessStream(Stream? stream)
	{
		if (stream == null)
			return null;
		if (!stream.CanSeek)
		{
			var copy = new MemoryStream();
			stream.CopyTo(copy);
			copy.Position = 0;
			stream = copy;
		}
		return stream.AsRandomAccessStream();
	}

	public override Color BackgroundColor
	{
		get => _backgroundColor ?? Control.DefaultBackgroundColor.ToEto();
		set
		{
			_backgroundColor = value;
			Control.DefaultBackgroundColor = value.ToWinUI();
		}
	}
}
