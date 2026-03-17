namespace Eto.WinUI.Forms.Controls;

public class SliderHandler : WinUIBorderedControl<muc.Slider, Slider, Slider.ICallback>, Slider.IHandler
{
	protected override sw.Size DefaultSize =>
		Orientation == Orientation.Horizontal
			? new sw.Size(100, double.NaN)
			: new sw.Size(double.NaN, 100);

	public int MaxValue
	{
		get => (int)Control.Maximum;
		set => Control.Maximum = value;
	}

	public int MinValue
	{
		get => (int)Control.Minimum;
		set => Control.Minimum = value;
	}

	public int Value
	{
		get => (int)Control.Value;
		set => Control.Value = value;
	}

	public int TickFrequency
	{
		get => (int)Control.TickFrequency;
		set => Control.TickFrequency = value;
	}

	public bool SnapToTick
	{
		get => Control.SnapsTo == muc.Primitives.SliderSnapsTo.Ticks;
		set => Control.SnapsTo = value ? muc.Primitives.SliderSnapsTo.Ticks : muc.Primitives.SliderSnapsTo.StepValues;
	}

	public Orientation Orientation
	{
		get => Control.Orientation == mux.Controls.Orientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical;
		set => Control.Orientation = value == Orientation.Horizontal ? mux.Controls.Orientation.Horizontal : mux.Controls.Orientation.Vertical;
	}

	protected override muc.Slider CreateControl() => new();

	protected override void Initialize()
	{
		base.Initialize();
		Control.Minimum = 0;
		Control.Maximum = 100;
		Control.TickFrequency = 5;
		Control.ValueChanged += Control_ValueChanged;
	}

	void Control_ValueChanged(object sender, muc.Primitives.RangeBaseValueChangedEventArgs e)
	{
		Callback.OnValueChanged(Widget, EventArgs.Empty);
	}
}
