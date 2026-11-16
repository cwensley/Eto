using System.Globalization;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Eto.WinUI.Forms.Controls;

class EtoDateTimePickerGrid : global::Eto.WinUI.Forms.EtoGrid
{
	public IWinUIFrameworkElement? Handler { get; set; }

	protected override wf.Size MeasureOverride(wf.Size constraint)
	{
		return Handler?.MeasureOverride(constraint, base.MeasureOverride) ?? base.MeasureOverride(constraint);
	}
}

public class DateTimePickerHandler : WinUIBorderedControl<muc.Grid, DateTimePicker, DateTimePicker.ICallback>, DateTimePicker.IHandler
{
	const double DateWidth = 120;
	const double DateTimeWidth = 180;
	const double TimeWidth = 80;
	const double ElementSpacing = 6;

	readonly muc.CalendarDatePicker _datePicker;
	readonly EtoTextBox _timeTextBox;

	DateTimePickerMode _mode;
	DateTime? _value;
	DateTime? _minimum;
	DateTime? _maximum;
	DateTime? _lastReportedValue;
	int _suppressValueChanged;

	protected override bool PreventUserResize => true;

	public Font Font { get; set; } = null!;

	protected override wf.Size DefaultSize => new(
		_mode == DateTimePickerMode.DateTime ? DateTimeWidth : _mode == DateTimePickerMode.Time ? TimeWidth : DateWidth,
		double.NaN);

	protected override muc.Grid CreateControl() => new EtoDateTimePickerGrid { Handler = this };

	public DateTimePickerHandler()
	{
		_datePicker = new muc.CalendarDatePicker
		{
			HorizontalAlignment = mux.HorizontalAlignment.Stretch,
			VerticalAlignment = mux.VerticalAlignment.Center,
			PlaceholderText = string.Empty
		};
		_timeTextBox = new EtoTextBox
		{
			Handler = this,
			HorizontalAlignment = mux.HorizontalAlignment.Stretch,
			VerticalAlignment = mux.VerticalAlignment.Center,
			VerticalContentAlignment = mux.VerticalAlignment.Center,
			MinWidth = 70
		};
	}

	protected override void Initialize()
	{
		base.Initialize();

		Control.ColumnSpacing = ElementSpacing;
		_datePicker.DateChanged += DatePicker_DateChanged;
		_timeTextBox.LostFocus += TimeTextBox_LostFocus;
		_timeTextBox.KeyDown += TimeTextBox_KeyDown;

		Mode = DateTimePickerMode.Date;
		Value = null;
		ApplyTextColor();
		ApplyShowBorder();
	}

	public override mux.FrameworkElement FocusControl =>
		Mode.HasFlag(DateTimePickerMode.Date) ? _datePicker : _timeTextBox;

	public bool ShowBorder
	{
		get => !_datePicker.BorderThickness.ToEto().IsZero || !_timeTextBox.BorderThickness.ToEto().IsZero;
		set
		{
			Widget.Properties[nameof(ShowBorder)] = value;
			ApplyShowBorder();
		}
	}

	public override Color BackgroundColor
	{
		get => base.BackgroundColor;
		set
		{
			base.BackgroundColor = value;
			_datePicker.Background = value.ToWinUIBrush();
			_timeTextBox.Background = value.ToWinUIBrush();
		}
	}

	public Color TextColor
	{
		get => Widget.Properties.Get<Color?>(nameof(TextColor)) ?? Colors.Black;
		set
		{
			Widget.Properties[nameof(TextColor)] = value;
			ApplyTextColor();
		}
	}

	public DateTime? Value
	{
		get => _value;
		set
		{
			var clamped = Clamp(value);
			if (_value == clamped)
				return;

			_value = clamped;
			SyncControlsFromValue();
		}
	}

	public DateTime MinDate
	{
		get => _minimum ?? DateTime.MinValue;
		set
		{
			_minimum = value == DateTime.MinValue ? null : value;
			SyncDateRange();
			Value = _value;
		}
	}

	public DateTime MaxDate
	{
		get => _maximum ?? DateTime.MaxValue;
		set
		{
			_maximum = value == DateTime.MaxValue ? null : value;
			SyncDateRange();
			Value = _value;
		}
	}

	public DateTimePickerMode Mode
	{
		get => _mode;
		set
		{
			if (_mode == value)
				return;

			_mode = value;
			RebuildLayout();
			SyncControlsFromValue();
			UserPreferredSize = DefaultSize;
		}
	}

