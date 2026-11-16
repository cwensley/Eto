using Eto.WinUI.Forms;

namespace Eto.WinUI.Drawing;

public class SystemColorsHandler : SystemColors.IHandler
{
	// Application resources only reflect the theme the app started with, so look in the theme dictionaries
	// for the theme Eto is currently using
	static object? FindThemeResource(mux.ResourceDictionary dictionary, string key, string[] themeKeys)
	{
		foreach (var themeKey in themeKeys)
		{
			if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var theme)
				&& theme is mux.ResourceDictionary themeDictionary
				&& themeDictionary.TryGetValue(key, out var value))
				return value;
		}
		for (int i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
		{
			var value = FindThemeResource(dictionary.MergedDictionaries[i], key, themeKeys);
			if (value != null)
				return value;
		}
		return null;
	}

	static readonly string[] s_darkThemeKeys = ["Dark", "Default"];
	static readonly string[] s_lightThemeKeys = ["Light"];

	static object? GetResource(string key)
	{
		var app = mux.Application.Current;
		if (app == null)
			return null;
		var themeKeys = ApplicationHandler.CurrentThemeStyle == ThemeStyle.Dark ? s_darkThemeKeys : s_lightThemeKeys;
		return FindThemeResource(app.Resources, key, themeKeys) ?? (app.Resources.TryGetValue(key, out var resource) ? resource : null);
	}

	static Color? GetResourceColor(string key)
	{
		// a brush in an inactive theme dictionary still resolves its color for the active theme, so prefer the color itself
		if (key.EndsWith("Brush", StringComparison.Ordinal) && GetResource(key.Substring(0, key.Length - 5)) is wu.Color themeColor)
			return themeColor.ToEto();

		var resource = GetResource(key);
		if (resource != null)
		{
			if (resource is muxm.SolidColorBrush brush)
				return brush.Color.ToEto();
			if (resource is wu.Color color)
				return color.ToEto();
		}
		return null;
	}

	public static muxm.Brush? GetBrush(string key) => GetResource(key) as muxm.Brush;

	public static muxm.Brush? SystemControlBackgroundAccentBrush => GetBrush("SystemControlBackgroundAccentBrush");

	public static muxm.Brush? ControlFillColorDefaultBrush => GetBrush("ControlFillColorDefaultBrush");


	public Color ControlBackground => GetResourceColor("SolidBackgroundFillColorBaseBrush") ?? Colors.White;

	public Color Control => GetResourceColor("ControlFillColorDefaultBrush") ?? Colors.White;

	public Color ControlText => GetResourceColor("TextFillColorPrimaryBrush") ?? Colors.Black;

	public Color HighlightText => GetResourceColor("TextOnAccentFillColorPrimaryBrush") ?? Colors.White;

	public Color Highlight => GetResourceColor("AccentFillColorDefaultBrush") ?? Colors.Blue;

	public Color WindowBackground => GetResourceColor("SolidBackgroundFillColorBaseBrush") ?? Colors.White;

	public Color DisabledText => GetResourceColor("TextFillColorDisabledBrush") ?? Colors.Gray;

	public Color SelectionText => GetResourceColor("TextOnAccentFillColorSelectedTextBrush") ?? Colors.White;

	public Color Selection => GetResourceColor("AccentFillColorSelectedTextBackgroundBrush") ?? Colors.Blue;

	public Color LinkText => GetResourceColor("HyperlinkForeground") ?? Colors.Blue;
}
