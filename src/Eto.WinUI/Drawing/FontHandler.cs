using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using wut = Windows.UI.Text;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Font handler that only holds a description of the font so it can be used from any thread.
/// </summary>
public class FontHandler : WidgetHandler<Font>, Font.IHandler
{
	// WinUI's default control font resolves to this at runtime
	const string XamlAutoFontFamily = "XamlAutoFontFamily";
	const string DefaultFamilyName = "Segoe UI";

	FontFamily? _family;
	FontTypeface? _typeface;
	CanvasFontFace? _face;

	public FontHandler()
	{
	}

	/// <summary>Creates a font from native values, with the size in device independent pixels.</summary>
	public FontHandler(string? familyName, double pixelSize, wut.FontWeight weight, wut.FontStyle style, wut.FontStretch stretch, FontDecoration decoration = FontDecoration.None)
	{
		if (string.IsNullOrEmpty(familyName) || familyName == XamlAutoFontFamily)
			familyName = DefaultFamilyName;
		_family = new FontFamily(new FontFamilyHandler(familyName!, familyName!));
		Size = PixelsToPoints(pixelSize);
		Weight = weight;
		Style = style;
		Stretch = stretch;
		FontStyle = FontTypefaceHandler.GetStyle(weight, style);
		FontDecoration = decoration;
	}

	public static double PointsToPixels(double points) => points * 96.0 / 72.0;

	public static float PixelsToPoints(double pixels) => (float)(pixels * 72.0 / 96.0);

	/// <summary>Gets the size in points.</summary>
	public float Size { get; private set; }

	/// <summary>Gets the size in device independent pixels, as used by XAML and Win2D.</summary>
	public float PixelSize => (float)PointsToPixels(Size);

	public wut.FontWeight Weight { get; private set; } = Microsoft.UI.Text.FontWeights.Normal;

	public wut.FontStyle Style { get; private set; } = wut.FontStyle.Normal;

	public wut.FontStretch Stretch { get; private set; } = wut.FontStretch.Normal;

	public FontStyle FontStyle { get; private set; }

	public FontDecoration FontDecoration { get; private set; }

	public FontFamily Family => _family ??= new FontFamily(new FontFamilyHandler(DefaultFamilyName, DefaultFamilyName));

	FontFamilyHandler FamilyHandler => (FontFamilyHandler)Family.Handler;

	public string FamilyName => Family.Name;

	/// <summary>Gets the family name to use for XAML or Win2D.</summary>
	public string NativeFamilyName => FamilyHandler.NativeName;

	public FontTypeface Typeface => _typeface ??= new FontTypeface(Family, new FontTypefaceHandler(Face ?? throw new InvalidOperationException($"Font '{FamilyName}' was not found")));

	public void Create(FontFamily family, float size, FontStyle style, FontDecoration decoration)
	{
		_family = family;
		Size = size;
		FontStyle = style;
		FontDecoration = decoration;
		Weight = style.HasFlag(FontStyle.Bold) ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal;
		Style = style.HasFlag(FontStyle.Italic) ? wut.FontStyle.Italic : wut.FontStyle.Normal;
	}

	public void Create(SystemFont systemFont, float? size, FontDecoration decoration)
	{
		// sizes match the WinUI control theme resources (14px body, 12px captions)
		var pixelSize = systemFont switch
		{
			SystemFont.ToolTip or SystemFont.StatusBar or SystemFont.Palette => 12,
			_ => 14
		};
		var style = systemFont == SystemFont.Bold ? FontStyle.Bold : FontStyle.None;
		Create(new FontFamily(new FontFamilyHandler(DefaultFamilyName, DefaultFamilyName)), size ?? PixelsToPoints(pixelSize), style, decoration);
	}

	public void Create(FontTypeface typeface, float size, FontDecoration decoration)
	{
		_typeface = typeface;
		_family = typeface.Family;
		Size = size;
		FontDecoration = decoration;
		if (typeface.Handler is FontTypefaceHandler typefaceHandler)
		{
			_face = typefaceHandler.Face;
			Weight = _face.Weight;
			Style = _face.Style;
			Stretch = _face.Stretch;
		}
		FontStyle = typeface.FontStyle;
	}

	CanvasFontFace? Face
	{
		get
		{
			if (_face != null)
				return _face;
			var set = FamilyHandler.FontSet ?? FontFamilyHandler.SystemFontSet;
			using var matches = set.GetMatchingFonts(FamilyHandler.FamilyName, Weight, Stretch, Style);
			if (matches.Fonts.Count > 0)
				_face = matches.Fonts[0];
			else
			{
				// fall back to the default family so metrics are still meaningful
				using var fallback = FontFamilyHandler.SystemFontSet.GetMatchingFonts(DefaultFamilyName, Weight, Stretch, Style);
				_face = fallback.Fonts.Count > 0 ? fallback.Fonts[0] : null;
			}
			return _face;
		}
	}

