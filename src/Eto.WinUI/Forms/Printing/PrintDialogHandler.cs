using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms.Printing;

/// <summary>
/// Prints the document through the Windows print UI, or without a document picks settings with the classic print dialog.
/// </summary>
public class PrintDialogHandler : WidgetHandler<object, PrintDialog>, PrintDialog.IHandler
{
	PrintSettings? _printSettings;

	public PrintDocument? Document { get; set; }

	public PrintSettings PrintSettings
	{
		get => _printSettings ??= new PrintSettings();
		set => _printSettings = value;
	}

	public bool AllowPageRange { get; set; } = true;

	// the Windows print UI has no selection option
	public bool AllowSelection { get; set; }

	public DialogResult ShowDialog(Window parent)
	{
		if (Document?.Handler is PrintDocumentHandler document)
			return document.ShowPrintUI(parent, PrintSettings, AllowPageRange) ? DialogResult.Ok : DialogResult.Cancel;

		// The modern print UI needs a document to preview, so use the classic dialog to just choose settings.
		return ShowSettingsDialog(parent) ? DialogResult.Ok : DialogResult.Cancel;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct PRINTDLG
	{
		public int lStructSize;
		public IntPtr hwndOwner;
		public IntPtr hDevMode;
		public IntPtr hDevNames;
		public IntPtr hDC;
		public int Flags;
		public ushort nFromPage;
		public ushort nToPage;
		public ushort nMinPage;
		public ushort nMaxPage;
		public ushort nCopies;
		public IntPtr hInstance;
		public IntPtr lCustData;
		public IntPtr lpfnPrintHook;
		public IntPtr lpfnSetupHook;
		public IntPtr lpPrintTemplateName;
		public IntPtr lpSetupTemplateName;
		public IntPtr hPrintTemplate;
		public IntPtr hSetupTemplate;
	}

	[DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "PrintDlgW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool PrintDlg(ref PRINTDLG lppd);

	[DllImport("kernel32.dll")]
	static extern IntPtr GlobalLock(IntPtr hMem);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GlobalUnlock(IntPtr hMem);

	[DllImport("kernel32.dll")]
	static extern IntPtr GlobalFree(IntPtr hMem);

	const int PD_SELECTION = 0x1;
	const int PD_PAGENUMS = 0x2;
	const int PD_NOSELECTION = 0x4;
	const int PD_NOPAGENUMS = 0x8;
	const int PD_COLLATE = 0x10;
	const int PD_USEDEVMODECOPIESANDCOLLATE = 0x40000;

	// DEVMODEW field offsets
	const int DM_ORIENTATION = 76;
	const int DM_COPIES = 86;
	const int DM_COLLATE = 100;
	const short DMORIENT_LANDSCAPE = 2;

	bool ShowSettingsDialog(Window? parent)
	{
		var range = PrintSettings.MaximumPageRange;
		var selected = PrintSettings.SelectedPageRange;
		var dlg = new PRINTDLG
		{
			lStructSize = Marshal.SizeOf<PRINTDLG>(),
			hwndOwner = parent?.NativeHandle ?? IntPtr.Zero,
			Flags = PD_USEDEVMODECOPIESANDCOLLATE
				| (AllowSelection ? 0 : PD_NOSELECTION)
				| (AllowPageRange ? 0 : PD_NOPAGENUMS)
				| (PrintSettings.PrintSelection == PrintSelection.SelectedPages && AllowPageRange ? PD_PAGENUMS : 0)
				| (PrintSettings.PrintSelection == PrintSelection.Selection && AllowSelection ? PD_SELECTION : 0),
			nMinPage = (ushort)Math.Max(1, range.Start),
			nMaxPage = (ushort)Math.Max(1, range.End),
			nFromPage = (ushort)Math.Max(1, selected.Start),
			nToPage = (ushort)Math.Max(1, selected.End),
			nCopies = (ushort)Math.Max(1, PrintSettings.Copies)
		};
		try
		{
			if (!PrintDlg(ref dlg))
				return false;

			if ((dlg.Flags & PD_PAGENUMS) != 0)
			{
				PrintSettings.PrintSelection = PrintSelection.SelectedPages;
				PrintSettings.SelectedPageRange = new Range<int>(dlg.nFromPage, dlg.nToPage);
			}
			else if ((dlg.Flags & PD_SELECTION) != 0)
				PrintSettings.PrintSelection = PrintSelection.Selection;
			else
				PrintSettings.PrintSelection = PrintSelection.AllPages;

			var devMode = dlg.hDevMode != IntPtr.Zero ? GlobalLock(dlg.hDevMode) : IntPtr.Zero;
			if (devMode != IntPtr.Zero)
			{
				try
				{
					PrintSettings.Orientation = Marshal.ReadInt16(devMode, DM_ORIENTATION) == DMORIENT_LANDSCAPE ? PageOrientation.Landscape : PageOrientation.Portrait;
					PrintSettings.Copies = Math.Max(1, (int)Marshal.ReadInt16(devMode, DM_COPIES));
					PrintSettings.Collate = Marshal.ReadInt16(devMode, DM_COLLATE) != 0;
				}
				finally
				{
					GlobalUnlock(dlg.hDevMode);
				}
			}
			else
			{
				PrintSettings.Copies = dlg.nCopies;
				PrintSettings.Collate = (dlg.Flags & PD_COLLATE) != 0;
			}
			return true;
		}
		finally
		{
			if (dlg.hDevMode != IntPtr.Zero)
				GlobalFree(dlg.hDevMode);
			if (dlg.hDevNames != IntPtr.Zero)
				GlobalFree(dlg.hDevNames);
		}
	}
}
