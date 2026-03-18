using Windows.Security.Cryptography.Certificates;

namespace Eto.WinUI.Forms;

public interface IWinUIFrameworkElement
{
	sw.Size MeasureOverride(sw.Size constraint, Func<sw.Size, sw.Size> measure);
	mux.FrameworkElement ContainerControl { get; }
}

public abstract partial class WinUIFrameworkElement<TControl, TWidget, TCallback> : WidgetHandler<TControl, TWidget, TCallback>, Control.IHandler, IWinUIFrameworkElement
	where TControl : class
	where TWidget : Control
	where TCallback : Control.ICallback
{

	protected T GetNullableValue<T, X>(object property, X obj, Func<X, T> getValue, T defaultValue)
	{
		if (obj == null)
		{
			if (defaultValue != null)
				return Widget.Properties.Get<T>(property, defaultValue);
			else
				return Widget.Properties.Get<T>(property);
		}
		return getValue(obj);
	}

	protected T GetNullableValue<T, X>(object property, X obj, Func<X, T> getValue, Func<T>? defaultValue = null)
		where X : class
	{
		if (obj == null)
		{
			if (defaultValue != null)
				return Widget.Properties.Get<T>(property, defaultValue);
			else
				return Widget.Properties.Get<T>(property);
		}
		return getValue(obj);
	}

	List<Action>? setValues;

	protected void SetNullableValue<T, X>(object property, T value, Func<X> objFunc, Action<X, T> setValue)
	{
		var obj = objFunc();
		if (obj == null)
		{
			setValues ??= new List<Action>();
			setValues.Add(() =>
			{
				var obj2 = objFunc();
				setValue(obj2, value);
				Widget.Properties.Remove(property);
			});
			ContainerControl.Loaded -= Control_Loaded;
			ContainerControl.Loaded += Control_Loaded;
			Widget.Properties.Set(property, value);
			return;
		}
		setValue(obj, value);
	}

	private void Control_Loaded(object sender, mux.RoutedEventArgs e)
	{
		if (setValues == null)
			return;
		foreach (var setValue in setValues)
		{
			setValue();
		}
		setValues.Clear();
	}

	public abstract mux.FrameworkElement ContainerControl { get; }
	public virtual mux.FrameworkElement FocusControl => ContainerControl;


	public abstract Color BackgroundColor { get; set; }


	public virtual bool Enabled { get; set; } = true;
	public virtual bool HasFocus => ContainerControl.FocusState != mux.FocusState.Unfocused;
	public virtual bool Visible
	{
		get => ContainerControl.Visibility == mux.Visibility.Visible;
		set => ContainerControl.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}
	public IEnumerable<string> SupportedPlatformCommands { get; }
	public Point Location { get; }
	public string ToolTip { get; set; }
	public Cursor Cursor { get; set; }
	public int TabIndex { get; set; }
	public virtual IEnumerable<Control> VisualControls { get; }
	public virtual bool AllowDrop
	{
		get => ContainerControl.AllowDrop;
		set => ContainerControl.AllowDrop = value;
	}
	public bool IsMouseCaptured { get; }

	public bool CaptureMouse()
	{
		throw new NotImplementedException();
	}

	public virtual void DoDragDrop(DataObject data, DragEffects allowedEffects, Image image, PointF cursorOffset)
	{
		throw new NotImplementedException();
	}

	public virtual void Focus()
	{
		ContainerControl.Focus(mux.FocusState.Programmatic);
	}

	public Window GetNativeParentWindow()
	{
		throw new NotImplementedException();
	}

	public virtual SizeF GetPreferredSize(SizeF availableSize)
	{
		ContainerControl.Measure(availableSize.ToWinUI());
		return ContainerControl.DesiredSize.ToEto();
	}

	public virtual void Invalidate(bool invalidateChildren)
	{
	}

	public virtual void Invalidate(Rectangle rect, bool invalidateChildren)
	{
	}

	public virtual void MapPlatformCommand(string systemCommand, Command command)
	{
	}

	public virtual void OnLoad(EventArgs e)
	{
	}

	public virtual void OnLoadComplete(EventArgs e)
	{
	}

	public virtual void OnPreLoad(EventArgs e)
	{
	}

	public virtual void OnUnLoad(EventArgs e)
	{
	}

	public virtual PointF PointFromScreen(PointF point)
	{
		if (!Widget.Loaded)
			return point;

		var root = ContainerControl.XamlRoot?.Content as mux.UIElement;
		if (root == null)
			return point;

		var windowPosition = GetWindowScreenPosition();
		if (windowPosition == null)
			return point;

		var rootPoint = point - ScreenHandler.PhysicalToLogical(windowPosition.Value);
		return root.TransformToVisual(ContainerControl).TransformPoint(rootPoint.ToWinUI()).ToEto();
	}

	public virtual PointF PointToScreen(PointF point)
	{
		if (!Widget.Loaded)
			return point;

		var root = ContainerControl.XamlRoot?.Content as mux.UIElement;
		if (root == null)
			return point;

		var windowPosition = GetWindowScreenPosition();
		if (windowPosition == null)
			return point;

		var rootPoint = ContainerControl.TransformToVisual(root).TransformPoint(point.ToWinUI()).ToEto();
		return ScreenHandler.PhysicalToLogical(windowPosition.Value) + rootPoint;
	}

	public void ReleaseMouseCapture()
	{
		throw new NotImplementedException();
	}

	public virtual void ResumeLayout()
	{
	}

	public virtual void SetParent(Container oldParent, Container newParent)
	{
	}

	public virtual void SuspendLayout()
	{
	}

	public virtual void UpdateLayout() => ContainerControl.InvalidateMeasure();

	public virtual sw.Size MeasureOverride(sw.Size constraint, Func<sw.Size, sw.Size> measure)
	{
		// enforce eto-style sizing to wpf controls
		var size = UserPreferredSize;
		var control = ContainerControl;

		// Constrain content to the preferred size of this control, if specified.
		var desired = measure(constraint.IfInfinity(size.InfinityIfNan()));
		// Desired size should not be smaller than default (minimum) size.
		// ensures buttons, text box, etc have a minimum size
		var defaultSize = DefaultSize.ZeroIfNan();
		desired = desired.Max(defaultSize);

		// Desired should also not be bigger than default size if we have no constraint.
		// Without it, controls like TextArea, GridView, etc will grow to their content.
		if (double.IsInfinity(constraint.Width) && defaultSize.Width > 0)
			desired.Width = PreventUserResize ? defaultSize.Width : Math.Max(defaultSize.Width, desired.Width);
		if (double.IsInfinity(constraint.Height) && defaultSize.Height > 0)
			desired.Height = PreventUserResize ? defaultSize.Height : Math.Max(defaultSize.Height, desired.Height);

		// use the user preferred size, and ensure it's not larger than available size
		size = size.IfNaN(desired);
		size = size.Min(constraint);

		// restrict to the min/max sizes
		size = size.Max(control.GetMinSize());
		size = size.Min(control.GetMaxSize());
		return size;
	}

	sw.Size _userPreferredSize = new sw.Size(double.NaN, double.NaN);
	protected sw.Size UserPreferredSize
	{
		get => _userPreferredSize;
		set
		{
			_userPreferredSize = value;
			//SetSize();
			//ContainerControl.SetSize(value);
			ContainerControl.InvalidateMeasure();
			//if (Control is mux.FrameworkElement fe)
			//	fe.InvalidateMeasure();
			UpdatePreferredSize();
		}
	}

	protected virtual sw.Size DefaultSize => new sw.Size(double.NaN, double.NaN);

	/// <summary>
	/// This property, when set to true, will prevent the control from growing/shrinking based on user input.
	/// Typically, this will be accompanied by overriding the <see cref="DefaultSize"/> as well.
	/// 
	/// For example, when the user types into a text box, it will grow to fit the content if it is auto sized.
	/// This doesn't happen on any other platform, so we need to disable this behaviour on WPF.
	/// </summary>
	protected virtual bool PreventUserResize => false;


	public virtual void UpdatePreferredSize()
	{
		if (Widget.Loaded)
		{
			//Widget.VisualParent.GetFrameworkElement()?.OnChildPreferredSizeUpdated();
		}
	}

	public virtual Size Size
	{
		get
		{
			//if (newSize != null)
			//	return newSize.Value;
			if (!Widget.Loaded)
				return UserPreferredSize.ToEtoSize();
			// if (Win32.IsSystemDpiAware && !double.IsNaN(Control.ActualWidth) && !double.IsNaN(Control.ActualHeight))
			// {
			// 	// convert system dpi to logical
			// 	var sizef = new SizeF((float)Control.ActualWidth, (float)Control.ActualHeight) / (Win32.GetDpiForSystem() / 96f) * SwfScreen.GetLogicalPixelSize();
			// 	return Size.Round(sizef);
			// }

			return ContainerControl.GetSize();
		}
		set
		{
			var newSize = value.ToWinUI();
			if (UserPreferredSize == newSize)
				return;
			UserPreferredSize = newSize;
		}
	}

	public virtual int Width
	{
		get => Size.Width;
		set
		{
			var newWidth = value == -1 ? double.NaN : value;
			var userPreferredSize = UserPreferredSize;
			if (userPreferredSize.Width == newWidth)
				return;
			UserPreferredSize = new sw.Size(newWidth, userPreferredSize.Height);
		}
	}

	public virtual int Height
	{
		get => Size.Height;
		set
		{
			var newHeight = value == -1 ? double.NaN : value;
			var userPreferredSize = UserPreferredSize;
			if (userPreferredSize.Height == newHeight)
				return;
			UserPreferredSize = new sw.Size(userPreferredSize.Width, newHeight);
		}
	}

	protected virtual mux.FrameworkElement MouseEventElement => ContainerControl;

	ContextMenu _contextMenu;
	public ContextMenu ContextMenu
	{
		get => _contextMenu;
		set
		{
			_contextMenu = value;
			ContainerControl.ContextFlyout = value?.ControlObject as muc.MenuFlyout;
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Eto.Forms.Control.MouseDownEvent:
				MouseEventElement.PointerPressed += HandlePointerPressed;
				HandleEvent(Eto.Forms.Control.MouseUpEvent);
				break;
			case Eto.Forms.Control.MouseUpEvent:
				MouseEventElement.PointerReleased += HandlePointerReleased;
				HandleEvent(Eto.Forms.Control.MouseDownEvent);
				break;
			case Eto.Forms.Control.MouseMoveEvent:
				MouseEventElement.PointerMoved += HandlePointerMoved;
				break;
			case Eto.Forms.Control.MouseEnterEvent:
				MouseEventElement.PointerEntered += HandlePointerEntered;
				break;
			case Eto.Forms.Control.MouseLeaveEvent:
				HandleEvent(Eto.Forms.Control.MouseEnterEvent);
				MouseEventElement.PointerExited += HandlePointerExited;
				break;
			case Eto.Forms.Control.MouseWheelEvent:
				MouseEventElement.PointerWheelChanged += HandlePointerWheelChanged;
				break;
			case Eto.Forms.Control.MouseDoubleClickEvent:
				MouseEventElement.DoubleTapped += HandleDoubleTapped;
				break;
		}
		base.AttachEvent(id);
	}

	void HandlePointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseButtonEventArgs(e, isPressed: true);
		Callback.OnMouseDown(Widget, args);
		e.Handled = args.Handled;
	}

	void HandlePointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseButtonEventArgs(e, isPressed: false);
		Callback.OnMouseUp(Widget, args);
		e.Handled = args.Handled;
	}

	void HandlePointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseEventArgs(e);
		Callback.OnMouseMove(Widget, args);
		e.Handled = args.Handled;
	}

	void HandlePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseEventArgs(e);
		Callback.OnMouseEnter(Widget, args);
		e.Handled = args.Handled;
	}

	void HandlePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseEventArgs(e);
		Callback.OnMouseLeave(Widget, args);
		e.Handled = args.Handled;
	}

	void HandlePointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = CreateMouseWheelEventArgs(e);
		Callback.OnMouseWheel(Widget, args);
		e.Handled = args.Handled;
	}

	void HandleDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
	{
		if (Control == null)
			return;

		var args = new MouseEventArgs(
			MouseButtons.Primary,
			GetModifierKeys(),
			e.GetPosition(MouseEventElement).ToEto());
		Callback.OnMouseDoubleClick(Widget, args);
		e.Handled = args.Handled;
	}

	MouseEventArgs CreateMouseButtonEventArgs(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool isPressed)
	{
		var point = e.GetCurrentPoint(MouseEventElement);
		var location = point.Position.ToEto();
		var buttons = GetChangedMouseButtons(point.Properties.PointerUpdateKind, isPressed);
		if (buttons == MouseButtons.None)
			buttons = GetPressedMouseButtons(point.Properties);

		return new MouseEventArgs(buttons, GetModifierKeys(e.KeyModifiers), location, pressure: GetPressure(point));
	}

	MouseEventArgs CreateMouseEventArgs(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		var point = e.GetCurrentPoint(MouseEventElement);
		return new MouseEventArgs(
			GetPressedMouseButtons(point.Properties),
			GetModifierKeys(e.KeyModifiers),
			point.Position.ToEto(),
			pressure: GetPressure(point));
	}

	MouseEventArgs CreateMouseWheelEventArgs(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		var point = e.GetCurrentPoint(MouseEventElement);
		return new MouseEventArgs(
			GetPressedMouseButtons(point.Properties),
			GetModifierKeys(e.KeyModifiers),
			point.Position.ToEto(),
			new SizeF(0, point.Properties.MouseWheelDelta / WinUIConversions.WheelDelta),
			GetPressure(point));
	}

	static MouseButtons GetPressedMouseButtons(Microsoft.UI.Input.PointerPointProperties properties)
	{
		var buttons = MouseButtons.None;
		if (properties.IsLeftButtonPressed)
			buttons |= MouseButtons.Primary;
		if (properties.IsRightButtonPressed)
			buttons |= MouseButtons.Alternate;
		if (properties.IsMiddleButtonPressed)
			buttons |= MouseButtons.Middle;
		return buttons;
	}

	static MouseButtons GetChangedMouseButtons(Microsoft.UI.Input.PointerUpdateKind updateKind, bool isPressed)
	{
		return updateKind switch
		{
			Microsoft.UI.Input.PointerUpdateKind.LeftButtonPressed when isPressed => MouseButtons.Primary,
			Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased when !isPressed => MouseButtons.Primary,
			Microsoft.UI.Input.PointerUpdateKind.RightButtonPressed when isPressed => MouseButtons.Alternate,
			Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased when !isPressed => MouseButtons.Alternate,
			Microsoft.UI.Input.PointerUpdateKind.MiddleButtonPressed when isPressed => MouseButtons.Middle,
			Microsoft.UI.Input.PointerUpdateKind.MiddleButtonReleased when !isPressed => MouseButtons.Middle,
			_ => MouseButtons.None
		};
	}

	static float GetPressure(Microsoft.UI.Input.PointerPoint point)
	{
		var pressure = point.Properties.Pressure;
		return pressure > 0 ? pressure : 1f;
	}

	static Keys GetModifierKeys() => Keys.None;

	static Keys GetModifierKeys(Windows.System.VirtualKeyModifiers modifiers)
	{
		var keys = Keys.None;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
			keys |= Keys.Control;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift))
			keys |= Keys.Shift;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Menu))
			keys |= Keys.Alt;
		if (modifiers.HasFlag(Windows.System.VirtualKeyModifiers.Windows))
			keys |= Keys.Application;
		return keys;
	}

	PointF? GetWindowScreenPosition()
	{
		if (Widget.ParentWindow?.Handler is not IWinUIWindow window)
			return null;

		var position = window.Control.AppWindow.Position;
		return new PointF(position.X, position.Y);
	}

}
