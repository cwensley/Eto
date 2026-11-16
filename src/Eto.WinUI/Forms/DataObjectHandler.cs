using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Eto.WinUI.Drawing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Eto.WinUI.Forms;

/// <summary>
/// Data held by a <see cref="DataObject"/> or the clipboard, either set by Eto or read from a <see cref="DataPackageView"/>.
/// </summary>
class DataStore
{
	// keyed by DataPackage format id so Types matches what other apps see
	readonly List<KeyValuePair<string, object>> _entries = new();
	const string UrisKey = "\0eto-uris";

	public DataPackageView? View { get; set; }

	public bool IsEmpty => _entries.Count == 0;

	public void Clear()
	{
		_entries.Clear();
		View = null;
	}

	void Set(string format, object? value)
	{
		_entries.RemoveAll(r => r.Key == format);
		if (value != null)
			_entries.Add(new KeyValuePair<string, object>(format, value));
	}

	object? Get(string format) => _entries.FirstOrDefault(r => r.Key == format).Value;

	public string[] Types
	{
		get
		{
			if (View != null)
				return View.AvailableFormats.ToArray();
			var types = new List<string>();
			foreach (var entry in _entries)
			{
				if (entry.Key == UrisKey)
				{
					var uris = (Uri[])entry.Value;
					if (uris.Any(r => r.IsFile))
						types.Add(StandardDataFormats.StorageItems);
					if (uris.Any(r => !r.IsFile))
						types.Add(StandardDataFormats.WebLink);
				}
				else
					types.Add(entry.Key);
			}
			return types.ToArray();
		}
	}

	public bool Contains(string type)
	{
		if (View != null)
			return View.Contains(type);
		return Types.Contains(type);
	}

	public bool ContainsText => Contains(StandardDataFormats.Text);
	public bool ContainsHtml => Contains(StandardDataFormats.Html);
	public bool ContainsImage => Contains(StandardDataFormats.Bitmap) || Contains(PngFormat);

	// what browsers and Office use for images with transparency, and what reads back reliably
	const string PngFormat = "PNG";
	public bool ContainsUris => Contains(StandardDataFormats.StorageItems) || Contains(StandardDataFormats.WebLink);

	public string? Text
	{
		get
		{
			if (View == null)
				return Get(StandardDataFormats.Text) as string;
			return View.Contains(StandardDataFormats.Text) ? AsyncWait.Wait(View.GetTextAsync()) : null;
		}
		set => Set(StandardDataFormats.Text, value);
	}

	public string? Html
	{
		get
		{
			if (View == null)
				return Get(StandardDataFormats.Html) as string;
			if (!View.Contains(StandardDataFormats.Html))
				return null;
			// strip the CF_HTML header and context, leaving what was set
			var html = AsyncWait.Wait(View.GetHtmlFormatAsync());
			return html == null ? null : HtmlFormatHelper.GetStaticFragment(html);
		}
		set => Set(StandardDataFormats.Html, value);
	}

	public Image? Image
	{
		get
		{
			if (View == null)
				return Get(StandardDataFormats.Bitmap) as Image;
			if (View.Contains(PngFormat) && GetValue(PngFormat) is byte[] png)
				return new Bitmap(new MemoryStream(png));
			if (!View.Contains(StandardDataFormats.Bitmap))
				return null;
			var reference = AsyncWait.Wait(View.GetBitmapAsync());
			if (reference == null)
				return null;
			using var stream = AsyncWait.Wait(reference.OpenReadAsync());
			return new Bitmap(stream.AsStreamForRead());
		}
		// keep encoded bytes rather than the image, so it survives the source being disposed
		set => Set(StandardDataFormats.Bitmap, value == null ? null : new Bitmap(new MemoryStream(EncodeImage(value))));
	}

	public Uri[]? Uris
	{
		get
		{
			if (View == null)
				return Get(UrisKey) as Uri[];
			var uris = new List<Uri>();
			if (View.Contains(StandardDataFormats.StorageItems))
			{
				var items = AsyncWait.Wait(View.GetStorageItemsAsync());
				uris.AddRange(items.Where(r => !string.IsNullOrEmpty(r.Path)).Select(r => new Uri(r.Path)));
			}
			if (View.Contains(StandardDataFormats.WebLink))
			{
				var link = AsyncWait.Wait(View.GetWebLinkAsync());
				if (link != null)
					uris.Add(link);
			}
			return uris.Count > 0 ? uris.ToArray() : null;
		}
		set => Set(UrisKey, value?.Length > 0 ? value : null);
	}

	public void SetString(string? value, string type)
	{
		if (type == StandardDataFormats.Text)
			Text = value;
		else if (type == StandardDataFormats.Html)
			Html = value;
		else
			Set(type, value);
	}

	public string? GetString(string type)
	{
		if (type == StandardDataFormats.Text)
			return Text;
		if (type == StandardDataFormats.Html)
			return Html;
		return GetValue(type) switch
		{
			string text => text,
			byte[] bytes => Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
			_ => null
		};
	}

	public void SetData(byte[]? value, string type) => Set(type, value);

	public byte[]? GetData(string type)
	{
		return GetValue(type) switch
		{
			byte[] bytes => bytes,
			string text => Encoding.Unicode.GetBytes(text),
			_ => null
		};
	}

