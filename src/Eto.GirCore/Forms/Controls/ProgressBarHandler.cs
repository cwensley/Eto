namespace Eto.GirCore.Forms.Controls
{
	public class ProgressBarHandler : GirControl<Gtk.ProgressBar, ProgressBar, ProgressBar.ICallback>, ProgressBar.IHandler
	{
		int _value;
		int _minValue;
		int _maxValue = 100;
		bool _indeterminate;
		uint _timeoutId;
		public static double UpdateInterval = 0.2;
		public static double PulseStep = 0.1;

		public ProgressBarHandler()
		{
			Control = Gtk.ProgressBar.New();
			Control.Fraction = 0;
		}

		public bool Indeterminate
		{
			get => _indeterminate;
			set
			{
				_indeterminate = value;
				if (_indeterminate)
				{
					Control.PulseStep = PulseStep;
					if (_timeoutId == 0)
						_timeoutId = GLib.Functions.TimeoutAdd(0, (uint)(UpdateInterval * 1000), OnTimeout);
				}
				else
				{
					StopPulse();
					UpdateFraction();
				}
			}
		}

		bool OnTimeout()
		{
			if (!_indeterminate)
				return false;
			Control.Pulse();
			return true;
		}

		void StopPulse()
		{
			if (_timeoutId != 0)
			{
				GLib.Functions.SourceRemove(_timeoutId);
				_timeoutId = 0;
			}
		}

		public int MaxValue
		{
			get => _maxValue;
			set
			{
				_maxValue = value;
				UpdateFraction();
			}
		}

		public int MinValue
		{
			get => _minValue;
			set
			{
				_minValue = value;
				UpdateFraction();
			}
		}

		public int Value
		{
			get => _value;
			set
			{
				_value = value;
				UpdateFraction();
			}
		}

		void UpdateFraction()
		{
			var range = MaxValue - MinValue;
			Control.Fraction = range <= 0 ? 0 : Math.Clamp((double)(Value - MinValue) / range, 0, 1);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				StopPulse();
			base.Dispose(disposing);
		}
	}
}
