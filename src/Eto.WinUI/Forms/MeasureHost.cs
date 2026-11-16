namespace Eto.WinUI.Forms;

/// <summary>
/// Hosts elements in a hidden window while measuring them.
/// </summary>
/// <remarks>
/// WinUI only applies styles and templates once an element is in a live tree, so a control that hasn't been
/// shown yet measures as if it had no template (e.g. a check box with no width).
/// </remarks>
static class MeasureHost
{
	static mux.Window? s_window;
	static muc.Canvas? s_canvas;
	static bool s_shutdown;

	static muc.Canvas? Canvas
	{
		get
		{
			if (s_canvas == null && !s_shutdown)
			{
				try
				{
					s_canvas = new muc.Canvas();
					s_window = new mux.Window { Content = s_canvas };
					s_window.Closed += (sender, e) =>
					{
						s_window = null;
						s_canvas = null;
					};
				}
				catch
				{
					// no application yet
					s_window = null;
					s_canvas = null;
				}
			}
			return s_canvas;
		}
	}

	/// <summary>
	/// Creates the hidden window ahead of the first measure.
	/// </summary>
	public static void Prepare() => _ = Canvas;

	/// <summary>
	/// Closes the hidden window for good, so it doesn't keep the app running once its last window closes.
	/// </summary>
	public static void Shutdown()
	{
		s_shutdown = true;
		var window = s_window;
		s_window = null;
		s_canvas = null;
		window?.Close();
	}

	/// <summary>
	/// Measures the element, hosting the root of its tree in the hidden window if it isn't in a live tree yet.
	/// </summary>
	public static sw.Size Measure(mux.FrameworkElement element, sw.Size availableSize)
	{
		if (element.XamlRoot != null)
		{
			element.Measure(availableSize);
			return element.DesiredSize;
		}

		var root = GetRoot(element);
		var canvas = root is mux.FrameworkElement ? Canvas : null;
		if (canvas == null || root is not mux.UIElement rootElement)
		{
			element.Measure(availableSize);
			return element.DesiredSize;
		}

		canvas.Children.Add(rootElement);
		try
		{
			// a newly created window only goes live once the dispatcher gets to it
			if (element.XamlRoot == null)
			{
				canvas.Children.Remove(rootElement);
				element.Measure(availableSize);
				return element.DesiredSize;
			}
			element.Measure(availableSize);
			// read before removing, which resets it
			return element.DesiredSize;
		}
		finally
		{
			canvas.Children.Remove(rootElement);
		}
	}

	static mux.DependencyObject GetRoot(mux.FrameworkElement element)
	{
		mux.DependencyObject current = element;
		while (true)
		{
			var parent = (current as mux.FrameworkElement)?.Parent ?? muxm.VisualTreeHelper.GetParent(current);
			if (parent == null)
				return current;
			current = parent;
		}
	}
}
