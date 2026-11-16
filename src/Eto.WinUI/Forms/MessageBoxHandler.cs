using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

public class MessageBoxHandler : WidgetHandler<object, Widget>, MessageBox.IHandler
{
	public string Text { get; set; } = string.Empty;
	public string Caption { get; set; } = string.Empty;
	public MessageBoxType Type { get; set; }
	public MessageBoxButtons Buttons { get; set; }
	public MessageBoxDefaultButton DefaultButton { get; set; }

	public DialogResult ShowDialog(Control parent)
	{
		var parentWindow = parent?.ParentWindow;
		var owner = parentWindow?.NativeHandle ?? IntPtr.Zero;
		var caption = Caption ?? parentWindow?.Title ?? string.Empty;
		// with no parent it should block all windows, which task-modal does
		var flags = GetButtons() | GetIcon() | GetDefaultButton() | (owner == IntPtr.Zero ? MB_TASKMODAL : 0);
		return MessageBoxW(owner, Text ?? string.Empty, caption, flags) switch
		{
			IDOK => DialogResult.Ok,
			IDCANCEL => DialogResult.Cancel,
			IDYES => DialogResult.Yes,
			IDNO => DialogResult.No,
			_ => DialogResult.None
		};
	}

	uint GetButtons() => Buttons switch
	{
		MessageBoxButtons.OKCancel => MB_OKCANCEL,
		MessageBoxButtons.YesNo => MB_YESNO,
		MessageBoxButtons.YesNoCancel => MB_YESNOCANCEL,
		_ => MB_OK
	};

	uint GetIcon() => Type switch
	{
		MessageBoxType.Error => MB_ICONERROR,
		MessageBoxType.Warning => MB_ICONWARNING,
		MessageBoxType.Question => MB_ICONQUESTION,
		_ => MB_ICONINFORMATION
	};

	uint GetDefaultButton() => DefaultButton switch
	{
		MessageBoxDefaultButton.OK or MessageBoxDefaultButton.Yes => MB_DEFBUTTON1,
		MessageBoxDefaultButton.No => MB_DEFBUTTON2,
		MessageBoxDefaultButton.Cancel => Buttons == MessageBoxButtons.YesNoCancel ? MB_DEFBUTTON3 : MB_DEFBUTTON2,
		// Cancel or No when shown, as on other platforms
		_ => Buttons switch
		{
			MessageBoxButtons.YesNo or MessageBoxButtons.OKCancel => MB_DEFBUTTON2,
			MessageBoxButtons.YesNoCancel => MB_DEFBUTTON3,
			_ => MB_DEFBUTTON1
		}
	};

	const uint MB_OK = 0x0;
	const uint MB_OKCANCEL = 0x1;
	const uint MB_YESNOCANCEL = 0x3;
	const uint MB_YESNO = 0x4;
	const uint MB_ICONERROR = 0x10;
	const uint MB_ICONQUESTION = 0x20;
	const uint MB_ICONWARNING = 0x30;
	const uint MB_ICONINFORMATION = 0x40;
	const uint MB_DEFBUTTON1 = 0x0;
	const uint MB_DEFBUTTON2 = 0x100;
	const uint MB_DEFBUTTON3 = 0x200;
	const uint MB_TASKMODAL = 0x2000;
	const int IDOK = 1;
	const int IDCANCEL = 2;
	const int IDYES = 6;
	const int IDNO = 7;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
