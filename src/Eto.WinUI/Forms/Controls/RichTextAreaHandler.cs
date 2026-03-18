using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.WinUI;
using mut = Microsoft.UI.Text;
using Windows.Storage.Streams;

namespace Eto.WinUI.Forms.Controls;

public class EtoRichEditBox : muc.RichEditBox
{
	public IWinUIFrameworkElement? Handler { get; set; }

	protected override wf.Size MeasureOverride(wf.Size availableSize)
	{
		return Handler?.MeasureOverride(availableSize, base.MeasureOverride) ?? base.MeasureOverride(availableSize);
	}
}

public class RichTextAreaHandler : WinUIControl<muc.RichEditBox, RichTextArea, RichTextArea.ICallback>, RichTextArea.IHandler, ITextBuffer
{
	int _suppressSelectionChanged;
	int _suppressTextChanged;
	int? _lastCaretIndex;
	Range<int> _lastSelection;
	bool _acceptsTab = true;
	bool _acceptsReturn = true;
	TextAlignment _textAlignment;

	protected override sw.Size DefaultSize => new sw.Size(100, 60);

	protected override bool PreventUserResize => true;

	protected override muc.RichEditBox CreateControl() => new EtoRichEditBox { Handler = this };

	protected override void Initialize()
	{
		base.Initialize();
		Control.IsSpellCheckEnabled = false;
		Control.TextWrapping = mux.TextWrapping.Wrap;
		UserPreferredSize = DefaultSize;
		_lastSelection = Selection;
		_lastCaretIndex = CaretIndex;
	}

	mut.RichEditTextDocument Document => Control.Document;

	mut.ITextRange FullRange => Document.GetRange(0, TextLength);

	mut.ITextRange GetRange(Range<int> range)
	{
		var start = Math.Max(0, range.Start);
		var end = Math.Max(start, start + range.Length());
		return Document.GetRange(start, end);
	}

	string GetDocumentText()
	{
		Document.GetText(mut.TextGetOptions.NoHidden, out var text);
		return NormalizeText(text);
	}

	static string NormalizeText(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		return text.Replace("\r\n", "\n").TrimEnd('\r');
	}

	static mut.FormatEffect ToFormatEffect(bool value) => value ? mut.FormatEffect.On : mut.FormatEffect.Off;

	static bool IsOn(mut.FormatEffect effect) => effect == mut.FormatEffect.On;

	static mut.UnderlineType ToUnderline(bool value) => value ? mut.UnderlineType.Single : mut.UnderlineType.None;

	static bool IsUnderline(mut.UnderlineType underline) => underline != mut.UnderlineType.None;

	static mut.ParagraphAlignment ToParagraphAlignment(TextAlignment value) => value switch
	{
		TextAlignment.Center => mut.ParagraphAlignment.Center,
		TextAlignment.Right => mut.ParagraphAlignment.Right,
		_ => mut.ParagraphAlignment.Left
	};

	static TextAlignment ToEtoAlignment(mut.ParagraphAlignment value) => value switch
	{
		mut.ParagraphAlignment.Center => TextAlignment.Center,
		mut.ParagraphAlignment.Right => TextAlignment.Right,
		_ => TextAlignment.Left
	};

	void SetSelectionFormat(Action<mut.ITextCharacterFormat> apply)
	{
		apply(Document.Selection.CharacterFormat);
	}

	void SetRangeFormat(Range<int> range, Action<mut.ITextCharacterFormat> apply)
	{
		apply(GetRange(range).CharacterFormat);
	}

	public bool ReadOnly
	{
		get => Control.IsReadOnly;
		set => Control.IsReadOnly = value;
	}

	public string Text
	{
		get => GetDocumentText();
		set
		{
			_suppressSelectionChanged++;
			_suppressTextChanged++;
			Document.SetText(mut.TextSetOptions.None, value ?? string.Empty);
			Document.Selection.SetRange(TextLength, TextLength);
			_suppressTextChanged--;
			_suppressSelectionChanged--;
			_lastSelection = Selection;
			_lastCaretIndex = CaretIndex;
		}
	}

	public bool Wrap
	{
		get => Control.TextWrapping != mux.TextWrapping.NoWrap;
		set => Control.TextWrapping = value ? mux.TextWrapping.Wrap : mux.TextWrapping.NoWrap;
	}

	public string SelectedText
	{
		get => NormalizeText(Document.Selection.Text);
		set => Document.Selection.Text = value ?? string.Empty;
	}

