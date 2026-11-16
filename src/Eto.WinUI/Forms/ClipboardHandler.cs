using System.Runtime.InteropServices;
using wadt = Windows.ApplicationModel.DataTransfer;

namespace Eto.WinUI.Forms;

public class ClipboardHandler : WidgetHandler<object, Clipboard, Clipboard.ICallback>, Clipboard.IHandler
{
	// what this instance put on the clipboard, so setting more formats adds to them
	readonly DataStore _store = new();
	uint _sequence;

	DataStore Read()
	{
		return new DataStore { View = wadt.Clipboard.GetContent() };
	}

	void Write(Action<DataStore> set)
	{
		// start again when something else changed the clipboard since we last set it
		if (_sequence != GetClipboardSequenceNumber())
			_store.Clear();
		set(_store);

		var package = new wadt.DataPackage();
		_store.CopyTo(package);
		wadt.Clipboard.SetContent(package);
		try
		{
			// keep the data on the clipboard once the app exits
			wadt.Clipboard.Flush();
		}
		catch (COMException)
		{
		}
		_sequence = GetClipboardSequenceNumber();
	}

	public string[] Types => Read().Types;
	public bool ContainsText => Read().ContainsText;
	public bool ContainsHtml => Read().ContainsHtml;
	public bool ContainsImage => Read().ContainsImage;
	public bool ContainsUris => Read().ContainsUris;

	public string Text
	{
		get => Read().Text!;
		set => Write(s => s.Text = value);
	}

	public string Html
	{
		get => Read().Html!;
		set => Write(s => s.Html = value);
	}

	public Image Image
	{
		get => Read().Image!;
		set => Write(s => s.Image = value);
	}

	public Uri[] Uris
	{
		get => Read().Uris!;
		set => Write(s => s.Uris = value);
	}

	public void Clear()
	{
		_store.Clear();
		wadt.Clipboard.Clear();
		_sequence = GetClipboardSequenceNumber();
	}

	public bool Contains(string type) => Read().Contains(type);

	public byte[] GetData(string type) => Read().GetData(type)!;

	public string GetString(string type) => Read().GetString(type)!;

	public void SetData(byte[] value, string type) => Write(s => s.SetData(value, type));

	public void SetString(string value, string type) => Write(s => s.SetString(value, type));

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

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Clipboard.ChangedEvent:
				wadt.Clipboard.ContentChanged += HandleContentChanged;
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}

	void HandleContentChanged(object? sender, object e)
	{
		Application.Instance.AsyncInvoke(() =>
		{
			if (!Widget.IsDisposed)
				Callback.OnChanged(Widget, EventArgs.Empty);
		});
	}

	protected override void Dispose(bool disposing)
	{
		// the clipboard event is static, so it would keep this alive
		wadt.Clipboard.ContentChanged -= HandleContentChanged;
		base.Dispose(disposing);
	}

	[DllImport("user32.dll")]
	static extern uint GetClipboardSequenceNumber();
}
