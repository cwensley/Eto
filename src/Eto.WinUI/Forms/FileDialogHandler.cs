using System.IO;
using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

/// <summary>
/// Base for the file and folder dialogs, using the Win32 common item dialog.
/// </summary>
/// <remarks>
/// The WinRT pickers are async only and can't set a title or start folder, so they can't do what Eto's dialogs do.
/// </remarks>
public abstract class CommonItemDialogHandler<TWidget> : WidgetHandler<object, TWidget>
	where TWidget : CommonDialog
{
	internal const uint FOS_OVERWRITEPROMPT = 0x2;
	internal const uint FOS_NOCHANGEDIR = 0x8;
	internal const uint FOS_PICKFOLDERS = 0x20;
	internal const uint FOS_FORCEFILESYSTEM = 0x40;
	internal const uint FOS_ALLOWMULTISELECT = 0x200;
	internal const uint FOS_PATHMUSTEXIST = 0x800;
	internal const uint FOS_FILEMUSTEXIST = 0x1000;
	const int ERROR_CANCELLED = unchecked((int)0x800704C7);
	const uint SIGDN_FILESYSPATH = 0x80058000;

	internal abstract FileDialogNativeMethods.IFileDialog CreateDialog();

	internal virtual void Prepare(FileDialogNativeMethods.IFileDialog dialog) { }

	internal abstract void ReadResult(FileDialogNativeMethods.IFileDialog dialog);

	public DialogResult ShowDialog(Window parent)
	{
		var dialog = CreateDialog();
		try
		{
			Prepare(dialog);
			var owner = parent?.NativeHandle ?? Application.Instance?.MainForm?.NativeHandle ?? IntPtr.Zero;
			var hr = dialog.Show(owner);
			if (hr == ERROR_CANCELLED)
				return DialogResult.Cancel;
			Marshal.ThrowExceptionForHR(hr);
			ReadResult(dialog);
			return DialogResult.Ok;
		}
		finally
		{
			Marshal.ReleaseComObject(dialog);
		}
	}

	internal static FileDialogNativeMethods.IShellItem? CreateItem(string? path)
	{
		if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
			return null;
		var iid = typeof(FileDialogNativeMethods.IShellItem).GUID;
		return FileDialogNativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var item) == 0 ? item : null;
	}

	internal static string? GetPath(FileDialogNativeMethods.IShellItem? item)
	{
		if (item == null)
			return null;
		item.GetDisplayName(SIGDN_FILESYSPATH, out var ptr);
		try
		{
			return Marshal.PtrToStringUni(ptr);
		}
		finally
		{
			Marshal.FreeCoTaskMem(ptr);
		}
	}
}

public abstract class FileDialogHandler<TWidget> : CommonItemDialogHandler<TWidget>, FileDialog.IHandler
	where TWidget : FileDialog
{
	string? _fileName;
	string? _directory;
	int _filterIndex;

	public string FileName
	{
		get => _fileName!;
		set
		{
			_fileName = string.IsNullOrEmpty(value) ? null : value;
			var dir = Path.GetDirectoryName(value);
			if (!string.IsNullOrEmpty(dir))
				_directory = dir;
		}
	}

	public Uri Directory
	{
		get => string.IsNullOrEmpty(_directory) ? null! : new Uri(_directory);
		set
		{
			_directory = value?.LocalPath;
			// last one wins, so a full path FileName moves into the new folder
			if (_directory != null && !string.IsNullOrEmpty(Path.GetDirectoryName(_fileName)))
				_fileName = Path.Combine(_directory, Path.GetFileName(_fileName)!);
		}
	}

	public int CurrentFilterIndex
	{
		get => _filterIndex;
		set => _filterIndex = value;
	}

	public bool CheckFileExists { get; set; }

	public string Title { get; set; } = string.Empty;

	public void InsertFilter(int index, FileFilter filter) { }

	public void RemoveFilter(int index) { }

	public void ClearFilters() { }

	internal override void Prepare(FileDialogNativeMethods.IFileDialog dialog)
	{
		var filters = Widget.Filters.Select(f => new FileDialogNativeMethods.COMDLG_FILTERSPEC
		{
			pszName = f.Name,
			pszSpec = string.Join(";", f.Extensions.Select(ex => ex.StartsWith("*") ? ex : "*" + ex))
		}).ToArray();
		if (filters.Length > 0)
		{
			dialog.SetFileTypes((uint)filters.Length, filters);
			dialog.SetFileTypeIndex((uint)Math.Min(Math.Max(_filterIndex, 0), filters.Length - 1) + 1);
		}

		if (!string.IsNullOrEmpty(Title))
			dialog.SetTitle(Title);

		var folder = CreateItem(_directory);
		if (folder != null)
			dialog.SetFolder(folder);

		if (_fileName != null)
			dialog.SetFileName(Path.GetFileName(_fileName));

		dialog.GetOptions(out var options);
		options |= FOS_FORCEFILESYSTEM | FOS_NOCHANGEDIR;
		if (CheckFileExists)
			options |= FOS_FILEMUSTEXIST;
		dialog.SetOptions(GetOptions(options));
	}

	internal virtual uint GetOptions(uint options) => options;

	internal override void ReadResult(FileDialogNativeMethods.IFileDialog dialog)
	{
		dialog.GetResult(out var item);
		_fileName = GetPath(item);
		_directory = Path.GetDirectoryName(_fileName);
		dialog.GetFileTypeIndex(out var index);
		if (index > 0)
			_filterIndex = (int)index - 1;
	}
}

