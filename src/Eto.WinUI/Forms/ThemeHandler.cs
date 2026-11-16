using Windows.UI.ViewManagement;

namespace Eto.WinUI.Forms;

public class ThemeHandler : WidgetHandler<mux.ElementTheme, Theme>, Theme.IHandler
{
	public ThemeHandler(mux.ElementTheme theme)
	{
		Control = theme;
	}

	public string Name => Control switch
	{
		mux.ElementTheme.Light => "Light",
		mux.ElementTheme.Dark => "Dark",
		_ => "System"
	};

	public ThemeStyle ThemeStyle => Control switch
	{
		mux.ElementTheme.Light => ThemeStyle.Light,
		mux.ElementTheme.Dark => ThemeStyle.Dark,
		_ => SystemThemeStyle
	};

	/// <summary>
	/// Gets the light/dark setting for apps currently chosen in Windows.
	/// </summary>
	public static ThemeStyle SystemThemeStyle
	{
		get
		{
			// the system background is black for dark mode and white for light mode
			var background = new UISettings().GetColorValue(UIColorType.Background);
			return background.R + background.G + background.B < 384 ? ThemeStyle.Dark : ThemeStyle.Light;
		}
	}

	public static mux.ElementTheme GetElementTheme(Theme? theme) => (theme?.Handler as ThemeHandler)?.Control ?? mux.ElementTheme.Default;
}

public class ThemesHandler : Themes.IHandler
{
	Theme? _system, _light, _dark;

	public Theme System => _system ??= new Theme(new ThemeHandler(mux.ElementTheme.Default));
	public Theme Light => _light ??= new Theme(new ThemeHandler(mux.ElementTheme.Light));
	public Theme Dark => _dark ??= new Theme(new ThemeHandler(mux.ElementTheme.Dark));

	public IEnumerable<Theme> GetThemes()
	{
		yield return System;
		yield return Light;
		yield return Dark;
	}
}
