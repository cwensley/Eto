using Windows.Graphics.Printing;

namespace Eto.WinUI.Forms.Printing;

public class PrintSettingsHandler : WidgetHandler<object, PrintSettings>, PrintSettings.IHandler
{
	public int Copies { get; set; } = 1;

	public Range<int> MaximumPageRange { get; set; } = new Range<int>(1, 1);

	public Range<int> SelectedPageRange { get; set; } = new Range<int>(1, 1);

	public PrintSelection PrintSelection { get; set; } = PrintSelection.AllPages;

	public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

	public bool Collate { get; set; } = true;

	public bool Reverse { get; set; }

	internal void ApplyTo(PrintTaskOptions options, bool allowPageRange)
	{
		options.NumberOfCopies = (uint)Math.Max(1, Copies);
		options.Orientation = Orientation == PageOrientation.Landscape ? PrintOrientation.Landscape : PrintOrientation.Portrait;
		options.Collation = Collate ? PrintCollation.Collated : PrintCollation.Uncollated;

		options.PageRangeOptions.AllowAllPages = true;
		options.PageRangeOptions.AllowCurrentPage = false;
		options.PageRangeOptions.AllowCustomSetOfPages = allowPageRange;
		if (allowPageRange && !options.DisplayedOptions.Contains(StandardPrintTaskOptions.CustomPageRanges))
			options.DisplayedOptions.Add(StandardPrintTaskOptions.CustomPageRanges);
	}

	internal void ReadFrom(PrintTaskOptions options)
	{
		Copies = (int)options.NumberOfCopies;
		Orientation = options.Orientation == PrintOrientation.Landscape || options.Orientation == PrintOrientation.LandscapeFlipped ? PageOrientation.Landscape : PageOrientation.Portrait;
		Collate = options.Collation != PrintCollation.Uncollated;
		var ranges = options.CustomPageRanges;
		if (ranges.Count > 0)
		{
			PrintSelection = PrintSelection.SelectedPages;
			SelectedPageRange = new Range<int>(ranges.Min(r => r.FirstPageNumber), ranges.Max(r => r.LastPageNumber));
		}
		else
			PrintSelection = PrintSelection.AllPages;
	}
}
