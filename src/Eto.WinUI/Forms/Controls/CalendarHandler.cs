namespace Eto.WinUI.Forms.Controls;

public class CalendarHandler : WinUIBorderedControl<muc.CalendarView, Calendar, Calendar.ICallback>, Calendar.IHandler
{
	// state is kept here, CalendarView only supports a set of individual dates
	DateTime _minDate = DateTime.MinValue;
	DateTime _maxDate = DateTime.MaxValue;
	Range<DateTime> _selectedRange = new Range<DateTime>(DateTime.Today);
	Range<DateTime>? _oldRange;
	CalendarMode _mode = CalendarMode.Single;
	int _suppressNative;

	protected override muc.CalendarView CreateControl() => new muc.CalendarView
	{
		SelectionMode = muc.CalendarViewSelectionMode.Single
	};

	protected override void Initialize()
	{
		base.Initialize();
		UpdateNativeSelection();
		Control.SelectedDatesChanged += Control_SelectedDatesChanged;
	}

	void Control_SelectedDatesChanged(muc.CalendarView sender, muc.CalendarViewSelectedDatesChangedEventArgs args)
	{
		if (_suppressNative > 0)
			return;

		var dates = Control.SelectedDates.Select(r => r.Date).ToList();
		if (dates.Count == 0)
		{
			// don't allow the user to clear the selection
			UpdateNativeSelection();
			return;
		}

		Range<DateTime> range;
		if (_mode == CalendarMode.Range)
		{
			// clicking a date extends the range from the previous start
			var added = args.AddedDates.Select(r => r.Date).ToList();
			if (added.Count > 0)
			{
				var date = added[0];
				range = date < _selectedRange.Start || _selectedRange.Start != _selectedRange.End
					? new Range<DateTime>(date)
					: new Range<DateTime>(_selectedRange.Start, date);
			}
			else
				range = new Range<DateTime>(dates.Min(), dates.Max());
		}
		else
			range = new Range<DateTime>(dates[0]);

		SetRange(range);
	}

	void UpdateNativeSelection()
	{
		_suppressNative++;
		try
		{
			Control.SelectedDates.Clear();
			if (_mode == CalendarMode.Range)
			{
				for (var date = _selectedRange.Start; date <= _selectedRange.End; date = date.AddDays(1))
					Control.SelectedDates.Add(new DateTimeOffset(date));
			}
			else
				Control.SelectedDates.Add(new DateTimeOffset(_selectedRange.Start));
			Control.SetDisplayDate(new DateTimeOffset(_selectedRange.Start));
		}
		finally
		{
			_suppressNative--;
		}
	}

	void SetRange(Range<DateTime> value, bool raiseDateChanged = true)
	{
		var old = _selectedRange;
		if (value == old)
			return;
		_selectedRange = value;
		_oldRange = null;
		UpdateNativeSelection();
		if (raiseDateChanged && old.Start != value.Start)
			Callback.OnSelectedDateChanged(Widget, EventArgs.Empty);
		Callback.OnSelectedRangeChanged(Widget, EventArgs.Empty);
	}

	DateTime Clamp(DateTime date)
	{
		if (date > _maxDate)
			date = _maxDate;
		if (date < _minDate)
			date = _minDate;
		return date;
	}

	public DateTime MinDate
	{
		get => _minDate;
		set
		{
			_minDate = value;
			if (value == DateTime.MinValue)
				Control.ClearValue(muc.CalendarView.MinDateProperty);
			else
				Control.MinDate = new DateTimeOffset(value.Date);
			if (SelectedDate < value)
				SelectedDate = value;
		}
	}

	public DateTime MaxDate
	{
		get => _maxDate;
		set
		{
			_maxDate = value;
			if (value == DateTime.MaxValue)
				Control.ClearValue(muc.CalendarView.MaxDateProperty);
			else
				Control.MaxDate = new DateTimeOffset(value.Date);
			if (SelectedDate > value)
				SelectedDate = value;
		}
	}

	public DateTime SelectedDate
	{
		get => _selectedRange.Start;
		set
		{
			var date = Clamp(value);
			if (date != SelectedDate || _selectedRange.End != date)
				SetRange(new Range<DateTime>(date));
		}
	}

	public Range<DateTime> SelectedRange
	{
		get => _selectedRange;
		set
		{
			if (_mode != CalendarMode.Range)
				value = new Range<DateTime>(value.Start);
			SetRange(value);
		}
	}

	public CalendarMode Mode
	{
		get => _mode;
		set
		{
			if (_mode == value)
				return;
			var range = _selectedRange;
			_mode = value;
			_suppressNative++;
			Control.SelectionMode = value == CalendarMode.Range ? muc.CalendarViewSelectionMode.Multiple : muc.CalendarViewSelectionMode.Single;
			_suppressNative--;
			Range<DateTime> newRange;
			if (value == CalendarMode.Range)
				newRange = _oldRange ?? range;
			else
				newRange = new Range<DateTime>(range.Start);

			_selectedRange = newRange;
			// remember the range so switching back to range mode restores it
			_oldRange = value == CalendarMode.Single ? range : null;
			UpdateNativeSelection();
			if (range != newRange)
				Callback.OnSelectedRangeChanged(Widget, EventArgs.Empty);
		}
	}
}
