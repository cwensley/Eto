namespace Eto.WinUI.Drawing;

public class SystemColorsHandler : SystemColors.IHandler
{
	static Color? GetResourceColor(string key)
	{
		var app = mux.Application.Current;
		if (app == null)
			return null;
		if (app.Resources.TryGetValue(key, out var resource))
		{
			if (resource is muxm.SolidColorBrush brush)
				return brush.Color.ToEto();
			if (resource is wu.Color color)
				return color.ToEto();
		}
		return null;
	}

	public static muxm.Brush? GetBrush(string key)
	{
		var app = mux.Application.Current;
		if (app == null)
			return null;
		if (app.Resources.TryGetValue(key, out var resource))
		{
			if (resource is muxm.Brush brush)
				return brush;
		}
		return null;
	}

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
