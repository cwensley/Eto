using Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

public abstract class WinUIContainer<TControl, TWidget, TCallback> : WinUIFrameworkElement<TControl, TWidget, TCallback>, Container.IHandler
	where TControl : class
	where TWidget : Container
	where TCallback : Container.ICallback
{
	public virtual Size ClientSize { get; set; }
	public virtual bool RecurseToChildren => true;
	public override IEnumerable<Control> VisualControls => Widget.Controls;
}


public abstract class WinUIBorderedContainer<TControl, TWidget, TCallback> : WinUIBorderedControl<TControl, TWidget, TCallback>, Container.IHandler
	where TControl : mux.UIElement
	where TWidget : Container
	where TCallback : Container.ICallback
{
	public virtual Size ClientSize { get; set; }
	public virtual bool RecurseToChildren => true;
	public override IEnumerable<Control> VisualControls => Widget.Controls;
}

