namespace Eto.WinUI.Forms;

public class ScreensHandler : Screen.IScreensHandler
{
	public void Initialize()
	{
	}

	public Widget Widget { get; set; }

	public Eto.Platform Platform { get; set; }

	public IEnumerable<Screen> Screens
	{
		get
		{
			foreach (var screen in ScreenHandler.GetScreens())
				yield return new Screen(new ScreenHandler(screen));
		}
	}

	public Screen PrimaryScreen
	{
		get
		{
			var screen = ScreenHandler.GetScreens().FirstOrDefault(r => r.IsPrimary) ?? ScreenHandler.GetScreens().First();
			return new Screen(new ScreenHandler(screen));
		}
	}
}

