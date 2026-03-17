namespace Eto.WinUI.Drawing;

sealed class PenData
{
	public required Brush Brush { get; init; }

	public float Thickness { get; set; }

	public PenLineJoin LineJoin { get; set; } = PenLineJoin.Miter;

	public PenLineCap LineCap { get; set; } = PenLineCap.Butt;

	public float MiterLimit { get; set; } = 10f;
}

public class PenHandler : Pen.IHandler
{
	static PenData GetPenData(Pen widget) => (PenData)widget.ControlObject;

	public object Create(Brush brush, float thickness)
	{
		return new PenData
		{
			Brush = brush,
			Thickness = thickness
		};
	}

	public Brush GetBrush(Pen widget) => GetPenData(widget).Brush;

	public float GetThickness(Pen widget) => GetPenData(widget).Thickness;

	public void SetThickness(Pen widget, float thickness) => GetPenData(widget).Thickness = thickness;

	public PenLineJoin GetLineJoin(Pen widget) => GetPenData(widget).LineJoin;

	public void SetLineJoin(Pen widget, PenLineJoin lineJoin) => GetPenData(widget).LineJoin = lineJoin;

	public PenLineCap GetLineCap(Pen widget) => GetPenData(widget).LineCap;

	public void SetLineCap(Pen widget, PenLineCap lineCap) => GetPenData(widget).LineCap = lineCap;

	public float GetMiterLimit(Pen widget) => GetPenData(widget).MiterLimit;

	public void SetMiterLimit(Pen widget, float miterLimit) => GetPenData(widget).MiterLimit = miterLimit;

	public void SetDashStyle(Pen widget, DashStyle dashStyle)
	{
		// DashStyle state is stored on the Eto Pen itself and read directly by GraphicsHandler.
	}
}
