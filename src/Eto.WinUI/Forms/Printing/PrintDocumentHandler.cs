using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Eto.WinUI.Drawing;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Printing;
using Windows.Graphics.DirectX;
using Windows.Graphics.Printing;
using static Eto.WinUI.Forms.ShellNative;

namespace Eto.WinUI.Forms.Printing;

/// <summary>
/// Prints through the Windows print UI, which always shows (with a preview) as it can't print silently.
/// </summary>
public class PrintDocumentHandler : WidgetHandler<object, PrintDocument, PrintDocument.ICallback>, PrintDocument.IHandler
{
	// Letter with quarter inch margins, until the printer says otherwise
	static readonly sw.Rect DefaultImageableArea = new sw.Rect(24, 24, 768, 1008);

	Control? _control;
	int? _pageCount;
	PrintSettings? _printSettings;

	public string? Name { get; set; }

	public int PageCount
	{
		get => _pageCount ?? GetControlPageCount(DefaultImageableArea, GetControlSize());
		set => _pageCount = value;
	}

	public PrintSettings PrintSettings
	{
		get => _printSettings ??= new PrintSettings();
		set => _printSettings = value;
	}

	public void Create()
	{
	}

	public void Create(Control control)
	{
		_control = control;
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case PrintDocument.PrintingEvent:
			case PrintDocument.PrintedEvent:
			case PrintDocument.PrintPageEvent:
				// raised while printing
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	public void Print() => ShowPrintUI(null, PrintSettings, true);

	sealed class ControlImage : IDisposable
	{
		public required CanvasBitmap Bitmap { get; init; }
		public SizeF Size => new SizeF((float)Bitmap.Size.Width, (float)Bitmap.Size.Height);
		public void Dispose() => Bitmap.Dispose();
	}

	/// <summary>
	/// Shows the print UI and waits for it to close, returning true if the document was sent to the printer.
	/// </summary>
	internal bool ShowPrintUI(Window? parent, PrintSettings? settings, bool allowPageRange)
	{
		var settingsHandler = settings?.Handler as PrintSettingsHandler;
		var hwnd = GetOwnerWindow(parent, out var tempWindow);
		var device = CanvasDevice.GetSharedDevice();
		using var image = CaptureControl(device);
		using var document = new CanvasPrintDocument(device);
		int pageCount = 1;

		document.PrintTaskOptionsChanged += (sender, args) =>
		{
			var area = GetImageableArea(args.PrintTaskOptions);
			pageCount = Math.Max(1, _pageCount ?? GetControlPageCount(area, image?.Size));
			sender.SetPageCount((uint)pageCount);
			sender.InvalidatePreview();
		};
		document.Preview += (sender, args) => DrawPage(args.DrawingSession, GetImageableArea(args.PrintTaskOptions), (int)args.PageNumber - 1, image);
		document.Print += (sender, args) =>
		{
			settingsHandler?.ReadFrom(args.PrintTaskOptions);
			var area = GetImageableArea(args.PrintTaskOptions);
			Callback.OnPrinting(Widget, EventArgs.Empty);
			foreach (var page in GetPagesToPrint(args.PrintTaskOptions, pageCount))
			{
				using var session = args.CreateDrawingSession();
				DrawPage(session, area, page, image);
			}
			Callback.OnPrinted(Widget, EventArgs.Empty);
		};

		PrintTask? task = null;
		PrintTaskCompletion? completion = null;
		var manager = PrintManagerInterop.GetForWindow(hwnd);
		void TaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
		{
			task = args.Request.CreatePrintTask(string.IsNullOrEmpty(Name) ? "Document" : Name, source => source.SetSource(document));
			settingsHandler?.ApplyTo(task.Options, allowPageRange);
			// raised on a background thread
			task.Completed += (s, e) => completion = e.Completion;
		}
		manager.PrintTaskRequested += TaskRequested;
		try
		{
			var showOperation = PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
			var shown = Wait(showOperation.AsTask());
			if (!shown || task == null)
				return false;
			// Eto's API is synchronous, so wait for the user to print or cancel.
			while (completion == null && IsWindow(hwnd))
				PumpMessages(10);
			return completion == PrintTaskCompletion.Submitted;
		}
		finally
		{
			manager.PrintTaskRequested -= TaskRequested;
			if (tempWindow != IntPtr.Zero)
				DestroyWindow(tempWindow);
		}
	}

	static T Wait<T>(Task<T> task)
	{
		while (!task.IsCompleted)
			PumpMessages(10);
		return task.GetAwaiter().GetResult();
	}

	static void Wait(Task task)
	{
		while (!task.IsCompleted)
			PumpMessages(10);
		task.GetAwaiter().GetResult();
	}

	static IEnumerable<int> GetPagesToPrint(PrintTaskOptions options, int pageCount)
	{
		var ranges = options.CustomPageRanges;
		if (ranges.Count == 0)
		{
			for (int i = 0; i < pageCount; i++)
				yield return i;
			yield break;
		}
		foreach (var range in ranges)
		{
			for (int page = range.FirstPageNumber; page <= range.LastPageNumber; page++)
			{
				if (page >= 1 && page <= pageCount)
					yield return page - 1;
			}
		}
	}

	static sw.Rect GetImageableArea(PrintTaskOptions options)
	{
		var area = options.GetPageDescription(1).ImageableRect;
		return area.Width > 0 && area.Height > 0 ? area : DefaultImageableArea;
	}

	void DrawPage(CanvasDrawingSession session, sw.Rect area, int pageIndex, ControlImage? image)
	{
		session.Transform = Matrix3x2.CreateTranslation((float)area.X, (float)area.Y);
		var pageSize = new SizeF((float)area.Width, (float)area.Height);

		if (image != null)
		{
			// fit to the page width, spanning pages down
			var scale = Math.Min(1f, pageSize.Width / image.Size.Width);
			var sourceHeight = pageSize.Height / scale;
			var top = pageIndex * sourceHeight;
			var height = Math.Min(sourceHeight, image.Size.Height - top);
			if (height > 0)
			{
				var source = new sw.Rect(0, top, image.Size.Width, height);
				var destination = new sw.Rect(0, 0, image.Size.Width * scale, height * scale);
				session.DrawImage(image.Bitmap, destination, source);
			}
		}

		using var graphics = new Graphics(new GraphicsHandler(session));
		Callback.OnPrintPage(Widget, new PrintPageEventArgs(graphics, pageSize, pageIndex));
	}

	SizeF? GetControlSize()
	{
		if (_control == null)
			return null;
		var element = _control.ToNative();
		if (element?.XamlRoot != null && element.ActualWidth > 0)
			return new SizeF((float)element.ActualWidth, (float)element.ActualHeight);
		return _control.GetPreferredSize();
	}

	int GetControlPageCount(sw.Rect area, SizeF? size)
	{
		if (size == null || size.Value.Width <= 0 || size.Value.Height <= 0)
			return _control != null ? 1 : 0;
		var scale = Math.Min(1, area.Width / size.Value.Width);
		return Math.Max(1, (int)Math.Ceiling(size.Value.Height * scale / area.Height - 0.001));
	}

	// The print preview runs outside the visual tree, so render the control to a bitmap up front.
	ControlImage? CaptureControl(CanvasDevice device)
	{
		var element = _control?.ToNative();
		if (element == null)
			return null;

		muc.Panel? hostRoot = null;
		muc.Canvas? host = null;
		var theme = element.RequestedTheme;
		try
		{
			// it's going on paper, not the screen
			element.RequestedTheme = mux.ElementTheme.Light;
			if (element.XamlRoot == null)
			{
				// RenderTargetBitmap can only render elements in a window, so borrow one off to the side.
				if (element.Parent != null || GetHostRoot() is not muc.Panel root)
					return null;
				host = new muc.Canvas { Opacity = 0, IsHitTestVisible = false };
				host.Children.Add(element);
				hostRoot = root;
				hostRoot.Children.Add(host);
			}

			var timeout = Stopwatch.StartNew();
			while ((element.ActualWidth <= 0 || element.ActualHeight <= 0) && timeout.ElapsedMilliseconds < 2000)
				PumpMessages(10);
			var width = element.ActualWidth;
			var height = element.ActualHeight;
			if (width <= 0 || height <= 0)
				return null;

			// higher resolution for print quality, within the largest texture size
			var pixelScale = Math.Min(2.0, 16000.0 / Math.Max(width, height));
			var renderTarget = new mux.Media.Imaging.RenderTargetBitmap();
			Wait(renderTarget.RenderAsync(element, (int)(width * pixelScale), (int)(height * pixelScale)).AsTask());
			var pixels = Wait(renderTarget.GetPixelsAsync().AsTask()).ToArray();
			if (renderTarget.PixelWidth <= 0)
				return null;
			var dpi = (float)(96 * renderTarget.PixelWidth / width);
			var bitmap = CanvasBitmap.CreateFromBytes(device, pixels, renderTarget.PixelWidth, renderTarget.PixelHeight, DirectXPixelFormat.B8G8R8A8UIntNormalized, dpi);
			return new ControlImage { Bitmap = bitmap };
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Could not render control for printing: {ex}");
			return null;
		}
		finally
		{
			element.RequestedTheme = theme;
			if (host != null)
			{
				host.Children.Clear();
				hostRoot?.Children.Remove(host);
			}
		}
	}

	static muc.Panel? GetHostRoot()
	{
		var app = Application.Instance;
		var window = app?.MainForm ?? app?.Windows.FirstOrDefault(w => w.Visible);
		return (window?.Handler as IWinUIWindow)?.ContainerControl as muc.Panel;
	}

	const int WS_POPUP = unchecked((int)0x80000000);
	const int WS_VISIBLE = 0x10000000;
	const int WS_EX_TOOLWINDOW = 0x00000080;
	const int SM_CXSCREEN = 0;
	const int SM_CYSCREEN = 1;

	/// <summary>
	/// Gets the window to show the print UI for, creating a placeholder when there is none as the UI needs an owner.
	/// </summary>
	internal static IntPtr GetOwnerWindow(Window? parent, out IntPtr tempWindow)
	{
		tempWindow = IntPtr.Zero;
		var app = Application.Instance;
		var window = parent ?? app?.MainForm ?? app?.Windows.FirstOrDefault(w => w.Visible);
		var hwnd = window?.NativeHandle ?? IntPtr.Zero;
		if (hwnd != IntPtr.Zero)
			return hwnd;
		var x = GetSystemMetrics(SM_CXSCREEN) / 2;
		var y = GetSystemMetrics(SM_CYSCREEN) / 2;
		tempWindow = CreateWindowEx(WS_EX_TOOLWINDOW, "Static", string.Empty, WS_POPUP | WS_VISIBLE, x, y, 1, 1, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
		return tempWindow;
	}
}