public class OpenFileDialogHandler : FileDialogHandler<OpenFileDialog>, OpenFileDialog.IHandler
{
	string[]? _fileNames;

	public OpenFileDialogHandler()
	{
		CheckFileExists = true;
	}

	public bool MultiSelect { get; set; }

	public IEnumerable<string> Filenames => _fileNames ?? (FileName != null ? new[] { FileName } : Array.Empty<string>());

	internal override FileDialogNativeMethods.IFileDialog CreateDialog() => (FileDialogNativeMethods.IFileDialog)new FileDialogNativeMethods.FileOpenDialog();

	internal override uint GetOptions(uint options) => MultiSelect ? options | FOS_ALLOWMULTISELECT : options;

	internal override void ReadResult(FileDialogNativeMethods.IFileDialog dialog)
	{
		if (dialog is FileDialogNativeMethods.IFileOpenDialog openDialog && MultiSelect)
		{
			openDialog.GetResults(out var items);
			items.GetCount(out var count);
			var names = new List<string>();
			for (uint i = 0; i < count; i++)
			{
				items.GetItemAt(i, out var item);
				var path = GetPath(item);
				if (path != null)
					names.Add(path);
			}
			_fileNames = names.ToArray();
			FileName = names.FirstOrDefault()!;
			dialog.GetFileTypeIndex(out var index);
			if (index > 0)
				CurrentFilterIndex = (int)index - 1;
			return;
		}
		base.ReadResult(dialog);
		_fileNames = null;
	}
}

public class SaveFileDialogHandler : FileDialogHandler<SaveFileDialog>, SaveFileDialog.IHandler
{
	internal override FileDialogNativeMethods.IFileDialog CreateDialog() => (FileDialogNativeMethods.IFileDialog)new FileDialogNativeMethods.FileSaveDialog();

	internal override uint GetOptions(uint options) => options | FOS_OVERWRITEPROMPT;

	internal override void Prepare(FileDialogNativeMethods.IFileDialog dialog)
	{
		base.Prepare(dialog);
		// append the selected filter's extension when the user doesn't type one
		var extension = Widget.CurrentFilter?.Extensions?.FirstOrDefault()?.TrimStart('*', '.');
		if (!string.IsNullOrEmpty(extension))
			dialog.SetDefaultExtension(extension);
	}
}

public class SelectFolderDialogHandler : CommonItemDialogHandler<SelectFolderDialog>, SelectFolderDialog.IHandler
{
	public string Title { get; set; } = string.Empty;

	public string Directory { get; set; } = string.Empty;

	internal override FileDialogNativeMethods.IFileDialog CreateDialog() => (FileDialogNativeMethods.IFileDialog)new FileDialogNativeMethods.FileOpenDialog();

