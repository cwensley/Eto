namespace Eto.WinUI.Forms.ToolBars;

public class ToolBarHandler : WidgetHandler<muc.CommandBar, Eto.Forms.ToolBar>, Eto.Forms.ToolBar.IHandler
{
	ToolBarDock _dock = ToolBarDock.Top;

	public ToolBarHandler()
	{
		Control = new muc.CommandBar
		{
			// left aligned like a classic toolbar; CommandBar puts primary commands on the right otherwise
			HorizontalAlignment = mux.HorizontalAlignment.Left,
			DefaultLabelPosition = muc.CommandBarDefaultLabelPosition.Right,
			IsTabStop = false
		};
	}

	internal event EventHandler? DockChanged;

	public void AddButton(ToolItem button, int index)
	{
		Control.PrimaryCommands.Insert(index, (muc.ICommandBarElement)button.ControlObject);
	}

	public void RemoveButton(ToolItem button, int index)
	{
		Control.PrimaryCommands.Remove((muc.ICommandBarElement)button.ControlObject);
	}

	public void Clear()
	{
		Control.PrimaryCommands.Clear();
	}

	// WinUI only shows labels underneath while the bar is expanded, so Right is the default
	public ToolBarTextAlign TextAlign
	{
		get => Control.DefaultLabelPosition == muc.CommandBarDefaultLabelPosition.Bottom ? ToolBarTextAlign.Underneath : ToolBarTextAlign.Right;
		set => Control.DefaultLabelPosition = value == ToolBarTextAlign.Underneath ? muc.CommandBarDefaultLabelPosition.Bottom : muc.CommandBarDefaultLabelPosition.Right;
	}

	public ToolBarDock Dock
	{
		get => _dock;
		set
		{
			if (_dock == value)
				return;
			_dock = value;
			DockChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
