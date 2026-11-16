using ctk = CommunityToolkit.WinUI.UI.Controls;

namespace Eto.WinUI.Forms.Cells;

static class CellImages
{
	public const int ImageSize = 16;

	public static muxm.ImageSource? ToImageSource(Image? image) => image switch
	{
		Bitmap bitmap => bitmap.ToBitmapSource(),
		Icon icon => icon.GetFrame(1, new Size(ImageSize, ImageSize))?.Bitmap?.ToBitmapSource(),
		_ => null
	};

	public static muc.Image CreateImage() => new muc.Image
	{
		Width = ImageSize,
		Height = ImageSize,
		Stretch = muxm.Stretch.Uniform,
		VerticalAlignment = mux.VerticalAlignment.Center,
	};
}

public class ImageViewCellHandler : CellHandler<ImageViewCell, ImageViewCell.ICallback>, ImageViewCell.IHandler
{
	// WinUI's Image always uses high quality scaling, so this is only kept for the API.
	public ImageInterpolation ImageInterpolation { get; set; }

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		var image = CellImages.CreateImage();
		image.Margin = new mux.Thickness(4, 2, 4, 2);
		image.HorizontalAlignment = mux.HorizontalAlignment.Center;
		return image;
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		if (element is muc.Image image)
			image.Source = CellImages.ToImageSource(dataItem != null ? Widget.Binding?.GetValue(dataItem) : null);
		FormatCell(element, cell, dataItem);
	}
}

public class ImageTextCellHandler : CellHandler<ImageTextCell, ImageTextCell.ICallback>, ImageTextCell.IHandler
{
	TextAlignment _textAlignment;
	VerticalAlignment _verticalAlignment = VerticalAlignment.Center;

	// WinUI's Image always uses high quality scaling, so this is only kept for the API.
	public ImageInterpolation ImageInterpolation { get; set; }

	public TextAlignment TextAlignment
	{
		get => _textAlignment;
		set
		{
			_textAlignment = value;
			Refresh();
		}
	}

	public VerticalAlignment VerticalAlignment
	{
		get => _verticalAlignment;
		set
		{
			_verticalAlignment = value;
			Refresh();
		}
	}

	public AutoSelectMode AutoSelectMode { get; set; } = AutoSelectMode.OnFocus;

	public override bool SupportsEditing => true;

	public override mux.FrameworkElement CreateElement(ctk.DataGridCell cell, bool isEditing)
	{
		var panel = new muc.Grid { Padding = new mux.Thickness(4, 0, 0, 0) };
		panel.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = mux.GridLength.Auto });
		panel.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });
		var image = CellImages.CreateImage();
		image.Margin = new mux.Thickness(0, 2, 4, 2);
		panel.Children.Add(image);
		mux.FrameworkElement text = isEditing
			? new muc.TextBox()
			: new muc.TextBlock { Margin = new mux.Thickness(0, 0, 12, 0) };
		muc.Grid.SetColumn(text, 1);
		panel.Children.Add(text);
		return panel;
	}

	public override void ConfigureElement(mux.FrameworkElement element, ctk.DataGridCell cell, object? dataItem, bool isEditing)
	{
		var image = FindChild<muc.Image>(element);
		if (image != null)
			image.Source = CellImages.ToImageSource(dataItem != null ? Widget.ImageBinding?.GetValue(dataItem) : null);
		var text = dataItem != null ? Widget.TextBinding?.GetValue(dataItem) ?? string.Empty : string.Empty;
		if (FindChild<muc.TextBox>(element) is muc.TextBox textBox)
		{
			textBox.Text = text;
			textBox.TextAlignment = TextAlignment.ToWinUI();
			textBox.VerticalAlignment = VerticalAlignment.ToWinUI();
		}
		else if (FindChild<muc.TextBlock>(element) is muc.TextBlock textBlock)
		{
			textBlock.Text = text;
			textBlock.TextAlignment = TextAlignment.ToWinUI();
			textBlock.VerticalAlignment = VerticalAlignment.ToWinUI();
			textBlock.TextTrimming = mux.TextTrimming.CharacterEllipsis;
		}
		FormatCell(element, cell, dataItem);
	}

	public override object? PrepareCellForEdit(mux.FrameworkElement editingElement, mux.RoutedEventArgs? args)
	{
		if (FindChild<muc.TextBox>(editingElement) is muc.TextBox textBox)
		{
			textBox.Focus(mux.FocusState.Programmatic);
			if (AutoSelectMode == AutoSelectMode.Never)
				textBox.Select(textBox.Text.Length, 0);
			else
				textBox.SelectAll();
			return textBox.Text;
		}
		return null;
	}

	public override void CommitCellEdit(mux.FrameworkElement editingElement, object? dataItem)
	{
		if (FindChild<muc.TextBox>(editingElement) is muc.TextBox textBox && dataItem != null)
			Widget.TextBinding?.SetValue(dataItem, textBox.Text);
	}
}
