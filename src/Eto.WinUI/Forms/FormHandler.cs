using mui = Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

public class FormHandler : WinUIWindow<mui.Window, Form, Form.ICallback>, Form.IHandler
{
	protected override mui.Window CreateControl() => new mui.Window();

	public bool ShowActivated { get; set; } = true;

	bool _canFocus = true;
	public bool CanFocus
	{
		get => _canFocus;
		set
		{
			_canFocus = value;
			var hwnd = NativeHandle;
			var exStyle = WindowNativeMethods.GetWindowLongPtr(hwnd, WindowNativeMethods.GWL_EXSTYLE).ToInt64();
			exStyle = value ? exStyle & ~WindowNativeMethods.WS_EX_NOACTIVATE : exStyle | WindowNativeMethods.WS_EX_NOACTIVATE;
			WindowNativeMethods.SetWindowLongPtr(hwnd, WindowNativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
		}
	}

	public virtual void Show() => ShowWindow(ShowActivated && CanFocus);

	public override void Focus()
	{
		if (CanFocus)
			base.Focus();
		else
			BringToFront();
	}
}
