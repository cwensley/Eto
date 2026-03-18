using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eto.WinUI.Drawing;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eto.WinUI.Forms.Controls;



public class DrawableHandler : WinUIBorderedContainer<CanvasControl, Drawable, Drawable.ICallback>, Drawable.IHandler
{
	private Eto.Forms.Control? _content;

	public bool SupportsCreateGraphics => false;

	public bool CanFocus
	{
		get => Control.AllowFocusOnInteraction;
		set => Control.AllowFocusOnInteraction = value;
	}

	public Eto.Forms.Control? Content
	{
		get => _content;
		set
		{
			if (_content != value)
			{
				_content = value;
				Control.Content = value.ToNative();
			}
		}
	}
	public override Padding Padding
	{
		get => Control.Padding.ToEto();
		set => Control.Padding = value.ToWinUI();
	}

	public void Create()
	{
	}

	public void Create(bool largeCanvas)
	{
	}

	protected override CanvasControl CreateControl()
	{
		var canvas = new CanvasControl();
		canvas.Name = "canvas";
		return canvas;
	}

	protected override void Initialize()
	{
		base.Initialize();
		Control.Draw += (s, e) =>
		{
			var g = new Graphics(new GraphicsHandler(e.DrawingSession));
			var args = new PaintEventArgs(g, new RectangleF(0, 0, (float)Control.ActualWidth, (float)Control.ActualHeight));
			Callback.OnPaint(Widget, args);
		};
	}

	public Graphics CreateGraphics()
	{
		throw new NotImplementedException();
	}

	public void Update(Rectangle region)
	{
		Control.Invalidate();
	}

	public override void Invalidate(bool invalidateChildren)
	{
		Control.Invalidate();
	}

	public override void Invalidate(Rectangle rect, bool invalidateChildren)
	{
		Control.Invalidate();
	}
}
