using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

internal class ScreenHandler : WidgetHandler<ScreenHandler.ScreenInfo, Screen>, Screen.IHandler
{
	sealed class ScreenHelper
	{
		readonly IReadOnlyList<ScreenInfo> _screens;

		public ScreenHelper(IReadOnlyList<ScreenInfo> screens)
		{
			_screens = screens;
		}

		IEnumerable<ScreenInfo> AllScreens => _screens;

		ScreenInfo PrimaryScreen => _screens.First(r => r.IsPrimary);

		static Rectangle GetBounds(ScreenInfo screen) => screen.Bounds;

		public SizeF GetLogicalSize(ScreenInfo screen) => new SizeF(screen.Bounds.Width / screen.LogicalPixelSize, screen.Bounds.Height / screen.LogicalPixelSize);

		float GetLogicalPixelSize(ScreenInfo screen) => screen.LogicalPixelSize;

		float GetMaxLogicalPixelSize()
		{
			float logicalPixelSize = 0;
			foreach (var screen in AllScreens)
				logicalPixelSize = Math.Max(logicalPixelSize, GetLogicalPixelSize(screen));
			return logicalPixelSize;
		}

		public PointF GetLogicalLocation(ScreenInfo screen)
		{
			var bounds = GetBounds(screen);
			var primaryScreen = PrimaryScreen;
			if (screen.Equals(primaryScreen))
				return new PointF(bounds.X, bounds.Y);
			var primaryBounds = GetBounds(primaryScreen);
			var location = new PointF(primaryBounds.X, primaryBounds.Y);

			var allScreens = AllScreens.ToList();
			var maxLogicalPixelSize = GetMaxLogicalPixelSize();

			if (bounds.X < primaryBounds.X)
			{
				var adjacentScreen = primaryScreen;
				foreach (var scn in allScreens.OrderByDescending(s => GetBounds(s).X))
				{
					var scnBounds = GetBounds(scn);
					if (scnBounds.X > primaryBounds.X || (!scn.Equals(screen) && bounds.Right > scnBounds.X))
						continue;
					if (scnBounds.X < bounds.X)
						break;
					if (scnBounds.Right == GetBounds(adjacentScreen).X)
					{
						var logicalSize = GetLogicalSize(scn);
						location.X -= logicalSize.Width;
						adjacentScreen = scn;
					}
					if (scn.Equals(screen))
						break;
				}
				if (!adjacentScreen.Equals(screen))
					location.X = bounds.X / maxLogicalPixelSize;
			}
			else if (bounds.X > primaryBounds.X)
			{
				var adjacentScreen = primaryScreen;
				foreach (var scn in allScreens.OrderBy(s => GetBounds(s).X))
				{
					var scnBounds = GetBounds(scn);
					if (scnBounds.X < primaryBounds.X || (!scn.Equals(screen) && bounds.X < scnBounds.Right))
						continue;
					if (scnBounds.X > bounds.X)
						break;
					if (scnBounds.X == GetBounds(adjacentScreen).Right)
					{
						var logicalSize = GetLogicalSize(adjacentScreen);
						location.X += logicalSize.Width;
						adjacentScreen = scn;
					}
					if (scn.Equals(screen))
						break;
				}
				if (!adjacentScreen.Equals(screen))
					location.X = bounds.X / maxLogicalPixelSize;
			}

			if (bounds.Y < primaryBounds.Y)
			{
				var adjacentScreen = primaryScreen;
				foreach (var scn in allScreens.OrderByDescending(s => GetBounds(s).Y))
				{
					var scnBounds = GetBounds(scn);
					if (scnBounds.Y > primaryBounds.Y || (!scn.Equals(screen) && bounds.Bottom > scnBounds.Y))
						continue;
					if (scnBounds.Y < bounds.Y)
						break;
					if (scnBounds.Bottom == GetBounds(adjacentScreen).Y)
					{
						var logicalSize = GetLogicalSize(scn);
						location.Y -= logicalSize.Height;
						adjacentScreen = scn;
					}
					if (scn.Equals(screen))
						break;
				}
				if (!adjacentScreen.Equals(screen))
					location.Y = bounds.Y / maxLogicalPixelSize;
			}
			else if (bounds.Y > primaryBounds.Y)
			{
				var adjacentScreen = primaryScreen;
				foreach (var scn in allScreens.OrderBy(s => GetBounds(s).Y))
				{
					var scnBounds = GetBounds(scn);
					if (scnBounds.Y < primaryBounds.Y || (!scn.Equals(screen) && bounds.Y < scnBounds.Bottom))
						continue;
					if (scnBounds.Y > bounds.Y)
						break;
					if (scnBounds.Y == GetBounds(adjacentScreen).Bottom)
					{
						var logicalSize = GetLogicalSize(adjacentScreen);
						location.Y += logicalSize.Height;
						adjacentScreen = scn;
					}
					if (scn.Equals(screen))
						break;
				}
				if (!adjacentScreen.Equals(screen))
					location.Y = bounds.Y / maxLogicalPixelSize;
			}

			return location;
		}
	}