	void RebuildLayout()
	{
		Control.Children.Clear();
		Control.ColumnDefinitions.Clear();

		var showDate = _mode.HasFlag(DateTimePickerMode.Date);
		var showTime = _mode.HasFlag(DateTimePickerMode.Time);

		if (showDate && showTime)
		{
			Control.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
			Control.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = mux.GridLength.Auto });
			muc.Grid.SetColumn(_datePicker, 0);
			muc.Grid.SetColumn(_timeTextBox, 1);
			Control.Children.Add(_datePicker);
			Control.Children.Add(_timeTextBox);
		}
		else if (showDate)
		{
			Control.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
			muc.Grid.SetColumn(_datePicker, 0);
			Control.Children.Add(_datePicker);
		}
		else if (showTime)
		{
			Control.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
			muc.Grid.SetColumn(_timeTextBox, 0);
			Control.Children.Add(_timeTextBox);
		}

		ApplyTextColor();
		ApplyShowBorder();
	}

	void ApplyShowBorder()
	{
		var showBorder = Widget?.Properties.Get(nameof(ShowBorder), true) ?? true;
		var thickness = new mux.Thickness(showBorder ? 1 : 0);
		_datePicker.BorderThickness = thickness;
		_timeTextBox.BorderThickness = thickness;
	}

	void ApplyTextColor()
	{
		var brush = TextColor.ToWinUIBrush();
		_datePicker.Foreground = brush;
		_timeTextBox.Foreground = brush;
	}

	static DateTimeOffset ToPickerDate(DateTime value)
	{
		return new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified), TimeSpan.Zero);
	}

	void SyncDateRange()
	{
		_suppressValueChanged++;
		try
		{
			_datePicker.MinDate = _minimum.HasValue ? ToPickerDate(_minimum.Value) : DateTimeOffset.MinValue;
			_datePicker.MaxDate = _maximum.HasValue ? ToPickerDate(_maximum.Value) : DateTimeOffset.MaxValue;
		}
		finally
		{
			_suppressValueChanged--;
		}
	}

	void SyncControlsFromValue()
	{
		_suppressValueChanged++;
		try
		{
			SyncDateRange();
			_datePicker.Date = _value.HasValue ? ToPickerDate(_value.Value) : null;
			_timeTextBox.Text = FormatTimeText(_value);
		}
		finally
		{
			_suppressValueChanged--;
		}
	}

	static string FormatTimeText(DateTime? value)
	{
		if (value == null)
			return string.Empty;

		var format = CultureInfo.CurrentUICulture.DateTimeFormat;
		return value.Value.ToString(format.LongTimePattern, CultureInfo.CurrentUICulture);
	}

	DateTime? Clamp(DateTime? value)
	{
		if (value == null)
			return null;

		var clamped = value.Value;
		if (_minimum.HasValue && clamped < _minimum.Value)
			clamped = _minimum.Value;
		if (_maximum.HasValue && clamped > _maximum.Value)
			clamped = _maximum.Value;
		return clamped;
	}

	void DatePicker_DateChanged(muc.CalendarDatePicker sender, muc.CalendarDatePickerDateChangedEventArgs args)
	{
		if (_suppressValueChanged != 0)
			return;

		var date = sender.Date?.DateTime.Date;
		if (date == null)
		{
			UpdateValueFromUser(null);
			return;
		}

		var current = _value;
		if (_mode.HasFlag(DateTimePickerMode.Time))
		{
			var time = current?.TimeOfDay ?? ParseTime(_timeTextBox.Text) ?? TimeSpan.Zero;
			UpdateValueFromUser(date.Value + time);
		}
		else
		{
			UpdateValueFromUser(date.Value);
		}
	}

	void TimeTextBox_LostFocus(object sender, mux.RoutedEventArgs e)
	{
		CommitTimeText();
	}

	void TimeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key != VirtualKey.Enter)
			return;

		CommitTimeText();
		e.Handled = true;
	}

	void CommitTimeText()
	{
		if (_suppressValueChanged != 0)
			return;

		var text = _timeTextBox.Text;
		if (string.IsNullOrWhiteSpace(text))
		{
			if (_mode == DateTimePickerMode.Time)
				UpdateValueFromUser(null);
			else
				_timeTextBox.Text = FormatTimeText(_value);
			return;
		}

		var time = ParseTime(text);
		if (time == null)
		{
			_timeTextBox.Text = FormatTimeText(_value);
			return;
		}

		var current = _value ?? DateTime.Today;
		if (_mode == DateTimePickerMode.Time)
			UpdateValueFromUser(current.Date + time.Value);
		else
		{
			var date = _datePicker.Date?.DateTime.Date ?? current.Date;
			UpdateValueFromUser(date + time.Value);
		}
	}

	static TimeSpan? ParseTime(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return null;

		if (DateTime.TryParse(text, CultureInfo.CurrentUICulture, DateTimeStyles.NoCurrentDateDefault, out var dateTime))
			return dateTime.TimeOfDay;
		if (TimeSpan.TryParse(text, CultureInfo.CurrentUICulture, out var time))
			return time;
		return null;
	}

	void UpdateValueFromUser(DateTime? value)
	{
		var clamped = Clamp(value);
		if (_value == clamped)
		{
			SyncControlsFromValue();
			return;
		}

		_value = clamped;
		SyncControlsFromValue();
		ReportValueChanged();
	}

	void ReportValueChanged()
	{
		var value = _value;
		if (_lastReportedValue == value)
			return;

		if (_lastReportedValue != null && value != null && Math.Abs((_lastReportedValue.Value - value.Value).TotalSeconds) < 1)
			return;

		_lastReportedValue = value;
		Callback.OnValueChanged(Widget, EventArgs.Empty);
	}
}
