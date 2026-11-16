using Microsoft.Graphics.Canvas.Text;

namespace Eto.WinUI.Drawing;

public class FontsHandler : WidgetHandler<Widget>, Fonts.IHandler
{
	HashSet<string>? _availableFontFamilies;

	static string[] GetSystemFamilies() => CanvasTextFormat.GetSystemFontFamilies(["en-us"]);

	public IEnumerable<FontFamily> AvailableFontFamilies => GetSystemFamilies()
		.Select(r => new FontFamily(new FontFamilyHandler(r, r)));

	public bool FontFamilyAvailable(string fontFamily)
	{
		_availableFontFamilies ??= new HashSet<string>(GetSystemFamilies(), StringComparer.OrdinalIgnoreCase);
		return _availableFontFamilies.Contains(fontFamily);
	}
}