	public Range<int> Selection
	{
		get
		{
			var selection = Document.Selection;
			return Eto.Forms.Range.FromLength(selection.StartPosition, selection.EndPosition - selection.StartPosition);
		}
		set
		{
			var start = Math.Max(0, value.Start);
			var end = Math.Max(start, start + value.Length());
			Document.Selection.SetRange(start, end);
		}
	}

	public int CaretIndex
	{
		get => Document.Selection.StartPosition;
		set => Document.Selection.SetRange(value, value);
	}

	public bool AcceptsTab
	{
		get => _acceptsTab;
		set => _acceptsTab = value;
	}

	public bool AcceptsReturn
	{
		get => _acceptsReturn;
		set => _acceptsReturn = value;
	}

	public TextReplacements TextReplacements { get; set; }

	public TextReplacements SupportedTextReplacements => TextReplacements.None;

	public TextAlignment TextAlignment
	{
		get => ToEtoAlignment(Document.Selection.ParagraphFormat.Alignment);
		set
		{
			_textAlignment = value;
			FullRange.ParagraphFormat.Alignment = ToParagraphAlignment(value);
		}
	}

	public bool SpellCheck
	{
		get => Control.IsSpellCheckEnabled;
		set => Control.IsSpellCheckEnabled = value;
	}

	public bool SpellCheckIsSupported => true;

	public BorderType Border
	{
		get => Control.BorderThickness.ToEto().IsZero ? BorderType.None : BorderType.Bezel;
		set => Control.BorderThickness = value == BorderType.None ? new mux.Thickness(0) : new mux.Thickness(1);
	}

	public int TextLength => GetDocumentText().Length;

	public bool SelectionBold
	{
		get => IsOn(Document.Selection.CharacterFormat.Bold);
		set => SetSelectionFormat(format => format.Bold = ToFormatEffect(value));
	}

	public bool SelectionItalic
	{
		get => IsOn(Document.Selection.CharacterFormat.Italic);
		set => SetSelectionFormat(format => format.Italic = ToFormatEffect(value));
	}

	public bool SelectionUnderline
	{
		get => IsUnderline(Document.Selection.CharacterFormat.Underline);
		set => SetSelectionFormat(format => format.Underline = ToUnderline(value));
	}

	public bool SelectionStrikethrough
	{
		get => IsOn(Document.Selection.CharacterFormat.Strikethrough);
		set => SetSelectionFormat(format => format.Strikethrough = ToFormatEffect(value));
	}

	public Font SelectionFont
	{
		get
		{
			var format = Document.Selection.CharacterFormat;
			var style = FontStyle.None;
			if (IsOn(format.Bold))
				style |= FontStyle.Bold;
			if (IsOn(format.Italic))
				style |= FontStyle.Italic;
			var decoration = FontDecoration.None;
			if (IsUnderline(format.Underline))
				decoration |= FontDecoration.Underline;
			if (IsOn(format.Strikethrough))
				decoration |= FontDecoration.Strikethrough;
			return new Font(new SelectionFontHandler(format.Name, format.Size, style, decoration));
		}
		set
		{
			if (value == null)
				return;

			SetSelectionFormat(format =>
			{
				format.Name = value.FamilyName;
				format.Size = value.Size;
				format.Bold = ToFormatEffect(value.Bold);
				format.Italic = ToFormatEffect(value.Italic);
				format.Underline = ToUnderline(value.Underline);
				format.Strikethrough = ToFormatEffect(value.Strikethrough);
			});
		}
	}

	public Color SelectionForeground
	{
		get => Document.Selection.CharacterFormat.ForegroundColor.ToEto();
		set => SetSelectionFormat(format => format.ForegroundColor = value.ToWinUI());
	}

	public Color SelectionBackground
	{
		get => Document.Selection.CharacterFormat.BackgroundColor.ToEto();
		set => SetSelectionFormat(format => format.BackgroundColor = value.ToWinUI());
	}

	public FontFamily SelectionFamily
	{
		get => new FontFamily(new SelectionFontFamilyHandler(Document.Selection.CharacterFormat.Name));
		set
		{
			if (value == null)
				return;
			SetSelectionFormat(format => format.Name = value.Name);
		}
	}

