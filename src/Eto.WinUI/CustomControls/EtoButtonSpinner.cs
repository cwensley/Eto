using Eto.WinUI.Forms;

namespace Eto.WinUI.CustomControls;

public enum SpinDirection
{
	Increase,
	Decrease
}

[Flags]
public enum ValidSpinDirections
{
	None = 0,
	Increase = 1,
	Decrease = 2
}

public class SpinEventArgs : EventArgs
{
	public SpinEventArgs(SpinDirection direction)
	{
		Direction = direction;
	}

	public SpinDirection Direction { get; }

	public bool Handled { get; set; }
}

public sealed class EtoButtonSpinner : muc.ContentControl
{
	SpinButton? _spinButton;
	mux.FrameworkElement? _contentPresenter;

	public IWinUIFrameworkElement? Handler { get; set; }

	public EtoButtonSpinner()
	{
		DefaultStyleKey = typeof(EtoButtonSpinner);
		HorizontalContentAlignment = mux.HorizontalAlignment.Stretch;
		VerticalContentAlignment = mux.VerticalAlignment.Stretch;
		IsEnabledChanged += EtoButtonSpinner_IsEnabledChanged;
	}

	public event EventHandler<SpinEventArgs>? Spin;

	public static readonly mux.DependencyProperty ValidSpinDirectionProperty =
		mux.DependencyProperty.Register(
			nameof(ValidSpinDirection),
			typeof(ValidSpinDirections),
			typeof(EtoButtonSpinner),
			new mux.PropertyMetadata(ValidSpinDirections.Increase | ValidSpinDirections.Decrease, OnSpinnerPropertyChanged));

	public static readonly mux.DependencyProperty ShowButtonSpinnerProperty =
		mux.DependencyProperty.Register(
			nameof(ShowButtonSpinner),
			typeof(bool),
			typeof(EtoButtonSpinner),
			new mux.PropertyMetadata(true, OnSpinnerPropertyChanged));

	public static readonly mux.DependencyProperty ShowContentAreaProperty =
		mux.DependencyProperty.Register(
			nameof(ShowContentArea),
			typeof(bool),
			typeof(EtoButtonSpinner),
			new mux.PropertyMetadata(true, OnSpinnerPropertyChanged));

	public ValidSpinDirections ValidSpinDirection
	{
		get => (ValidSpinDirections)GetValue(ValidSpinDirectionProperty);
		set => SetValue(ValidSpinDirectionProperty, value);
	}

	public bool ShowButtonSpinner
	{
		get => (bool)GetValue(ShowButtonSpinnerProperty);
		set => SetValue(ShowButtonSpinnerProperty, value);
	}

	public bool ShowContentArea
	{
		get => (bool)GetValue(ShowContentAreaProperty);
		set => SetValue(ShowContentAreaProperty, value);
	}

	protected override wf.Size MeasureOverride(wf.Size availableSize)
	{
		return Handler?.MeasureOverride(availableSize, base.MeasureOverride) ?? base.MeasureOverride(availableSize);
	}

	protected override void OnApplyTemplate()
	{
		if (_spinButton != null)
		{
			_spinButton.UpClicked -= SpinButton_UpClicked;
			_spinButton.DownClicked -= SpinButton_DownClicked;
		}

		base.OnApplyTemplate();

		_spinButton = GetTemplateChild("PART_SpinButton") as SpinButton;
		_contentPresenter = GetTemplateChild("PART_ContentPresenter") as mux.FrameworkElement;

		if (_spinButton != null)
		{
			_spinButton.UpClicked += SpinButton_UpClicked;
			_spinButton.DownClicked += SpinButton_DownClicked;
		}

		UpdateTemplateState();
	}

	void SpinButton_UpClicked(object sender, mux.RoutedEventArgs e) => RaiseSpin(SpinDirection.Increase);

	void SpinButton_DownClicked(object sender, mux.RoutedEventArgs e) => RaiseSpin(SpinDirection.Decrease);

	void RaiseSpin(SpinDirection direction)
	{
		if (!CanSpin(direction))
			return;

		var args = new SpinEventArgs(direction);
		Spin?.Invoke(this, args);

		if (Content is muc.Control control)
			control.Focus(mux.FocusState.Programmatic);
	}

	bool CanSpin(SpinDirection direction)
	{
		if (!IsEnabled || !ShowButtonSpinner)
			return false;

		return direction == SpinDirection.Increase
			? ValidSpinDirection.HasFlag(ValidSpinDirections.Increase)
			: ValidSpinDirection.HasFlag(ValidSpinDirections.Decrease);
	}

	void UpdateTemplateState()
	{
		if (_spinButton != null)
		{
			_spinButton.Visibility = ShowButtonSpinner ? mux.Visibility.Visible : mux.Visibility.Collapsed;
			_spinButton.UpEnabled = IsEnabled && ShowButtonSpinner && ValidSpinDirection.HasFlag(ValidSpinDirections.Increase);
			_spinButton.DownEnabled = IsEnabled && ShowButtonSpinner && ValidSpinDirection.HasFlag(ValidSpinDirections.Decrease);
		}

		if (_contentPresenter != null)
			_contentPresenter.Visibility = ShowContentArea ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	void EtoButtonSpinner_IsEnabledChanged(object sender, mux.DependencyPropertyChangedEventArgs e)
	{
		UpdateTemplateState();
	}

	static void OnSpinnerPropertyChanged(mux.DependencyObject d, mux.DependencyPropertyChangedEventArgs e)
	{
		((EtoButtonSpinner)d).UpdateTemplateState();
	}
}
