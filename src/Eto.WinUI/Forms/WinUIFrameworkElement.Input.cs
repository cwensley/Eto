using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace Eto.WinUI.Forms;

// Shown, keyboard, cursor and drag/drop support for all controls
public abstract partial class WinUIFrameworkElement<TControl, TWidget, TCallback>
{
	// the args Eto last marked as handled, so outer controls listening to handled events too can skip them
	static object? s_etoHandledKeyArgs;
	static PointerPoint? s_lastPointerPoint;

	/// <summary>Element that key events are attached to.</summary>
	protected virtual mux.UIElement KeyEventElement => ContainerControl;

	/// <summary>Element that drag and drop events are attached to.</summary>
	protected virtual mux.UIElement DragEventElement => ContainerControl;

	Cursor? _cursor;
	public virtual Cursor Cursor
	{
		get => _cursor!;
		set
		{
			_cursor = value;
			CursorHandler.SetCursor(MouseEventElement, value);
		}
	}

	bool AttachInputEvent(string id)
	{
		switch (id)
		{
			case Eto.Forms.Control.ShownEvent:
				ContainerControl.Loaded += HandleShownLoaded;
				ContainerControl.RegisterPropertyChangedCallback(mux.UIElement.VisibilityProperty, HandleShownVisibilityChanged);
				return true;
			case Eto.Forms.Control.KeyDownEvent:
				// handled events too, as controls like TextBox mark most keys as handled
				KeyEventElement.AddHandler(mux.UIElement.KeyDownEvent, new KeyEventHandler(HandleKeyDown), true);
				KeyEventElement.CharacterReceived += HandleCharacterReceived;
				return true;
			case Eto.Forms.Control.TextInputEvent:
				HandleEvent(Eto.Forms.Control.KeyDownEvent);
				return true;
			case Eto.Forms.Control.KeyUpEvent:
				KeyEventElement.AddHandler(mux.UIElement.KeyUpEvent, new KeyEventHandler(HandleKeyUp), true);
				return true;
			case Eto.Forms.Control.DragEnterEvent:
				DragEventElement.DragEnter += HandleDragEnter;
				HandleEvent(Eto.Forms.Control.DragOverEvent);
				return true;
			case Eto.Forms.Control.DragOverEvent:
				DragEventElement.DragOver += HandleDragOver;
				return true;
			case Eto.Forms.Control.DragLeaveEvent:
				DragEventElement.DragLeave += HandleDragLeave;
				HandleEvent(Eto.Forms.Control.DragEnterEvent);
				return true;
			case Eto.Forms.Control.DragDropEvent:
				DragEventElement.Drop += HandleDrop;
				HandleEvent(Eto.Forms.Control.DragOverEvent);
				return true;
			case Eto.Forms.Control.DragEndEvent:
				// raised by DoDragDrop when the drag completes
				return true;
			default:
				return false;
		}
	}

	void HandleShownLoaded(object sender, mux.RoutedEventArgs e) => ShownQueue.Add(ContainerControl, RaiseShownIfVisible);

	void HandleShownVisibilityChanged(mux.DependencyObject sender, mux.DependencyProperty dp)
	{
		if (ContainerControl.IsLoaded)
			RaiseShownIfVisible();
	}

	void RaiseShownIfVisible()
	{
		if (Widget.IsDisposed || !ContainerControl.IsLoaded)
			return;
		// only when it is actually visible, not just loaded inside a collapsed parent
		for (var element = (mux.DependencyObject)ContainerControl; element != null; element = muxm.VisualTreeHelper.GetParent(element))
		{
			if (element is mux.UIElement uiElement && uiElement.Visibility != mux.Visibility.Visible)
				return;
		}
		Callback.OnShown(Widget, EventArgs.Empty);
	}

