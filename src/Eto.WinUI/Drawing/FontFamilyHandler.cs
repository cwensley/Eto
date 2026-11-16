using System.Globalization;
using Microsoft.Graphics.Canvas.Text;

namespace Eto.WinUI.Drawing;

public class FontFamilyHandler : WidgetHandler<FontFamily>, FontFamily.IHandler
{
	static CanvasFontSet? _systemFontSet;
	FontTypeface[]? _typefaces;
	CanvasFontFace[]? _faces;

	internal static CanvasFontSet SystemFontSet => _systemFontSet ??= CanvasFontSet.GetSystemFontSet();

	public FontFamilyHandler()
	{
	}

	internal FontFamilyHandler(string name, string nativeName, CanvasFontSet? fontSet = null)
	{
		Name = name;
		NativeName = nativeName;
		FontSet = fontSet;
	}

	/// <summary>Gets the Eto name of the family, which may be a generic name like "sans".</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>Gets the name to use for XAML or Win2D, including the font file for custom fonts.</summary>
	public string NativeName { get; private set; } = string.Empty;

	/// <summary>Gets the actual family name without any font file location.</summary>
	internal string FamilyName => NativeName.Contains('#') ? NativeName.Substring(NativeName.LastIndexOf('#') + 1) : NativeName;

	internal CanvasFontSet? FontSet { get; private set; }

	public string LocalizedName
	{
		get
		{
			var names = GetFaces().FirstOrDefault()?.FamilyNames;
			if (names != null && names.Count > 0)
				return GetLocalized(names) ?? Name;
			return Name;
		}
	}

	internal static string? GetLocalized(IReadOnlyDictionary<string, string> names)
	{
		var culture = CultureInfo.CurrentUICulture.Name;
		if (names.TryGetValue(culture, out var name))
			return name;
		return names.FirstOrDefault(r => string.Equals(r.Key, culture, StringComparison.OrdinalIgnoreCase)).Value
			?? GetEnglish(names);
	}

	internal static string? GetEnglish(IReadOnlyDictionary<string, string> names)
	{
		return names.FirstOrDefault(r => string.Equals(r.Key, "en-us", StringComparison.OrdinalIgnoreCase)).Value
			?? names.Values.FirstOrDefault();
	}

	public IEnumerable<FontTypeface> Typefaces => _typefaces ??= GetFaces()
		.Select(r => new FontTypeface(Widget, new FontTypefaceHandler(r)))
		.ToArray();

	internal CanvasFontFace[] GetFaces()
	{
		if (_faces != null)
			return _faces;
		var set = FontSet ?? SystemFontSet;
		var familyName = FamilyName;
		_faces = set.Fonts
			.Where(r => r.Simulations == CanvasFontSimulations.None && r.FamilyNames.Values.Any(n => string.Equals(n, familyName, StringComparison.OrdinalIgnoreCase)))
			.ToArray();
		return _faces;
	}

	public void Create(string familyName)
	{
		Name = familyName;
		NativeName = GetNativeName(familyName);
	}

	internal static string GetNativeName(string familyName) => familyName.ToUpperInvariant() switch
	{
		FontFamilies.MonospaceFamilyName => "Consolas",
		FontFamilies.SansFamilyName => "Segoe UI",
		FontFamilies.SerifFamilyName => "Times New Roman",
		FontFamilies.CursiveFamilyName => "Comic Sans MS",
		FontFamilies.FantasyFamilyName => "Impact",
		_ => familyName
	};

	public void CreateFromFiles(IEnumerable<string> fileNames)
	{
		var files = fileNames.ToList();
		var faces = new List<CanvasFontFace>();
		CanvasFontSet? firstSet = null;
		foreach (var fileName in files)
		{
			CanvasFontSet set;
			try
			{
				set = new CanvasFontSet(new Uri(Path.GetFullPath(fileName)));
			}
			catch (System.Runtime.InteropServices.COMException ex)
			{
				// Win2D resolves font URIs through StorageFile, which rejects plain file paths in unpackaged apps
				throw new NotSupportedException($"Could not load font file '{fileName}'. Win2D can only load fonts from app package (ms-appx) or app data (ms-appdata) locations.", ex);
			}
			firstSet ??= set;
			faces.AddRange(set.Fonts);
		}
		if (faces.Count == 0)
			throw new ArgumentException("Could not load any fonts from the specified files", nameof(fileNames));

		var familyName = GetEnglish(faces[0].FamilyNames) ?? string.Empty;
		if (faces.Any(r => !r.FamilyNames.Values.Contains(familyName)))
			throw new ArgumentException("Fonts must all be in the same family", nameof(fileNames));

		Name = familyName;
		// XAML and Win2D find a family inside a font file using "<uri>#<family>"
		NativeName = $"{new Uri(Path.GetFullPath(files[0])).AbsoluteUri}#{familyName}";
		FontSet = firstSet;
		_faces = faces.ToArray();
	}

	public void CreateFromStreams(IEnumerable<Stream> streams)
	{
		CreateFromFiles(streams.Select(FontTypefaceHandler.SaveToTempFile).ToList());
	}
}
