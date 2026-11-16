using Eto.WinUI.Drawing;

namespace Eto.WinUI.Forms.Controls;

public class TabPageHandler : WinUIPanel<muc.TabViewItem, TabPage, TabPage.ICallback>, TabPage.IHandler
{
	Image? _image;
	readonly muc.StackPanel _header;
	readonly muc.Image _headerImage;
	readonly muc.TextBlock _headerText;
	const int ImageSpacing = 4;

	public TabPageHandler()
	{
		_header = new muc.StackPanel { Orientation = muc.Orientation.Horizontal };
		_headerImage = new muc.Image { Stretch = muxm.Stretch.None, Visibility = mux.Visibility.Collapsed };
		_headerText = new muc.TextBlock { VerticalAlignment = mux.VerticalAlignment.Center };
		_header.Children.Add(_headerImage);
		_header.Children.Add(_headerText);
	}

	protected override muc.TabViewItem CreateControl() => new muc.TabViewItem
	{
		IsClosable = false,
		HorizontalContentAlignment = mux.HorizontalAlignment.Stretch,
		VerticalContentAlignment = mux.VerticalAlignment.Stretch
	};

	protected override void Initialize()
	{
		Control.Header = _header;
		base.Initialize();
	}

	public override mux.FrameworkElement ContainerControl => Control;

	public override void SetContainerContent(mux.FrameworkElement content) => Control.Content = content;

	public string Text
	{
		get => _headerText.Text;
		set
		{
			_headerText.Text = value ?? string.Empty;
			SetMargin();
		}
	}

	public Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			_headerImage.Source = value switch
			{
				Bitmap bitmap => bitmap.ToBitmapSource(),
				Icon icon => icon.GetFrame(1).Bitmap.ToBitmapSource(),
				_ => (value?.Handler as IWinUIImage)?.GetImageClosestToSize(1, null)
			};
			_headerImage.Visibility = value != null ? mux.Visibility.Visible : mux.Visibility.Collapsed;
			SetMargin();
		}
	}

	void SetMargin()
	{
		_headerImage.Margin = _image != null && !string.IsNullOrEmpty(Text)
			? new mux.Thickness(0, 0, ImageSpacing, 0)
			: new mux.Thickness();
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override bool Visible
	{
		get => base.Visible;
		set
		{
			if (value == Visible)
				return;
			base.Visible = value;
			(Widget.Parent?.Handler as TabControlHandler)?.SetPageVisible(Widget, value);
		}
	}
}
