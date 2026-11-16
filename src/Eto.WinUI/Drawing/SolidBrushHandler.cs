namespace Eto.WinUI.Drawing;

public class SolidBrushHandler : SolidBrush.IHandler
{
	static SolidBrushData Get(SolidBrush widget) => (SolidBrushData)widget.ControlObject;

	public Color GetColor(SolidBrush widget) => Get(widget).Color;

	public void SetColor(SolidBrush widget, Color color) => Get(widget).Color = color;

	public object Create(Color color) => new SolidBrushData { Color = color };
}