	object? GetValue(string type)
	{
		if (View == null)
			return Get(type);
		if (!View.Contains(type))
			return null;
		var value = AsyncWait.Wait(View.GetDataAsync(type));
		return value switch
		{
			IRandomAccessStream stream => ReadAll(stream),
			IRandomAccessStreamReference reference => ReadAll(AsyncWait.Wait(reference.OpenReadAsync())),
			IBuffer buffer => buffer.ToArray(),
			_ => value
		};
	}

	static byte[] ReadAll(IRandomAccessStream stream)
	{
		using (stream)
		{
			using var ms = new MemoryStream();
			stream.Seek(0);
			stream.AsStreamForRead().CopyTo(ms);
			return ms.ToArray();
		}
	}

	static byte[] EncodeImage(Image image)
	{
		using var ms = new MemoryStream();
		if (image is Bitmap bitmap)
			bitmap.Save(ms, ImageFormat.Png);
		else if (image is Icon icon)
			icon.GetFrame(1).Bitmap.Save(ms, ImageFormat.Png);
		return ms.ToArray();
	}

	static IRandomAccessStream ToStream(byte[] bytes)
	{
		// a memory stream is complete straight away, so there's nothing to wait on
		return new MemoryStream(bytes, false).AsRandomAccessStream();
	}

	/// <summary>Copies the data into <paramref name="package"/>, replacing what was there.</summary>
	public void CopyTo(DataPackage package)
	{
		if (View != null)
		{
			foreach (var format in View.AvailableFormats)
			{
				var value = GetValue(format);
				if (value != null)
					package.SetData(format, value is byte[] bytes ? ToStream(bytes) : value);
			}
			return;
		}

		foreach (var entry in _entries)
		{
			var format = entry.Key;
			switch (entry.Value)
			{
				case string text when format == StandardDataFormats.Text:
					package.SetText(text);
					break;
				case string html when format == StandardDataFormats.Html:
					package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(html));
					break;
				case Image image when format == StandardDataFormats.Bitmap:
					var encoded = EncodeImage(image);
					package.SetBitmap(RandomAccessStreamReference.CreateFromStream(ToStream(encoded)));
					package.SetData(PngFormat, ToStream(encoded));
					break;
				case Uri[] uris when format == UrisKey:
					SetUris(package, uris);
					break;
				case string text:
					// as Unicode, the same way WPF and WinForms store a custom string format
					package.SetData(format, ToStream(Encoding.Unicode.GetBytes(text + "\0")));
					break;
				case byte[] bytes:
					package.SetData(format, ToStream(bytes));
					break;
			}
		}
	}

	static void SetUris(DataPackage package, Uri[] uris)
	{
		var items = new List<IStorageItem>();
		foreach (var uri in uris.Where(r => r.IsFile))
		{
			var path = uri.LocalPath;
			if (Directory.Exists(path))
				items.Add(AsyncWait.Wait(StorageFolder.GetFolderFromPathAsync(path)));
			else if (File.Exists(path))
				items.Add(AsyncWait.Wait(StorageFile.GetFileFromPathAsync(path)));
		}
		if (items.Count > 0)
			package.SetStorageItems(items);
		// Windows only supports a single web link
		var link = uris.FirstOrDefault(r => !r.IsFile);
		if (link != null)
			package.SetWebLink(link);
	}
}

public class DataObjectHandler : WidgetHandler<DataPackage, DataObject>, DataObject.IHandler
{
	internal DataStore Store { get; } = new();

	public DataObjectHandler()
	{
	}

	public DataObjectHandler(DataPackageView view)
	{
		Store.View = view;
	}

	/// <summary>Copies the Eto data object into a WinUI data package.</summary>
	internal static void CopyTo(DataObject data, DataPackage package)
	{
		if (data.Handler is DataObjectHandler handler)
			handler.Store.CopyTo(package);
	}

	public string[] Types => Store.Types;
	public bool ContainsText => Store.ContainsText;
	public bool ContainsHtml => Store.ContainsHtml;
	public bool ContainsImage => Store.ContainsImage;
	public bool ContainsUris => Store.ContainsUris;

	public string Text
	{
		get => Store.Text!;
		set => Store.Text = value;
	}

	public string Html
	{
		get => Store.Html!;
		set => Store.Html = value;
	}

	public Image Image
	{
		get => Store.Image!;
		set => Store.Image = value;
	}

	public Uri[] Uris
	{
		get => Store.Uris!;
		set => Store.Uris = value;
	}

	public void Clear() => Store.Clear();

	public bool Contains(string type) => Store.Contains(type);

	public byte[] GetData(string type) => Store.GetData(type)!;

	public string GetString(string type) => Store.GetString(type)!;

	public void SetData(byte[] value, string type) => Store.SetData(value, type);

	public void SetString(string value, string type) => Store.SetString(value, type);

	public bool TrySetObject(object value, string type) => false;

	public void SetObject(object value, string type) => Widget.SetObject(value, type);
	public T GetObject<T>(string type) => Widget.GetObject<T>(type);
	public object GetObject(string type) => Widget.GetObject(type);
	public object GetObject(string type, Type objectType) => Widget.GetObject(type, objectType);

	public bool TryGetObject(string type, Type objectType, out object value)
	{
		value = null!;
		return false;
	}
}

public class DataFormatsHandler : DataFormats.IHandler
{
	public string Text => StandardDataFormats.Text;
	public string Html => StandardDataFormats.Html;
	public string Color => "Color";
}
