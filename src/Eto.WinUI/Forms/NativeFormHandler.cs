using WinRT.Interop;
using mui = Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

/// <summary>
/// Wraps a WinUI window that Eto did not create, so it can be used as an Eto window (e.g. as a dialog parent).
/// </summary>
public class NativeFormHandler : FormHandler
{
	static readonly List<WeakReference> s_windows = new();

	public NativeFormHandler(mui.Window window)
	{
		Control = window;
	}

	protected override bool IsAttached => true;

	protected override mui.Window CreateControl() => throw new InvalidOperationException("NativeFormHandler requires an existing window");

	/// <summary>
	/// Gets the Eto window for the specified native window, wrapping it if Eto doesn't already know it.
	/// </summary>
	public static Window Create(mui.Window window)
	{
		return Find(w => w == window) ?? Wrap(window);
	}

	/// <summary>
	/// Gets the Eto window for the specified window handle.
	/// </summary>
	public static Window Create(IntPtr windowHandle)
	{
		// there's no way to get a WinUI window from an HWND, so only windows Eto already knows can be found
		return Find(w => WindowNative.GetWindowHandle(w) == windowHandle)
			?? throw new NotSupportedException("WinUI can only use a window handle of a window created by Eto or wrapped using WinUIHelpers.ToEtoWindow(Microsoft.UI.Xaml.Window)");
	}

	internal static Window? Find(Func<mui.Window, bool> matches)
	{
		foreach (var w in Application.Instance.Windows)
		{
			if (w.ControlObject is mui.Window native && matches(native))
				return w;
		}

		for (int i = 0; i < s_windows.Count; i++)
		{
			if (s_windows[i].Target is NativeFormHandler handler)
			{
				if (matches(handler.Control))
					return handler.Widget;
			}
			else
			{
				s_windows.RemoveAt(i);
				i--;
			}
		}
		return null;
	}

	static Window Wrap(mui.Window window)
	{
		var form = new Form(new NativeFormHandler(window));
		s_windows.Add(new WeakReference(form.Handler));
		return form;
	}
}