	internal override void Prepare(FileDialogNativeMethods.IFileDialog dialog)
	{
		if (!string.IsNullOrEmpty(Title))
			dialog.SetTitle(Title);
		var folder = CreateItem(Directory);
		if (folder != null)
			dialog.SetFolder(folder);
		dialog.GetOptions(out var options);
		dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR);
	}

	internal override void ReadResult(FileDialogNativeMethods.IFileDialog dialog)
	{
		dialog.GetResult(out var item);
		Directory = GetPath(item) ?? Directory;
	}
}

public class OpenWithDialogHandler : WidgetHandler<object, OpenWithDialog>, OpenWithDialog.IHandler
{
	public string FilePath { get; set; } = string.Empty;

	public DialogResult ShowDialog(Window parent)
	{
		var info = new FileDialogNativeMethods.OPENASINFO
		{
			pcszFile = FilePath,
			oaifInFlags = FileDialogNativeMethods.OAIF_ALLOW_REGISTRATION | FileDialogNativeMethods.OAIF_EXEC
		};
		var hr = FileDialogNativeMethods.SHOpenWithDialog(parent?.NativeHandle ?? IntPtr.Zero, ref info);
		return hr == 0 ? DialogResult.Ok : DialogResult.Cancel;
	}
}

static class FileDialogNativeMethods
{
	public const int OAIF_ALLOW_REGISTRATION = 0x1;
	public const int OAIF_EXEC = 0x4;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct OPENASINFO
	{
		public string pcszFile;
		public string? pcszClass;
		public int oaifInFlags;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHOpenWithDialog(IntPtr hwndParent, ref OPENASINFO poainfo);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct COMDLG_FILTERSPEC
	{
		[MarshalAs(UnmanagedType.LPWStr)]
		public string pszName;
		[MarshalAs(UnmanagedType.LPWStr)]
		public string pszSpec;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

	[ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
	public class FileOpenDialog { }

	[ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
	public class FileSaveDialog { }

	[ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IFileDialog
	{
		[PreserveSig] int Show(IntPtr parent);
		void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
		void SetFileTypeIndex(uint iFileType);
		void GetFileTypeIndex(out uint piFileType);
		void Advise(IntPtr pfde, out uint pdwCookie);
		void Unadvise(uint dwCookie);
		void SetOptions(uint fos);
		void GetOptions(out uint pfos);
		void SetDefaultFolder(IShellItem psi);
		void SetFolder(IShellItem psi);
		void GetFolder(out IShellItem ppsi);
		void GetCurrentSelection(out IShellItem ppsi);
		void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
		void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
		void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
		void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
		void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
		void GetResult(out IShellItem ppsi);
		void AddPlace(IShellItem psi, int fdap);
		void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
		void Close(int hr);
		void SetClientGuid(ref Guid guid);
		void ClearClientData();
		void SetFilter(IntPtr pFilter);
	}

	[ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IFileOpenDialog : IFileDialog
	{
		[PreserveSig] new int Show(IntPtr parent);
		new void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
		new void SetFileTypeIndex(uint iFileType);
		new void GetFileTypeIndex(out uint piFileType);
		new void Advise(IntPtr pfde, out uint pdwCookie);
		new void Unadvise(uint dwCookie);
		new void SetOptions(uint fos);
		new void GetOptions(out uint pfos);
		new void SetDefaultFolder(IShellItem psi);
		new void SetFolder(IShellItem psi);
		new void GetFolder(out IShellItem ppsi);
		new void GetCurrentSelection(out IShellItem ppsi);
		new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
		new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
		new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
		new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
		new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
		new void GetResult(out IShellItem ppsi);
		new void AddPlace(IShellItem psi, int fdap);
		new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
		new void Close(int hr);
		new void SetClientGuid(ref Guid guid);
		new void ClearClientData();
		new void SetFilter(IntPtr pFilter);
		void GetResults(out IShellItemArray ppenum);
		void GetSelectedItems(out IShellItemArray ppsai);
	}

	[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellItem
	{
		void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
		void GetParent(out IShellItem ppsi);
		void GetDisplayName(uint sigdnName, out IntPtr ppszName);
		void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
		void Compare(IShellItem psi, uint hint, out int piOrder);
	}

	[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellItemArray
	{
		void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
		void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
		void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
		void GetAttributes(int dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
		void GetCount(out uint pdwNumItems);
		void GetItemAt(uint dwIndex, out IShellItem ppsi);
		void EnumItems(out IntPtr ppenumShellItems);
	}
}
