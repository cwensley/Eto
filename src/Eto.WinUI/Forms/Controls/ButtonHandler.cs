namespace Eto.WinUI.Forms.Controls;

public class ButtonHandler : WinUIControl<muc.Button, Button, Button.ICallback>, Button.IHandler
{
	static readonly object Image_Key = new object();
	static readonly object ImagePosition_Key = new object();
	static readonly object MinimumSize_Key = new object();
	static readonly Size DefaultMinimumSize = new(80, 23);
	const int ImageLabelSpacing = 2;

	readonly muc.TextBlock _textBlock = new()
	{
		TextAlignment = mux.TextAlignment.Center,
		HorizontalAlignment = mux.HorizontalAlignment.Center,
		VerticalAlignment = mux.VerticalAlignment.Center
	};

	readonly muc.Grid _contentGrid = new();
	muc.Image? _imagePart;

	public string Text
	{
		get => _textBlock.Text;
		set
		{
			var newValue = value ?? string.Empty;
			if (_textBlock.Text == newValue)
				return;

			_textBlock.Text = newValue;
			UpdateContent();
		}
	}

	public Image? Image
	{
		get => Widget.Properties.Get<Image>(Image_Key);
		set
		{
			if (!Widget.Properties.TrySet(Image_Key, value))
				return;

			if (value == null)
			{
				_imagePart = null;
			}
			else
			{
				_imagePart ??= new muc.Image
				{
					HorizontalAlignment = mux.HorizontalAlignment.Center,
					VerticalAlignment = mux.VerticalAlignment.Center,
					Stretch = muxm.Stretch.None
				};
				_imagePart.Source = ToImageSource(value);
			}

			UpdateContent();
		}
	}

	public ButtonImagePosition ImagePosition
	{
		get => Widget.Properties.Get<ButtonImagePosition>(ImagePosition_Key);
		set
		{
			if (ImagePosition == value)
				return;

			Widget.Properties[ImagePosition_Key] = value;
			UpdateContent();
		}
	}

	public Size MinimumSize
	{
		get => Widget.Properties.Get<Size?>(MinimumSize_Key) ?? DefaultMinimumSize;
		set
		{
			if (MinimumSize == value)
				return;

			Widget.Properties[MinimumSize_Key] = value;
			Control.SetMinSize(value);
			Control.InvalidateMeasure();
		}
	}

	public bool UseMnemonic { get; set; }
	public bool AlwaysShowMnemonic { get; set; }

	protected override muc.Button CreateControl() => new muc.Button();

	protected override void Initialize()
	{
		base.Initialize();

		_contentGrid.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Auto) });
		_contentGrid.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
		_contentGrid.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Auto) });
		_contentGrid.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Auto) });
		_contentGrid.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Star) });
		_contentGrid.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Auto) });

		Control.HorizontalContentAlignment = mux.HorizontalAlignment.Stretch;
		Control.VerticalContentAlignment = mux.VerticalAlignment.Stretch;
		Control.Content = _contentGrid;
		Control.SetMinSize(MinimumSize);
		Control.Click += Control_Click;
		UpdateContent();
	}

	private void Control_Click(object sender, mux.RoutedEventArgs e)
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}

	void UpdateContent()
	{
		_contentGrid.Children.Clear();
		_textBlock.Margin = new mux.Thickness(0);

		var hasText = !string.IsNullOrEmpty(_textBlock.Text);
		var hasImage = _imagePart?.Source != null;

		Control.HorizontalContentAlignment = mux.HorizontalAlignment.Center;
		Control.VerticalContentAlignment = mux.VerticalAlignment.Center;

		if (hasImage && _imagePart != null)
		{
			muc.Grid.SetColumn(_imagePart, 1);
			muc.Grid.SetRow(_imagePart, 1);
			_contentGrid.Children.Add(_imagePart);
		}

		if (hasText)
		{
			muc.Grid.SetColumn(_textBlock, 1);
			muc.Grid.SetRow(_textBlock, 1);
			_contentGrid.Children.Add(_textBlock);
		}

		if (hasImage && hasText && _imagePart != null)
			SetImagePosition();
	}

	void SetImagePosition()
	{
		if (_imagePart == null)
			return;

		mux.Thickness textMargin;
		int imageColumn;
		int imageRow;

		switch (ImagePosition)
		{
			case ButtonImagePosition.Left:
				imageColumn = 0;
				imageRow = 1;
				Control.HorizontalContentAlignment = mux.HorizontalAlignment.Stretch;
				Control.VerticalContentAlignment = mux.VerticalAlignment.Center;
				textMargin = new mux.Thickness(ImageLabelSpacing, 0, 0, 0);
				break;
			case ButtonImagePosition.Right:
				imageColumn = 2;
				imageRow = 1;
				Control.HorizontalContentAlignment = mux.HorizontalAlignment.Stretch;
				Control.VerticalContentAlignment = mux.VerticalAlignment.Center;
				textMargin = new mux.Thickness(0, 0, ImageLabelSpacing, 0);
				break;
			case ButtonImagePosition.Above:
				imageColumn = 1;
				imageRow = 0;
				Control.HorizontalContentAlignment = mux.HorizontalAlignment.Center;
				Control.VerticalContentAlignment = mux.VerticalAlignment.Stretch;
				textMargin = new mux.Thickness(0, ImageLabelSpacing, 0, 0);
				break;
			case ButtonImagePosition.Below:
				imageColumn = 1;
				imageRow = 2;
				Control.HorizontalContentAlignment = mux.HorizontalAlignment.Center;
				Control.VerticalContentAlignment = mux.VerticalAlignment.Stretch;
				textMargin = new mux.Thickness(0, 0, 0, ImageLabelSpacing);
				break;
			case ButtonImagePosition.Overlay:
				imageColumn = 1;
				imageRow = 1;
				Control.HorizontalContentAlignment = mux.HorizontalAlignment.Center;
				Control.VerticalContentAlignment = mux.VerticalAlignment.Center;
				textMargin = new mux.Thickness(0);
				break;
			default:
				throw new NotSupportedException();
		}

		muc.Grid.SetColumn(_imagePart, imageColumn);
		muc.Grid.SetRow(_imagePart, imageRow);
		_textBlock.Margin = textMargin;
	}

	static muxm.ImageSource ToImageSource(Image image) => image switch
	{
		Bitmap bitmap => bitmap.ToBitmapSource(),
		Icon icon => icon.GetFrame(1).Bitmap.ToBitmapSource(),
		_ => throw new NotSupportedException($"Image type '{image.GetType().FullName}' is not supported by Eto.WinUI buttons.")
	};

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