	void HandleKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (Widget.IsDisposed || ReferenceEquals(s_etoHandledKeyArgs, e))
			return;
		var args = e.ToEto(KeyEventType.KeyDown);
		if (args.KeyData == Keys.None)
			return;
		Callback.OnKeyDown(Widget, args);
		if (args.Handled)
		{
			e.Handled = true;
			s_etoHandledKeyArgs = e;
		}
	}

	void HandleKeyUp(object sender, KeyRoutedEventArgs e)
	{
		if (Widget.IsDisposed || ReferenceEquals(s_etoHandledKeyArgs, e))
			return;
		var args = e.ToEto(KeyEventType.KeyUp);
		if (args.KeyData == Keys.None)
			return;
		Callback.OnKeyUp(Widget, args);
		if (args.Handled)
		{
			e.Handled = true;
			s_etoHandledKeyArgs = e;
		}
	}

	void HandleCharacterReceived(mux.UIElement sender, CharacterReceivedRoutedEventArgs e)
	{
		if (Widget.IsDisposed || e.Handled || char.IsControl(e.Character))
			return;

		var text = e.Character.ToString();
		var textArgs = new TextInputEventArgs(text);
		Callback.OnTextInput(Widget, textArgs);
		if (textArgs.Cancel)
		{
			e.Handled = true;
			return;
		}

		// like other platforms, typed characters also come through KeyDown with KeyChar set
		var args = new Eto.Forms.KeyEventArgs(Keys.None, KeyEventType.KeyDown, e.Character);
		Callback.OnKeyDown(Widget, args);
		e.Handled = args.Handled;
	}

	/// <summary>Remembers the last pointer seen, as WinUI needs one to start a drag.</summary>
	internal static void SetLastPointerPoint(PointerPoint point) => s_lastPointerPoint = point;

	/// <summary>The control a drag was started from with DoDragDrop, if it's still going.</summary>
	internal static Control? DragSourceControl { get; private set; }

	/// <summary>The data of the drag started with DoDragDrop, if it's still going.</summary>
	internal static DataObject? DragSourceData { get; private set; }

	public virtual void DoDragDrop(DataObject data, DragEffects allowedEffects, Image image, PointF cursorOffset)
	{
		var point = s_lastPointerPoint;
		var element = DragEventElement;
		if (point == null)
			throw new InvalidOperationException("DoDragDrop must be called while handling a mouse event");

		void Starting(mux.UIElement sender, mux.DragStartingEventArgs e)
		{
			e.AllowedOperations = allowedEffects.ToWinUI();
			DataObjectHandler.CopyTo(data, e.Data);
		}

		DragSourceControl = Widget;
		DragSourceData = data;
		element.DragStarting += Starting;
		var operation = element.StartDragAsync(point);
		operation.Completed = (info, status) =>
		{
			var result = status == Windows.Foundation.AsyncStatus.Completed ? info.GetResults() : Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
			Application.Instance.AsyncInvoke(() =>
			{
				element.DragStarting -= Starting;
				DragSourceControl = null;
				DragSourceData = null;
				if (Widget.IsDisposed)
					return;
				var args = new DragEventArgs(Widget, data, allowedEffects, PointFromScreen(Mouse.Position), KeyMap.GetModifiers(), Mouse.Buttons);
				args.Effects = result.ToEto();
				Callback.OnDragEnd(Widget, args);
			});
		};
	}

	DragEventArgs CreateDragEventArgs(mux.DragEventArgs e)
	{
		var data = DragSourceData ?? new DataObject(new DataObjectHandler(e.DataView));
		var modifiers = Keys.None;
		var buttons = MouseButtons.None;
		var dragModifiers = e.Modifiers;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Shift))
			modifiers |= Keys.Shift;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Control))
			modifiers |= Keys.Control;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.Alt))
			modifiers |= Keys.Alt;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.LeftButton))
			buttons |= MouseButtons.Primary;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.RightButton))
			buttons |= MouseButtons.Alternate;
		if (dragModifiers.HasFlag(Windows.ApplicationModel.DataTransfer.DragDrop.DragDropModifiers.MiddleButton))
			buttons |= MouseButtons.Middle;
		var location = e.GetPosition(ContainerControl).ToEto();
		return new DragEventArgs(DragSourceControl, data, e.AllowedOperations.ToEto(), location, modifiers, buttons, e);
	}

	void HandleDragEnter(object sender, mux.DragEventArgs e)
	{
		if (Widget.IsDisposed)
			return;
		var args = CreateDragEventArgs(e);
		Callback.OnDragEnter(Widget, args);
		e.AcceptedOperation = args.Effects.ToWinUI();
		e.Handled = true;
	}

	void HandleDragOver(object sender, mux.DragEventArgs e)
	{
		if (Widget.IsDisposed)
			return;
		var args = CreateDragEventArgs(e);
		Callback.OnDragOver(Widget, args);
		e.AcceptedOperation = args.Effects.ToWinUI();
		e.Handled = true;
	}

	void HandleDragLeave(object sender, mux.DragEventArgs e)
	{
		if (Widget.IsDisposed)
			return;
		Callback.OnDragLeave(Widget, CreateDragEventArgs(e));
	}

	void HandleDrop(object sender, mux.DragEventArgs e)
	{
		if (Widget.IsDisposed)
			return;
		var args = CreateDragEventArgs(e);
		args.Effects = e.AcceptedOperation.ToEto();
		Callback.OnDragDrop(Widget, args);
		e.AcceptedOperation = args.Effects.ToWinUI();
		e.Handled = true;
	}
}

static class DragConversions
{
	public static Windows.ApplicationModel.DataTransfer.DataPackageOperation ToWinUI(this DragEffects effects)
	{
		var result = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
		if (effects.HasFlag(DragEffects.Copy))
			result |= Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
		if (effects.HasFlag(DragEffects.Move))
			result |= Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
		if (effects.HasFlag(DragEffects.Link))
			result |= Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
		return result;
	}

	public static DragEffects ToEto(this Windows.ApplicationModel.DataTransfer.DataPackageOperation operation)
	{
		var result = DragEffects.None;
		if (operation.HasFlag(Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy))
			result |= DragEffects.Copy;
		if (operation.HasFlag(Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move))
			result |= DragEffects.Move;
		if (operation.HasFlag(Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link))
			result |= DragEffects.Link;
		return result;
	}
}

/// <summary>
/// Raises Shown for controls loaded together from parent to child, as WinUI raises Loaded from the children up.
/// </summary>
static class ShownQueue
{
	static List<(mux.DependencyObject Element, Action Raise)>? s_pending;

	public static void Add(mux.DependencyObject element, Action raise)
	{
		if (s_pending == null)
		{
			s_pending = new();
			mud.DispatcherQueue.GetForCurrentThread().TryEnqueue(mud.DispatcherQueuePriority.High, Flush);
		}
		s_pending.Add((element, raise));
	}

	static void Flush()
	{
		var pending = s_pending;
		s_pending = null;
		if (pending == null)
			return;
		foreach (var item in pending.OrderBy(r => GetDepth(r.Element)))
			item.Raise();
	}

	static int GetDepth(mux.DependencyObject element)
	{
		var depth = 0;
		for (var parent = muxm.VisualTreeHelper.GetParent(element); parent != null; parent = muxm.VisualTreeHelper.GetParent(parent))
			depth++;
		return depth;
	}
}