	public FontTypeface SelectionTypeface
	{
		get
		{
			var format = Document.Selection.CharacterFormat;
			var family = new FontFamily(new SelectionFontFamilyHandler(format.Name));
			var style = FontStyle.None;
			if (IsOn(format.Bold))
				style |= FontStyle.Bold;
			if (IsOn(format.Italic))
				style |= FontStyle.Italic;
			return new FontTypeface(family, new SelectionFontTypefaceHandler(family, format.Name, style));
		}
		set
		{
			if (value == null)
				return;
			SetSelectionFormat(format =>
			{
				format.Name = value.Family?.Name ?? value.Name;
				format.Bold = ToFormatEffect(value.Bold);
				format.Italic = ToFormatEffect(value.Italic);
			});
		}
	}

	public ITextBuffer Buffer => this;

	public IEnumerable<RichTextAreaFormat> SupportedFormats
	{
		get
		{
			yield return RichTextAreaFormat.Rtf;
			yield return RichTextAreaFormat.PlainText;
		}
	}

	muc.ScrollViewer? ScrollViewer => Control.FindDescendant<muc.ScrollViewer>();

	public void Append(string text, bool scrollToCursor)
	{
		if (string.IsNullOrEmpty(text))
			return;
		Document.Selection.SetRange(TextLength, TextLength);
		Document.Selection.Text = text;
		if (scrollToCursor)
			ScrollToEnd();
	}

	public void SelectAll() => Document.Selection.Expand(mut.TextRangeUnit.Story);

	public void ScrollTo(Range<int> range)
	{
		GetRange(range).ScrollIntoView(mut.PointOptions.Start);
	}

	public void ScrollToStart()
	{
		ScrollViewer?.ChangeView(null, 0, null);
	}

	public void ScrollToEnd()
	{
		ScrollViewer?.ChangeView(null, ScrollViewer.ExtentHeight, null);
	}

	public void SetBold(Range<int> range, bool bold)
	{
		SetRangeFormat(range, format => format.Bold = ToFormatEffect(bold));
	}

	public void SetItalic(Range<int> range, bool italic)
	{
		SetRangeFormat(range, format => format.Italic = ToFormatEffect(italic));
	}

	public void SetUnderline(Range<int> range, bool underline)
	{
		SetRangeFormat(range, format => format.Underline = ToUnderline(underline));
	}

	public void SetStrikethrough(Range<int> range, bool strikethrough)
	{
		SetRangeFormat(range, format => format.Strikethrough = ToFormatEffect(strikethrough));
	}

	public void SetFont(Range<int> range, Font font)
	{
		if (font == null)
			return;
		SetRangeFormat(range, format =>
		{
			format.Name = font.FamilyName;
			format.Size = font.Size;
			format.Bold = ToFormatEffect(font.Bold);
			format.Italic = ToFormatEffect(font.Italic);
			format.Underline = ToUnderline(font.Underline);
			format.Strikethrough = ToFormatEffect(font.Strikethrough);
		});
	}

	public void SetForeground(Range<int> range, Color color)
	{
		SetRangeFormat(range, format => format.ForegroundColor = color.ToWinUI());
	}

	public void SetBackground(Range<int> range, Color color)
	{
		SetRangeFormat(range, format => format.BackgroundColor = color.ToWinUI());
	}

	public void SetFamily(Range<int> range, FontFamily family)
	{
		if (family == null)
			return;
		SetRangeFormat(range, format => format.Name = family.Name);
	}

	public void Load(Stream stream, RichTextAreaFormat format)
	{
		using var randomAccessStream = stream.AsRandomAccessStream();
		_suppressSelectionChanged++;
		_suppressTextChanged++;
		Document.LoadFromStream(format == RichTextAreaFormat.Rtf ? mut.TextSetOptions.FormatRtf : mut.TextSetOptions.None, randomAccessStream);
		Document.Selection.SetRange(TextLength, TextLength);
		_suppressTextChanged--;
		_suppressSelectionChanged--;
		_lastSelection = Selection;
		_lastCaretIndex = CaretIndex;
		Callback.OnTextChanged(Widget, EventArgs.Empty);
	}

	public void Save(Stream stream, RichTextAreaFormat format)
	{
		using var randomAccessStream = new InMemoryRandomAccessStream();
		Document.SaveToStream(format == RichTextAreaFormat.Rtf ? mut.TextGetOptions.FormatRtf : mut.TextGetOptions.None, randomAccessStream);
		randomAccessStream.Seek(0);
		using var input = randomAccessStream.GetInputStreamAt(0);
		using var source = input.AsStreamForRead();
		source.CopyTo(stream);
	}

