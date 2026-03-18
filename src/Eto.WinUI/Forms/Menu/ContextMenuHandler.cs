using Microsoft.UI.Xaml.Controls.Primitives;
using Eto.WinUI;

namespace Eto.WinUI.Forms.Menu;

public class ContextMenuHandler : WidgetHandler<muc.MenuFlyout, ContextMenu, ContextMenu.ICallback>, ContextMenu.IHandler
{
	public ContextMenuHandler()
	{
		Control = new muc.MenuFlyout();
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case ContextMenu.OpeningEvent:
				Control.Opening += Control_Opening;
				break;
			case ContextMenu.ClosedEvent:
				Control.Closed += Control_Closed;
				break;
			case ContextMenu.ClosingEvent:
				Control.Closing += Control_Closing;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void Control_Opening(object sender, object e)
	{
		foreach (var item in Widget.Items)
		{
			if (item.Handler is IMenuItemHandler handler)
				handler.Validate();
		}
		Callback.OnOpening(Widget, EventArgs.Empty);
	}

	void Control_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
	{
		Callback.OnClosing(Widget, EventArgs.Empty);
	}

	void Control_Closed(object sender, object e)
	{
		Callback.OnClosed(Widget, EventArgs.Empty);
	}

	public void AddMenu(int index, MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			throw new NotSupportedException($"Menu item type '{item.GetType().Name}' is not supported in WinUI context menus.");
		Control.Items.Insert(index, nativeControl);
	}

	public void RemoveMenu(MenuItem item)
	{
		if (item.Handler is not IWinUIMenuItemHandler handler || handler.NativeControlObject is not muc.MenuFlyoutItemBase nativeControl)
			return;
		Control.Items.Remove(nativeControl);
	}

	public void Clear()
	{
		Control.Items.Clear();
	}

	public void Show(Control relativeTo, PointF? location)
	{
		var control = relativeTo ?? Application.Instance?.MainForm;
		var host = control?.GetContainerControl();
		if (host == null || control == null)
			throw new InvalidOperationException("Unable to find a WinUI element to host the context menu.");

		if (location == null)
		{
			location = control.PointFromScreen(Mouse.Position);
		}
		var options = new FlyoutShowOptions
		{
			Position = location.Value.ToWinUI(),
			Placement = FlyoutPlacementMode.Auto
		};
		Control.ShowAt(host, options);
	}
}
