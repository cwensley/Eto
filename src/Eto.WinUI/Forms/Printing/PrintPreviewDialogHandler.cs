namespace Eto.WinUI.Forms.Printing;

/// <summary>
/// Shows the Windows print UI, which previews the document and can also print it.
/// </summary>
public class PrintPreviewDialogHandler : WidgetHandler<object, PrintPreviewDialog>, PrintPreviewDialog.IHandler
{
	PrintSettings? _printSettings;

	public PrintDocument? Document { get; set; }

	public PrintSettings PrintSettings
	{
		get => _printSettings ??= Document?.PrintSettings ?? new PrintSettings();
		set => _printSettings = value;
	}

	public DialogResult ShowDialog(Window parent)
	{
		if (Document?.Handler is PrintDocumentHandler document)
			document.ShowPrintUI(parent, PrintSettings, true);
		return DialogResult.Ok;
	}
}
