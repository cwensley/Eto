using System.Globalization;
using Microsoft.Graphics.Canvas.Text;

namespace Eto.WinUI.Drawing;

public class FontTypefaceHandler : WidgetHandler<FontTypeface>, FontTypeface.IHandler
{
	CanvasFontFace? _face;

	public FontTypefaceHandler()
	{
	}

	public FontTypefaceHandler(CanvasFontFace face)
	{
		_face = face;
	}

	/// <summary>Gets the Win2D font face this typeface describes.</summary>
	public CanvasFontFace Face => _face ?? throw new InvalidOperationException("Typeface has not been created");

	public string Name => FontFamilyHandler.GetEnglish(Face.FaceNames) ?? string.Empty;

	public string LocalizedName => FontFamilyHandler.GetLocalized(Face.FaceNames) ?? Name;

	public string PostScriptName
	{
		get
		{
			var names = Face.GetInformationalStrings(CanvasFontInformation.PostscriptName);
			return FontFamilyHandler.GetEnglish(names) ?? string.Empty;
		}
	}

	public FontStyle FontStyle => GetStyle(Face.Weight, Face.Style);

	internal static FontStyle GetStyle(Windows.UI.Text.FontWeight weight, Windows.UI.Text.FontStyle style)
	{
		var result = FontStyle.None;
		if (weight.Weight >= Microsoft.UI.Text.FontWeights.SemiBold.Weight)
			result |= FontStyle.Bold;
		if (style != Windows.UI.Text.FontStyle.Normal)
			result |= FontStyle.Italic;
		return result;
	}

	public bool IsSymbol => Face.IsSymbolFont;

	public FontFamily Family { get; private set; } = null!;

	public bool HasCharacterRanges(IEnumerable<Range<int>> ranges)
	{
		foreach (var range in ranges)
		{
			for (int i = range.Start; i <= range.End; i++)
			{
				if (!Face.HasCharacter((uint)i))
					return false;
			}
		}
		return true;
	}

	public void Create(FontFamily family)
	{
		Family = family;
	}

	public void Create(Stream stream)
	{
		Create(SaveToTempFile(stream));
	}

	public void Create(string fileName)
	{
		var familyHandler = new FontFamilyHandler();
		var family = new FontFamily(familyHandler);
		familyHandler.CreateFromFiles([fileName]);
		_face = familyHandler.GetFaces().FirstOrDefault()
			?? throw new ArgumentException("Could not load font from file", nameof(fileName));
		Family = family;
	}

	// Win2D and XAML can only load custom fonts from a file
	internal static string SaveToTempFile(Stream stream)
	{
		var directory = Path.Combine(Path.GetTempPath(), "Eto.WinUI.Fonts");
		Directory.CreateDirectory(directory);
		var fileName = Path.Combine(directory, Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".otf");
		using var file = File.Create(fileName);
		stream.CopyTo(file);
		return fileName;
	}
}
