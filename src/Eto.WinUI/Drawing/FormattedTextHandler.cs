using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace Eto.WinUI.Drawing;

public class FormattedTextHandler : WidgetHandler<FormattedText>, FormattedText.IHandler
{
	public FormattedTextWrapMode Wrap { get; set; }

	public FormattedTextTrimming Trimming { get; set; }

	public FormattedTextAlignment Alignment { get; set; }

	public Font Font { get; set; } = SystemFonts.Default();

	public string Text { get; set; } = string.Empty;

	public SizeF MaximumSize { get; set; } = SizeF.MaxValue;

	public Brush ForegroundBrush { get; set; } = new SolidBrush(Colors.Black);

	public SizeF Measure()
	{
		if (string.IsNullOrEmpty(Text))
			return SizeF.Empty;
		using var layout = CreateLayout(CanvasDevice.GetSharedDevice());
		var bounds = layout.LayoutBoundsIncludingTrailingWhitespace;
		return new SizeF((float)bounds.Width, (float)bounds.Height);
	}

	/// <summary>Creates a Win2D layout of the text, which the caller must dispose.</summary>
	internal CanvasTextLayout CreateLayout(ICanvasResourceCreator creator)
	{
		var fontHandler = (FontHandler)Font.Handler;
		// Win2D treats 0 as unbounded, so the layout is measured from its content
		var width = MaximumSize.Width >= float.MaxValue / 2 ? 0f : MaximumSize.Width;
		var height = MaximumSize.Height >= float.MaxValue / 2 ? 0f : MaximumSize.Height;
		var layout = fontHandler.CreateTextLayout(creator, Text ?? string.Empty, width, height);
		layout.WordWrapping = width <= 0 ? CanvasWordWrapping.NoWrap : Wrap switch
		{
			FormattedTextWrapMode.None => CanvasWordWrapping.NoWrap,
			FormattedTextWrapMode.Character => CanvasWordWrapping.Character,
			_ => CanvasWordWrapping.Wrap
		};
		layout.HorizontalAlignment = Alignment switch
		{
			FormattedTextAlignment.Center => CanvasHorizontalAlignment.Center,
			FormattedTextAlignment.Right => CanvasHorizontalAlignment.Right,
			FormattedTextAlignment.Justify => CanvasHorizontalAlignment.Justified,
			_ => CanvasHorizontalAlignment.Left
		};
		switch (Trimming)
		{
			case FormattedTextTrimming.CharacterEllipsis:
				layout.TrimmingGranularity = CanvasTextTrimmingGranularity.Character;
				layout.TrimmingSign = CanvasTrimmingSign.Ellipsis;
				break;
			case FormattedTextTrimming.WordEllipsis:
				layout.TrimmingGranularity = CanvasTextTrimmingGranularity.Word;
				layout.TrimmingSign = CanvasTrimmingSign.Ellipsis;
				break;
			default:
				layout.TrimmingGranularity = CanvasTextTrimmingGranularity.None;
				break;
		}
		return layout;
	}
}