	internal sealed class ScreenInfo : IEquatable<ScreenInfo>
	{
		public IntPtr MonitorHandle { get; init; }
		public Rectangle Bounds { get; init; }
		public Rectangle WorkingArea { get; init; }
		public bool IsPrimary { get; init; }
		public int BitsPerPixel { get; init; }
		public float LogicalPixelSize { get; init; }

		public bool Equals(ScreenInfo? other) => other != null && MonitorHandle == other.MonitorHandle;

		public override bool Equals(object? obj) => obj is ScreenInfo other && Equals(other);

		public override int GetHashCode() => MonitorHandle.GetHashCode();
	}

	static IReadOnlyList<ScreenInfo> EnumerateScreens()
	{
		var screens = new List<ScreenInfo>();
		bool Callback(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data)
		{
			var info = new MONITORINFOEX();
			if (!GetMonitorInfo(monitor, ref info))
				return true;

			var bounds = info.rcMonitor.ToEtoRectangle();
			var workingArea = info.rcWork.ToEtoRectangle();
			var logicalPixelSize = GetLogicalPixelSize(monitor);

			screens.Add(new ScreenInfo
			{
				MonitorHandle = monitor,
				Bounds = bounds,
				WorkingArea = workingArea,
				IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
				BitsPerPixel = GetBitsPerPixel(info.szDevice),
				LogicalPixelSize = logicalPixelSize
			});
			return true;
		}

		EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

		return screens;
	}

	internal static IReadOnlyList<ScreenInfo> GetScreens() => EnumerateScreens();

	internal static PointF PhysicalToLogical(PointF point)
	{
		var screen = FindScreenContainingPhysicalPoint(point) ?? GetPrimaryScreen();
		if (screen == null)
			return point;

		var logicalBounds = new ScreenHandler(screen).Bounds;
		return new PointF(
			logicalBounds.X + ((point.X - screen.Bounds.X) / screen.LogicalPixelSize),
			logicalBounds.Y + ((point.Y - screen.Bounds.Y) / screen.LogicalPixelSize));
	}

	internal static PointF LogicalToPhysical(PointF point)
	{
		var screen = FindScreenContainingLogicalPoint(point) ?? GetPrimaryScreen();
		if (screen == null)
			return point;

		var logicalBounds = new ScreenHandler(screen).Bounds;
		return new PointF(
			screen.Bounds.X + ((point.X - logicalBounds.X) * screen.LogicalPixelSize),
			screen.Bounds.Y + ((point.Y - logicalBounds.Y) * screen.LogicalPixelSize));
	}

	static ScreenInfo? GetPrimaryScreen() => GetScreens().FirstOrDefault(r => r.IsPrimary) ?? GetScreens().FirstOrDefault();

	static ScreenInfo? FindScreenContainingPhysicalPoint(PointF point)
	{
		return FindBestScreen(GetScreens(), screen => Contains(screen.Bounds, point), screen => GetDistanceSquared(screen.Bounds, point));
	}

	static ScreenInfo? FindScreenContainingLogicalPoint(PointF point)
	{
		return FindBestScreen(GetScreens(), screen => new ScreenHandler(screen).Bounds.Contains(point), screen => GetDistanceSquared(new ScreenHandler(screen).Bounds, point));
	}