	public void Clear()
	{
		Text = string.Empty;
	}

	public void Delete(Range<int> range)
	{
		GetRange(range).Text = string.Empty;
	}

	public void Insert(int position, string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		Document.GetRange(position, position).Text = text;
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case TextControl.TextChangedEvent:
				Control.TextChanged += Control_TextChanged;
				break;
			case TextArea.SelectionChangedEvent:
				Control.SelectionChanged += Control_SelectionChanged;
				break;
			case TextArea.CaretIndexChangedEvent:
				Control.SelectionChanged += Control_CaretSelectionChanged;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void Control_TextChanged(object sender, mux.RoutedEventArgs e)
	{
		if (_suppressTextChanged == 0)
			Callback.OnTextChanged(Widget, EventArgs.Empty);
	}

	void Control_SelectionChanged(object sender, mux.RoutedEventArgs e)
	{
		if (_suppressSelectionChanged != 0)
			return;

		var selection = Selection;
		if (_lastSelection != selection)
		{
			_lastSelection = selection;
			Callback.OnSelectionChanged(Widget, EventArgs.Empty);
		}
	}

	void Control_CaretSelectionChanged(object sender, mux.RoutedEventArgs e)
	{
		var caretIndex = CaretIndex;
		if (_lastCaretIndex != caretIndex)
		{
			_lastCaretIndex = caretIndex;
			Callback.OnCaretIndexChanged(Widget, EventArgs.Empty);
		}
	}

	sealed class SelectionFontHandler(string familyName, float size, FontStyle style, FontDecoration decoration) : Font.IHandler
	{
		public string ID { get; set; } = string.Empty;
		public Widget Widget { get; set; }
		public IntPtr NativeHandle => IntPtr.Zero;
		public string FamilyName => familyName;
		public FontStyle FontStyle => style;
		public FontDecoration FontDecoration => decoration;
		public float XHeight => size * 0.5f;
		public float Ascent => size * 0.8f;
		public float Descent => size * 0.2f;
		public float LineHeight => size;
		public float Leading => 0;
		public float Baseline => size;
		public float Size => size;
		public FontFamily Family => new(new SelectionFontFamilyHandler(familyName));
		public FontTypeface Typeface => new(Family, new SelectionFontTypefaceHandler(Family, familyName, style));
		public void Initialize() { }
		public void HandleEvent(string id, bool defaultEvent = false) { }
		public SizeF MeasureString(string text) => SizeF.Empty;
		public void Create(SystemFont systemFont, float? size, FontDecoration decoration) => throw new NotSupportedException();
		public void Create(FontFamily family, float size, FontStyle style, FontDecoration decoration) => throw new NotSupportedException();
		public void Create(FontTypeface typeface, float size, FontDecoration decoration) => throw new NotSupportedException();
	}

	sealed class SelectionFontFamilyHandler(string name) : FontFamily.IHandler
	{
		public string ID { get; set; } = string.Empty;
		public Widget Widget { get; set; }
		public IntPtr NativeHandle => IntPtr.Zero;
		public string Name => name;
		public string LocalizedName => name;
		public IEnumerable<FontTypeface> Typefaces => [];
		public void Initialize() { }
		public void HandleEvent(string id, bool defaultEvent = false) { }
		public void Create(string familyName) => throw new NotSupportedException();
		public void CreateFromFiles(IEnumerable<string> fileNames) => throw new NotSupportedException();
		public void CreateFromStreams(IEnumerable<Stream> streams) => throw new NotSupportedException();
	}

	sealed class SelectionFontTypefaceHandler(FontFamily family, string name, FontStyle style) : FontTypeface.IHandler
	{
		public string ID { get; set; } = string.Empty;
		public Widget Widget { get; set; }
		public IntPtr NativeHandle => IntPtr.Zero;
		public string Name => name;
		public string PostScriptName => name;
		public string LocalizedName => name;
		public FontStyle FontStyle => style;
		public bool IsSymbol => false;
		public FontFamily Family => family;
		public void Initialize() { }
		public void HandleEvent(string id, bool defaultEvent = false) { }
		public void Create(FontFamily family) => throw new NotSupportedException();
		public void Create(Stream stream) => throw new NotSupportedException();
		public void Create(string fileName) => throw new NotSupportedException();
		public bool HasCharacterRanges(IEnumerable<Range<int>> ranges) => true;
	}
}
