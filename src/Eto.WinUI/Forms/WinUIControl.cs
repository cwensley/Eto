
using Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

public abstract class WinUIControl<TControl, TWidget, TCallback> : WinUIFrameworkElement<TControl, TWidget, TCallback>, Control.IHandler
	where TControl : muc.Control
	where TWidget : Control
	where TCallback : Control.ICallback
{
	public override FrameworkElement ContainerControl => Control;

	static readonly object Font_Key = new object();

	public virtual Font Font
	{
		get => Widget.Properties.Get<Font>(Font_Key) ?? Eto.WinUI.Drawing.FontHandler.GetFont(Control);
		set
		{
			Widget.Properties.Set(Font_Key, value);
			Eto.WinUI.Drawing.FontHandler.Apply(Control, value);
		}
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}
	public virtual Color TextColor
	{
		get => Control.Foreground.ToEtoColor();
		set => Control.Foreground = value.ToWinUIBrush();
	}
}
