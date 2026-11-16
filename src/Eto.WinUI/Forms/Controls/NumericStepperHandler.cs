using CommunityToolkit.WinUI;
using Windows.Globalization.NumberFormatting;

namespace Eto.WinUI.Forms.Controls;

public class NumericStepperHandler : WinUIBorderedControl<muc.NumberBox, NumericStepper, NumericStepper.ICallback>, NumericStepper.IHandler
{
	muc.TextBox? _textBox;
	public muc.TextBox TextBox => _textBox ??= Control.FindVisualChild<muc.TextBox>();

	static readonly object ReadOnlyProperty = new object();

	// the value and its limits are kept here so rounding and change notifications match the other platforms
	double _value;
	double _minValue = double.MinValue;
	double _maxValue = double.MaxValue;
	int _decimalPlaces;
	int _maximumDecimalPlaces;
	int _suppressNative;

	public bool ReadOnly
	{
		get => GetNullableValue(ReadOnlyProperty, TextBox, t => t.IsReadOnly, false);
		set => SetNullableValue(ReadOnlyProperty, value, () => TextBox, (t, v) => t.IsReadOnly = v);
	}

	public double Value
	{
		get => GetValue(_value);
		set => SetValue(value);
	}

	double GetValue(double value)
	{
		value = Math.Max(_minValue, Math.Min(_maxValue, value));
		if (!string.IsNullOrEmpty(FormatString))
			return value;
		return Math.Round(value, Math.Min(15, _maximumDecimalPlaces));
	}

	void SetValue(double value)
	{
		var oldValue = Value;
		_value = Math.Max(_minValue, Math.Min(_maxValue, value));
		UpdateNativeValue();
		if (oldValue != Value)
			Callback.OnValueChanged(Widget, EventArgs.Empty);
	}

	void UpdateNativeValue()
	{
		_suppressNative++;
		try
		{
			Control.Minimum = _minValue;
			Control.Maximum = _maxValue;
			Control.Value = Value;
		}
		finally
		{
			_suppressNative--;
		}
	}

	public double MinValue
	{
		get => _minValue;
		set
		{
			var oldValue = Value;
			_minValue = value;
			if (_maxValue < value)
				_maxValue = value;
			_value = Math.Max(_minValue, Math.Min(_maxValue, _value));
			UpdateNativeValue();
			if (oldValue != Value)
				Callback.OnValueChanged(Widget, EventArgs.Empty);
		}
	}

	public double MaxValue
	{
		get => _maxValue;
		set
		{
			var oldValue = Value;
			_maxValue = value;
			if (_minValue > value)
				_minValue = value;
			_value = Math.Max(_minValue, Math.Min(_maxValue, _value));
			UpdateNativeValue();
			if (oldValue != Value)
				Callback.OnValueChanged(Widget, EventArgs.Empty);
		}
	}

	public int DecimalPlaces
	{
		get => _decimalPlaces;
		set
		{
			_decimalPlaces = value;
			_maximumDecimalPlaces = Math.Max(_maximumDecimalPlaces, value);
			UpdateFormatter();
		}
	}

	public int MaximumDecimalPlaces
	{
		get => _maximumDecimalPlaces;
		set
		{
			_maximumDecimalPlaces = value;
			_decimalPlaces = Math.Min(_decimalPlaces, value);
			UpdateFormatter();
		}
	}

	void UpdateFormatter()
	{
		var formatter = new DecimalFormatter
		{
			IntegerDigits = 1,
			FractionDigits = _decimalPlaces,
			IsGrouped = false
		};
		try
		{
			formatter.NumberRounder = new IncrementNumberRounder
			{
				Increment = Math.Pow(10, -Math.Min(15, _maximumDecimalPlaces)),
				RoundingAlgorithm = RoundingAlgorithm.RoundHalfAwayFromZero
			};
		}
		catch (ArgumentException)
		{
			// not every increment is accepted, the value is still rounded when read
		}
		Control.NumberFormatter = formatter;
		UpdateNativeValue();
	}

	public double Increment
	{
		get => Control.SmallChange;
		set
		{
			Control.SmallChange = value;
			Control.LargeChange = value * 10;
		}
	}

	public string? FormatString { get; set; }
	public CultureInfo? CultureInfo { get; set; }

	public bool Wrap
	{
		get => Control.IsWrapEnabled;
		set => Control.IsWrapEnabled = value;
	}

	public TextAlignment TextAlignment
	{
		get => TextBox.TextAlignment.ToEto();
		set => TextBox.TextAlignment = value.ToWinUI();
	}

	public Color TextColor
	{
		get => Control.Foreground.ToEtoColor();
		set => Control.Foreground = value.ToWinUIBrush();
	}

	static readonly object Font_Key = new object();

	public Font Font
	{
		get => Widget.Properties.Get<Font>(Font_Key) ?? Eto.WinUI.Drawing.FontHandler.GetFont(Control);
		set
		{
			Widget.Properties.Set(Font_Key, value);
			Eto.WinUI.Drawing.FontHandler.Apply(Control, value);
		}
	}

	protected override muc.NumberBox CreateControl() => new muc.NumberBox();

	protected override void Initialize()
	{
		base.Initialize();
		Control.SpinButtonPlacementMode = muc.NumberBoxSpinButtonPlacementMode.Inline;
		Control.SmallChange = 1;
		Control.Minimum = _minValue;
		Control.Maximum = _maxValue;
		UpdateFormatter();
		Control.ValueChanged += Control_ValueChanged;
	}

	void Control_ValueChanged(muc.NumberBox sender, muc.NumberBoxValueChangedEventArgs args)
	{
		if (_suppressNative > 0)
			return;
		if (double.IsNaN(args.NewValue))
		{
			// cleared by the user, show the current value again
			UpdateNativeValue();
			return;
		}
		SetValue(args.NewValue);
		if (Control.Value != Value)
			UpdateNativeValue();
	}
}
