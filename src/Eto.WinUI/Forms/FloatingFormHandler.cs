namespace Eto.WinUI.Forms;

public class FloatingFormHandler : FormHandler, FloatingForm.IHandler
{
	protected override void Initialize()
	{
		base.Initialize();
		// defaults for a floating form
		Maximizable = false;
		Minimizable = false;
		ShowInTaskbar = false;
		Topmost = true;
	}
}
