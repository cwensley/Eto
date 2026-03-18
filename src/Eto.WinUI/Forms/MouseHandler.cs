using System.Runtime.InteropServices;

namespace Eto.WinUI.Forms;

public class MouseHandler : Mouse.IHandler
{
	public Widget Widget { get; set; }

	public Eto.Platform Platform { get; set; }

	public void Initialize()
	{
	}

	public PointF Position
	{
		get
		{
			if (!GetCursorPos(out var point))
				return PointF.Empty;
			return PhysicalToLogical(point.X, point.Y);
		}
		set
		{
			var point = LogicalToPhysical(value);
			SetCursorPos(point.X, point.Y);
		}
	}

	public MouseButtons Buttons
	{
		get
		{
			var buttons = MouseButtons.None;
			if (IsVirtualKeyPressed(VK_LBUTTON))
				buttons |= MouseButtons.Primary;
			if (IsVirtualKeyPressed(VK_RBUTTON))
				buttons |= MouseButtons.Alternate;
			if (IsVirtualKeyPressed(VK_MBUTTON))
				buttons |= MouseButtons.Middle;
			return buttons;
		}
	}

	public void SetCursor(Cursor cursor)
	{
		// Temporary global cursor changes are not supported until Eto.WinUI has a Cursor handler.
	}

	static bool IsVirtualKeyPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

	static PointF PhysicalToLogical(int x, int y)
	{
		return ScreenHandler.PhysicalToLogical(new PointF(x, y));
	}

	static POINT LogicalToPhysical(PointF point)
	{
		var physicalPoint = ScreenHandler.LogicalToPhysical(point);
		return new POINT
		{
			X = (int)Math.Round(physicalPoint.X),
			Y = (int)Math.Round(physicalPoint.Y)
		};
	}

	const int VK_LBUTTON = 0x01;
	const int VK_RBUTTON = 0x02;
	const int VK_MBUTTON = 0x04;

	[StructLayout(LayoutKind.Sequential)]
	struct POINT
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll")]
	static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	static extern short GetAsyncKeyState(int vKey);
}
