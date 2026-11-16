namespace Eto.WinUI.Forms;

public class EtoCanvas : muc.Canvas
{
	public IWinUIFrameworkElement? Handler { get; set; }

	public EtoCanvas()
	{
		// children are clipped to the layout like on other platforms
		SizeChanged += (sender, e) => Clip = new muxm.RectangleGeometry { Rect = new sw.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
	}

	protected override sw.Size MeasureOverride(sw.Size constraint)
	{
		return Handler?.MeasureOverride(constraint, MeasureChildren) ?? MeasureChildren(constraint);
	}

	// Canvas reports no size of its own, so size to fit the children at their positions
	sw.Size MeasureChildren(sw.Size constraint)
	{
		var size = new sw.Size();
		var infinite = new sw.Size(double.PositiveInfinity, double.PositiveInfinity);
		foreach (var child in Children)
		{
			child.Measure(infinite);
			var desired = child.DesiredSize;
			size.Width = Math.Max(size.Width, GetLeft(child) + desired.Width);
			size.Height = Math.Max(size.Height, GetTop(child) + desired.Height);
		}
		return size.Min(constraint);
	}
}

public class PixelLayoutHandler : WinUILayout<EtoCanvas, PixelLayout, PixelLayout.ICallback>, PixelLayout.IHandler
{
	protected override EtoCanvas CreateControl() => new EtoCanvas { Handler = this };

	public override mux.FrameworkElement ContainerControl => Control;

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public void Add(Control child, int x, int y)
	{
		var element = child.GetContainerControl();
		if (element == null)
			return;
		muc.Canvas.SetLeft(element, x);
		muc.Canvas.SetTop(element, y);
		if (!Control.Children.Contains(element))
			Control.Children.Add(element);
		Control.InvalidateMeasure();
	}

	public void Move(Control child, int x, int y)
	{
		var element = child.GetContainerControl();
		if (element == null)
			return;
		muc.Canvas.SetLeft(element, x);
		muc.Canvas.SetTop(element, y);
		Control.InvalidateMeasure();
	}

	public void Remove(Control child)
	{
		var element = child.GetContainerControl();
		if (element != null && Control.Children.Remove(element))
			Control.InvalidateMeasure();
	}
}
