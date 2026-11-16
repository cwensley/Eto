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

	protected override bool ControlEnabled
	{
		get => base.ControlEnabled;
		set
		{
			base.ControlEnabled = value;
			foreach (var child in Widget.VisualControls)
				child.GetFrameworkElement()?.SetEnabled(value);
		}
	}

	public override void Invalidate(bool invalidateChildren)
	{
		base.Invalidate(invalidateChildren);
		if (invalidateChildren)
			WinUIContainerHelper.InvalidateChildren(Widget);
	}

	public override void Invalidate(Rectangle rect, bool invalidateChildren)
	{
		base.Invalidate(rect, invalidateChildren);
		if (invalidateChildren)
			WinUIContainerHelper.InvalidateChildren(Widget);
	}
}

static class WinUIContainerHelper
{
	// only drawn content needs repainting, but it can be nested anywhere in the container
	public static void InvalidateChildren(Container container)
	{
		foreach (var child in container.VisualControls)
			child.Invalidate(true);
	}
}

public abstract class WinUIBorderedContainer<TControl, TWidget, TCallback> : WinUIBorderedControl<TControl, TWidget, TCallback>, Container.IHandler
	where TControl : mux.UIElement
	where TWidget : Container
	where TCallback : Container.ICallback
{
	public virtual Size ClientSize { get; set; }
	public virtual bool RecurseToChildren => true;
	public override IEnumerable<Control> VisualControls => Widget.Controls;

	protected override bool ControlEnabled
	{
		get => base.ControlEnabled;
		set
		{
			base.ControlEnabled = value;
			foreach (var child in Widget.VisualControls)
				child.GetFrameworkElement()?.SetEnabled(value);
		}
	}

	public override void Invalidate(bool invalidateChildren)
	{
		base.Invalidate(invalidateChildren);
		if (invalidateChildren)
			WinUIContainerHelper.InvalidateChildren(Widget);
	}

	public override void Invalidate(Rectangle rect, bool invalidateChildren)
	{
		base.Invalidate(rect, invalidateChildren);
		if (invalidateChildren)
			WinUIContainerHelper.InvalidateChildren(Widget);
	}
}

