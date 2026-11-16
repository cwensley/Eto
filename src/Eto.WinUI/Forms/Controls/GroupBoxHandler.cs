namespace Eto.WinUI.Forms.Controls;

/// <summary>
/// WinUI has no group box, so this draws a caption above a bordered content area.
/// </summary>
public class GroupBoxHandler : WinUIPanel<muc.Grid, GroupBox, GroupBox.ICallback>, GroupBox.IHandler
{
	readonly EtoContentControl _container;
	readonly muc.TextBlock _header;
	readonly muc.Border _border;
	Font? _font;

	public GroupBoxHandler()
	{
		_container = new EtoContentControl { Handler = this };
		_header = new muc.TextBlock
		{
			Margin = new mux.Thickness(0, 0, 0, 4),
			Visibility = mux.Visibility.Collapsed
		};
		_border = new muc.Border
		{
			BorderThickness = new mux.Thickness(1),
			CornerRadius = new mux.CornerRadius(4),
			Padding = new mux.Thickness(4),
			BorderBrush = Eto.WinUI.Drawing.SystemColorsHandler.GetBrush("CardStrokeColorDefaultBrush")
		};
	}

	protected override muc.Grid CreateControl() => new muc.Grid();

	protected override void Initialize()
	{
		Control.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		Control.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Star) });
		muc.Grid.SetRow(_border, 1);
		Control.Children.Add(_header);
		Control.Children.Add(_border);
		_container.Content = Control;
		base.Initialize();
	}

	public override mux.FrameworkElement ContainerControl => _container;

	public override void SetContainerContent(mux.FrameworkElement content)
	{
		// sizing is applied by the outer container, not the content
		if (content is EtoContentControl contentControl)
			contentControl.Handler = null;
		_border.Child = content;
	}

	public override Color BackgroundColor
	{
		get => _container.Background.ToEtoColor();
		set => _container.Background = value.ToWinUIBrush();
	}

	public Font? Font
	{
		get => _font;
		set
		{
			_font = value;
			if (value != null)
			{
				_header.FontFamily = new muxm.FontFamily(value.FamilyName);
				_header.FontSize = value.Size * 96.0 / 72.0;
				_header.FontWeight = value.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
				_header.FontStyle = value.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
			}
			else
			{
				_header.ClearValue(muc.TextBlock.FontFamilyProperty);
				_header.ClearValue(muc.TextBlock.FontSizeProperty);
				_header.ClearValue(muc.TextBlock.FontWeightProperty);
				_header.ClearValue(muc.TextBlock.FontStyleProperty);
			}
			ContainerControl.InvalidateMeasure();
		}
	}

	public string Text
	{
		get => _header.Text;
		set
		{
			_header.Text = value ?? string.Empty;
			// collapse the caption row when there's no text so the padding stays even
			_header.Visibility = string.IsNullOrEmpty(value) ? mux.Visibility.Collapsed : mux.Visibility.Visible;
		}
	}

	public Color TextColor
	{
		get => _header.Foreground.ToEtoColor();
		set => _header.Foreground = value.ToWinUIBrush();
	}
}
