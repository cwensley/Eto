using System.Runtime.InteropServices;

namespace Eto.WinUI.Drawing;

/// <summary>
/// Gets file and stock icons from the Windows shell.
/// </summary>
public class SystemIconsHandler : SystemIcons.IHandler
{
	public Icon GetFileIcon(string fileName, SystemIconSize size)
	{
		var attributes = FILE_ATTRIBUTE_NORMAL;
		var flags = SHGFI_SYSICONINDEX;
		// files that don't exist still get the icon for their extension
		if (Directory.Exists(fileName))
			attributes = FILE_ATTRIBUTE_DIRECTORY;
		else if (!File.Exists(fileName))
			flags |= SHGFI_USEFILEATTRIBUTES;

		var info = new SHFILEINFO();
		if (SHGetFileInfo(fileName, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero)
			throw new ArgumentException($"Could not get the icon for '{fileName}'", nameof(fileName));
		return GetIcon(info.iIcon, size);
	}

	public Icon Get(SystemIconType type, SystemIconSize size)
	{
		var id = type switch
		{
			SystemIconType.OpenDirectory => SIID_FOLDEROPEN,
			SystemIconType.CloseDirectory => SIID_FOLDER,
			SystemIconType.Question => SIID_HELP,
			SystemIconType.Error => SIID_ERROR,
			SystemIconType.Information => SIID_INFO,
			SystemIconType.Warning => SIID_WARNING,
			_ => throw new NotSupportedException()
		};
		var info = new SHSTOCKICONINFO { cbSize = (uint)Marshal.SizeOf<SHSTOCKICONINFO>() };
		Marshal.ThrowExceptionForHR(SHGetStockIconInfo(id, SHGSI_SYSICONINDEX, ref info));
		return GetIcon(info.iSysImageIndex, size);
	}

	static Icon GetIcon(int index, SystemIconSize size)
	{
		var logicalSize = size == SystemIconSize.Small ? 16 : 32;
		var pixels = (int)Math.Ceiling(logicalSize * GetDpiForSystem() / 96.0);
		var imageList = pixels <= 16 ? SHIL_SMALL : pixels <= 32 ? SHIL_LARGE : pixels <= 48 ? SHIL_EXTRALARGE : SHIL_JUMBO;

		var bitmap = GetImageListBitmap(imageList, index)
			?? throw new InvalidOperationException("Could not get the system icon");
		return new Icon(new IconHandler(bitmap)).WithSize(logicalSize, logicalSize);
	}

	static unsafe Bitmap? GetImageListBitmap(int imageList, int index)
	{
		var iid = IID_IImageList;
		if (SHGetImageList(imageList, ref iid, out var list) != 0 || list == IntPtr.Zero)
			return null;
		try
		{
			// IImageList::GetIcon is slot 10 of the vtable (after IUnknown and 7 other methods)
			var vtable = *(IntPtr**)list;
			var getIcon = (delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)vtable[10];
			IntPtr hicon;
			if (getIcon(list, index, ILD_TRANSPARENT, &hicon) != 0 || hicon == IntPtr.Zero)
				return null;
			try
			{
				return ToBitmap(hicon);
			}
			finally
			{
				DestroyIcon(hicon);
			}
		}
		finally
		{
			var release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)(*(IntPtr**)list)[2];
			release(list);
		}
	}

	static Bitmap? ToBitmap(IntPtr hicon)
	{
		if (!GetIconInfo(hicon, out var iconInfo))
			return null;
		try
		{
			if (iconInfo.hbmColor == IntPtr.Zero)
				return null;
			if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out var bmp) == 0)
				return null;
			var width = bmp.bmWidth;
			var height = bmp.bmHeight;
			var pixels = GetBits(iconInfo.hbmColor, width, height);
			if (pixels == null)
				return null;

			// icons without an alpha channel use the mask for transparency
			var hasAlpha = false;
			for (int i = 3; i < pixels.Length; i += 4)
			{
				if (pixels[i] != 0)
				{
					hasAlpha = true;
					break;
				}
			}
			if (!hasAlpha)
			{
				var mask = iconInfo.hbmMask != IntPtr.Zero ? GetBits(iconInfo.hbmMask, width, height) : null;
				for (int i = 0; i < pixels.Length; i += 4)
					pixels[i + 3] = mask == null || mask[i] == 0 ? (byte)255 : (byte)0;
			}
			return new Bitmap(new BitmapHandler(width, height, pixels));
		}
		finally
		{
			if (iconInfo.hbmColor != IntPtr.Zero)
				DeleteObject(iconInfo.hbmColor);
			if (iconInfo.hbmMask != IntPtr.Zero)
				DeleteObject(iconInfo.hbmMask);
		}
	}

	static byte[]? GetBits(IntPtr hbitmap, int width, int height)
	{
		var info = new BITMAPINFOHEADER
		{
			biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
			biWidth = width,
			biHeight = -height, // top-down
			biPlanes = 1,
			biBitCount = 32,
		};
		var pixels = new byte[width * height * 4];
		var hdc = GetDC(IntPtr.Zero);
		try
		{
			return GetDIBits(hdc, hbitmap, 0, (uint)height, pixels, ref info, 0) == 0 ? null : pixels;
		}
		finally
		{
			ReleaseDC(IntPtr.Zero, hdc);
		}
	}

	const uint FILE_ATTRIBUTE_NORMAL = 0x80;
	const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
	const uint SHGFI_SYSICONINDEX = 0x4000;
	const uint SHGFI_USEFILEATTRIBUTES = 0x10;
	const uint SHGSI_SYSICONINDEX = 0x4000;
	const int SHIL_LARGE = 0;
	const int SHIL_SMALL = 1;
	const int SHIL_EXTRALARGE = 2;
	const int SHIL_JUMBO = 4;
	const int ILD_TRANSPARENT = 1;
	const uint SIID_FOLDER = 3;
	const uint SIID_FOLDEROPEN = 4;
	const uint SIID_HELP = 23;
	const uint SIID_WARNING = 78;
	const uint SIID_INFO = 79;
	const uint SIID_ERROR = 80;
	static readonly Guid IID_IImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct SHFILEINFO
	{
		public IntPtr hIcon;
		public int iIcon;
		public uint dwAttributes;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct SHSTOCKICONINFO
	{
		public uint cbSize;
		public IntPtr hIcon;
		public int iSysImageIndex;
		public int iIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szPath;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct ICONINFO
	{
		public int fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct BITMAP
	{
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public ushort bmPlanes;
		public ushort bmBitsPixel;
		public IntPtr bmBits;
	}

	[StructLayout(LayoutKind.Sequential)]
	struct BITMAPINFOHEADER
	{
		public uint biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public uint biCompression;
		public uint biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public uint biClrUsed;
		public uint biClrImportant;
		// room for the colour masks GetDIBits may write
		public uint mask0;
		public uint mask1;
		public uint mask2;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

	[DllImport("shell32.dll")]
	static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

	[DllImport("user32.dll", SetLastError = true)]
	static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport("user32.dll")]
	static extern uint GetDpiForSystem();

	[DllImport("user32.dll")]
	static extern IntPtr GetDC(IntPtr hWnd);

	[DllImport("user32.dll")]
	static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

	[DllImport("gdi32.dll")]
	static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

	[DllImport("gdi32.dll")]
	static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

	[DllImport("gdi32.dll")]
	static extern bool DeleteObject(IntPtr hObject);
}