	public float XHeight => Size * (Face?.LowercaseLetterHeight ?? 0.5f);

	public float Ascent => Size * (Face?.Ascent ?? 0.9f);

	public float Descent => Size * (Face?.Descent ?? 0.2f);

	public float LineHeight => Ascent + Descent + Leading;

	public float Leading => Size * (Face?.LineGap ?? 0f);

	public float Baseline => Ascent;

	public float UnderlinePosition => Size * -(Face?.UnderlinePosition ?? -0.1f);

	public float UnderlineThickness => Size * (Face?.UnderlineThickness ?? 0.05f);

	public SizeF MeasureString(string text)
	{
		if (string.IsNullOrEmpty(text))
			return SizeF.Empty;
		using var layout = CreateTextLayout(CanvasDevice.GetSharedDevice(), text, 0, 0);
		var bounds = layout.LayoutBoundsIncludingTrailingWhitespace;
		return new SizeF((float)bounds.Width, (float)bounds.Height);
	}

	/// <summary>Creates a Win2D text format for this font, which the caller must dispose.</summary>
	public CanvasTextFormat CreateTextFormat()
	{
		return new CanvasTextFormat
		{
			FontFamily = NativeFamilyName,
			FontSize = PixelSize,
			FontWeight = Weight,
			FontStyle = Style,
			FontStretch = Stretch,
			WordWrapping = CanvasWordWrapping.NoWrap,
		};
	}

	/// <summary>Creates a Win2D text layout for this font, including its decorations.</summary>
	public CanvasTextLayout CreateTextLayout(ICanvasResourceCreator creator, string text, float width, float height)
	{
		using var format = CreateTextFormat();
		var layout = new CanvasTextLayout(creator, text, format, width, height);
		if (FontDecoration.HasFlag(FontDecoration.Underline))
			layout.SetUnderline(0, text.Length, true);
		if (FontDecoration.HasFlag(FontDecoration.Strikethrough))
			layout.SetStrikethrough(0, text.Length, true);
		return layout;
	}

	/// <summary>Gets the font currently used by the specified control.</summary>
	public static Font GetFont(muc.Control control)
	{
		return new Font(new FontHandler(control.FontFamily?.Source, control.FontSize, control.FontWeight, control.FontStyle, control.FontStretch));
	}

	/// <summary>Gets the font currently used by the specified text block.</summary>
	public static Font GetFont(muc.TextBlock control)
	{
		var decoration = FontDecoration.None;
		if (control.TextDecorations.HasFlag(wut.TextDecorations.Underline))
			decoration |= FontDecoration.Underline;
		if (control.TextDecorations.HasFlag(wut.TextDecorations.Strikethrough))
			decoration |= FontDecoration.Strikethrough;
		return new Font(new FontHandler(control.FontFamily?.Source, control.FontSize, control.FontWeight, control.FontStyle, control.FontStretch, decoration));
	}

	/// <summary>Applies the font to the control, or restores the default font when null.</summary>
	public static void Apply(muc.Control control, Font? font)
	{
		if (font?.Handler is not FontHandler handler)
		{
			control.ClearValue(muc.Control.FontFamilyProperty);
			control.ClearValue(muc.Control.FontSizeProperty);
			control.ClearValue(muc.Control.FontWeightProperty);
			control.ClearValue(muc.Control.FontStyleProperty);
			control.ClearValue(muc.Control.FontStretchProperty);
			return;
		}
		control.FontFamily = new muxm.FontFamily(handler.NativeFamilyName);
		control.FontSize = handler.PixelSize;
		control.FontWeight = handler.Weight;
		control.FontStyle = handler.Style;
		control.FontStretch = handler.Stretch;
	}

	/// <summary>Applies the font to the text block, or restores the default font when null.</summary>
	public static void Apply(muc.TextBlock control, Font? font)
	{
		if (font?.Handler is not FontHandler handler)
		{
			control.ClearValue(muc.TextBlock.FontFamilyProperty);
			control.ClearValue(muc.TextBlock.FontSizeProperty);
			control.ClearValue(muc.TextBlock.FontWeightProperty);
			control.ClearValue(muc.TextBlock.FontStyleProperty);
			control.ClearValue(muc.TextBlock.FontStretchProperty);
			control.ClearValue(muc.TextBlock.TextDecorationsProperty);
			return;
		}
		control.FontFamily = new muxm.FontFamily(handler.NativeFamilyName);
		control.FontSize = handler.PixelSize;
		control.FontWeight = handler.Weight;
		control.FontStyle = handler.Style;
		control.FontStretch = handler.Stretch;
		var decorations = wut.TextDecorations.None;
		if (handler.FontDecoration.HasFlag(FontDecoration.Underline))
			decorations |= wut.TextDecorations.Underline;
		if (handler.FontDecoration.HasFlag(FontDecoration.Strikethrough))
			decorations |= wut.TextDecorations.Strikethrough;
		control.TextDecorations = decorations;
	}
}
