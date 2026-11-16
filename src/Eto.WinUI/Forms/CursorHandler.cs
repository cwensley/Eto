using System.Reflection;
using Microsoft.UI.Input;

namespace Eto.WinUI.Forms;

/// <summary>
/// Cursor handler for WinUI, only system cursors are supported.
/// </summary>
public class CursorHandler : WidgetHandler<InputCursor, Cursor>, Cursor.IHandler
{
	public void Create(CursorType type)
	{
		Control = InputSystemCursor.Create(type switch
		{
			CursorType.Crosshair => InputSystemCursorShape.Cross,
			CursorType.Pointer => InputSystemCursorShape.Hand,
			CursorType.Move or CursorType.SizeAll => InputSystemCursorShape.SizeAll,
			CursorType.IBeam => InputSystemCursorShape.IBeam,
			CursorType.VerticalSplit or CursorType.SizeLeft or CursorType.SizeRight => InputSystemCursorShape.SizeWestEast,
			CursorType.HorizontalSplit or CursorType.SizeTop or CursorType.SizeBottom => InputSystemCursorShape.SizeNorthSouth,
			CursorType.SizeTopLeft or CursorType.SizeBottomRight => InputSystemCursorShape.SizeNorthwestSoutheast,
			CursorType.SizeTopRight or CursorType.SizeBottomLeft => InputSystemCursorShape.SizeNortheastSouthwest,
			CursorType.NotAllowed => InputSystemCursorShape.UniversalNo,
			_ => InputSystemCursorShape.Arrow
		});
	}

	// WinUI can only create cursors from system shapes or exe resources, so custom cursors show the arrow
	public void Create(Image image, PointF hotspot) => Create(CursorType.Arrow);

	public void Create(string fileName) => Create(CursorType.Arrow);

	public void Create(Stream stream) => Create(CursorType.Arrow);

	static readonly PropertyInfo? s_protectedCursor = typeof(mux.UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic);

	/// <summary>Sets the cursor shown over the element, or null for its default.</summary>
	internal static void SetCursor(mux.UIElement element, Cursor? cursor)
	{
		// ProtectedCursor is the only way WinUI lets an element choose its cursor, and it's protected
		s_protectedCursor?.SetValue(element, cursor?.ControlObject as InputCursor);
	}
}
