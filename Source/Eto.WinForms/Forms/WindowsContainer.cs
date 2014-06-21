using System;
using sd = System.Drawing;
using swf = System.Windows.Forms;
using Eto.Forms;
using Eto.Drawing;

namespace Eto.WinForms
{

	public abstract class WindowsContainer<TControl, TWidget, TCallback> : WindowsControl<TControl, TWidget, TCallback>, Container.IHandler
		where TControl : swf.Control
		where TWidget : Container
		where TCallback : Control.ICallback
	{
		Size minimumSize;

		protected WindowsContainer()
		{
			EnableRedrawDuringSuspend = false;
		}

		public bool RecurseToChildren { get { return true; } }

		public override Size? DefaultSize
		{
			get
			{
				var container = ContainerControl;
				var min = container.MinimumSize;
				if (min != sd.Size.Empty)
				{
					var parent = container.Parent;
					if (parent != null)
						parent.SuspendLayout();
					container.MinimumSize = sd.Size.Empty;
					var size = container.GetPreferredSize(Size.MaxValue.ToSD()).ToEto();
					container.MinimumSize = min;
					if (parent != null)
						parent.ResumeLayout();
					return size;
				}
				else
					return ContainerControl.GetPreferredSize(Size.MaxValue.ToSD()).ToEto();
			}
		}

		public bool EnableRedrawDuringSuspend { get; set; }

		public override Size GetPreferredSize(Size availableSize)
		{
			var size = base.GetPreferredSize(availableSize);
			return Size.Max(minimumSize, size);
		}

		public Size MinimumSize
		{
			get { return minimumSize; }
			set
			{
				minimumSize = value;
				SetMinimumSize();
			}
		}

		bool restoreRedraw;

		public override void SuspendLayout()
		{
			base.SuspendLayout();
			if (!EnableRedrawDuringSuspend && Control.IsHandleCreated && EtoEnvironment.Platform.IsWindows)
			{
				restoreRedraw = (int)Win32.SendMessage(Control.Handle, Win32.WM.SETREDRAW, IntPtr.Zero, IntPtr.Zero) == 0;
			}
		}

		public override void ResumeLayout()
		{
			base.ResumeLayout();
			if (restoreRedraw)
			{
				Win32.SendMessage(Control.Handle, Win32.WM.SETREDRAW, new IntPtr(1), IntPtr.Zero);
				Control.Refresh();
				restoreRedraw = false;
			}
		}
	}
}
