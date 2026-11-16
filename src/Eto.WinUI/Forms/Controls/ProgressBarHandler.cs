namespace Eto.WinUI.Forms.Controls;

public class ProgressBarHandler : WinUIBorderedControl<muc.ProgressBar, ProgressBar, ProgressBar.ICallback>, ProgressBar.IHandler
{
	protected override sw.Size DefaultSize => new sw.Size(100, double.NaN);

	protected override muc.ProgressBar CreateControl() => new muc.ProgressBar
	{
		Minimum = 0,
		Maximum = 100,
		VerticalAlignment = mux.VerticalAlignment.Center
	};

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

	public bool Indeterminate
	{
		get => Control.IsIndeterminate;
		set => Control.IsIndeterminate = value;
	}
}