	static ScreenInfo? FindBestScreen(IEnumerable<ScreenInfo> screens, Func<ScreenInfo, bool> contains, Func<ScreenInfo, float> distance)
	{
		ScreenInfo? closest = null;
		var closestDistance = float.MaxValue;
		foreach (var screen in screens)
		{
			if (contains(screen))
				return screen;

			var screenDistance = distance(screen);
			if (screenDistance < closestDistance)
			{
				closest = screen;
				closestDistance = screenDistance;
			}
		}

		return closest;
	}

	static bool Contains(Rectangle bounds, PointF point) => point.X >= bounds.Left && point.X < bounds.Right && point.Y >= bounds.Top && point.Y < bounds.Bottom;

	static float GetDistanceSquared(RectangleF bounds, PointF point)
	{
		var dx = point.X < bounds.Left ? bounds.Left - point.X : point.X > bounds.Right ? point.X - bounds.Right : 0f;
		var dy = point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0f;
		return (dx * dx) + (dy * dy);
	}

	static float GetDistanceSquared(Rectangle bounds, PointF point) => GetDistanceSquared(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), point);

	static float GetLogicalPixelSize(IntPtr monitor)
	{
		if (GetDpiForMonitor(monitor, MonitorDpiType.EffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0)
			return dpiX / 96f;
		return 1f;
	}

	static int GetBitsPerPixel(string deviceName)
	{
		var hdc = CreateDC(deviceName, deviceName, null, IntPtr.Zero);
		if (hdc == IntPtr.Zero)
			return 32;
		try
		{
			var bits = GetDeviceCaps(hdc, BITSPIXEL);
			var planes = GetDeviceCaps(hdc, PLANES);
			if (bits <= 0 || planes <= 0)
				return 32;
			return bits * planes;
		}
		finally
		{
			DeleteDC(hdc);
		}
	}

	public ScreenHandler(ScreenInfo screen)
	{
		Control = screen;
	}

	public float RealScale => Control.LogicalPixelSize * Scale;

	public float Scale => 96f / 72f;

	public RectangleF Bounds
	{
		get
		{
			var helper = new ScreenHelper(GetScreens());
			return new RectangleF(helper.GetLogicalLocation(Control), helper.GetLogicalSize(Control));
		}
	}

	public RectangleF WorkingArea
	{
		get
		{
			var bounds = Bounds;
			var physicalBounds = Control.Bounds;
			var physicalWorkingArea = Control.WorkingArea;
			var offset = new PointF(
				(physicalWorkingArea.X - physicalBounds.X) / Control.LogicalPixelSize,
				(physicalWorkingArea.Y - physicalBounds.Y) / Control.LogicalPixelSize);
			var size = new SizeF(
				physicalWorkingArea.Width / Control.LogicalPixelSize,
				physicalWorkingArea.Height / Control.LogicalPixelSize);
			return new RectangleF(bounds.Location + offset, size);
		}
	}

	public int BitsPerPixel => Control.BitsPerPixel;

	public bool IsPrimary => Control.IsPrimary;

	public Image GetImage(RectangleF rect)
	{
		throw new NotSupportedException("Screen capture is not implemented for Eto.WinUI.");
	}

	const int MONITORINFOF_PRIMARY = 0x00000001;
	const int BITSPIXEL = 12;
	const int PLANES = 14;

	delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

	enum MonitorDpiType : uint
	{
		EffectiveDpi = 0
	}

	[StructLayout(LayoutKind.Sequential)]
	struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;

		public Rectangle ToEtoRectangle() => new Rectangle(Left, Top, Right - Left, Bottom - Top);
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	struct MONITORINFOEX
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public int dwFlags;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szDevice;

		public MONITORINFOEX()
		{
			cbSize = Marshal.SizeOf<MONITORINFOEX>();
			szDevice = string.Empty;
		}
	}

	[DllImport("user32.dll")]
	static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

	[DllImport("shcore.dll")]
	static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

	[DllImport("gdi32.dll", CharSet = CharSet.Auto)]
	static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

	[DllImport("gdi32.dll")]
	static extern bool DeleteDC(IntPtr hdc);

	[DllImport("gdi32.dll")]
	static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}
