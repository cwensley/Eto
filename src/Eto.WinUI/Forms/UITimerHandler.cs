namespace Eto.WinUI.Forms;

public class UITimerHandler : WidgetHandler<mud.DispatcherQueueTimer?, UITimer, UITimer.ICallback>, UITimer.IHandler
{
	double _interval = 1;

	public double Interval
	{
		get => _interval;
		set
		{
			_interval = value;
			if (Control != null)
				Control.Interval = TimeSpan.FromSeconds(value);
		}
	}

	public void Start()
	{
		if (Control == null)
		{
			// created on start, as the timer belongs to the dispatcher of the thread that creates it
			Control = mud.DispatcherQueue.GetForCurrentThread().CreateTimer();
			Control.IsRepeating = true;
			Control.Tick += HandleTick;
		}
		else
			Control.Stop();
		Control.Interval = TimeSpan.FromSeconds(_interval);
		Control.Start();
	}

	public void Stop() => Control?.Stop();

	void HandleTick(mud.DispatcherQueueTimer sender, object args)
	{
		if (Widget.IsDisposed)
		{
			sender.Stop();
			return;
		}
		Callback.OnElapsed(Widget, EventArgs.Empty);
	}

	protected override void Dispose(bool disposing)
	{
		if (Control != null)
		{
			Control.Stop();
			Control.Tick -= HandleTick;
		}
		base.Dispose(disposing);
	}
}
