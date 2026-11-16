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

	public override Size ClientSize
	{
		get => Control.IsLoaded ? new Size((int)Math.Round(Control.ActualWidth), (int)Math.Round(Control.ActualHeight)) : base.ClientSize;
		set => base.ClientSize = value;
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
			// disposing closes any clip layer, which must happen before the session ends
			var bounds = new RectangleF(0, 0, (float)Control.ActualWidth, (float)Control.ActualHeight);
			using var g = new Graphics(new GraphicsHandler(e.DrawingSession, bounds));
			var args = new PaintEventArgs(g, bounds);
			Callback.OnPaint(Widget, args);
		};
	}

	public Graphics CreateGraphics()
	{
		// Win2D's CanvasControl only hands out a drawing session during its Draw event
		throw new NotSupportedException("Drawable.CreateGraphics is not supported on WinUI, draw in the Paint event instead");
	}

	public void Update(Rectangle region)
	{
		Control.Invalidate();
	}

	// no IME text input on WinUI drawables yet, so there is never a composition in progress
	public void CancelTextComposition()
	{
	}

	public void CommitTextComposition()
	{
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
