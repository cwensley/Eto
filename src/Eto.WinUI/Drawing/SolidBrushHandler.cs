using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace Eto.WinUI.Drawing;

public class SolidBrushHandler : SolidBrush.IHandler
{
	static SolidColorBrush Get(SolidBrush widget) => (SolidColorBrush)widget.ControlObject;

	public Color GetColor(SolidBrush widget)
	{
		return Get(widget).Color.ToEto();
	}

	public void SetColor(SolidBrush widget, Color color)
	{
		Get(widget).Color = color.ToWinUI();
	}

	public object Create(Color color)
	{
		return new SolidColorBrush(color.ToWinUI());
	}
}
