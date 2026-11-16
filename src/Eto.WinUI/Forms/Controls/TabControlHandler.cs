namespace Eto.WinUI.Forms.Controls;

public class TabControlHandler : WinUIContainer<muc.TabView, TabControl, TabControl.ICallback>, TabControl.IHandler
{
	readonly EtoContentControl _container;
	int _lastSelectedIndex = -1;
	int _suppressSelectedIndexChanged;
	DockPosition _tabPosition = DockPosition.Top;

	public TabControlHandler()
	{
		_container = new EtoContentControl { Handler = this };
	}

	protected override muc.TabView CreateControl() => new muc.TabView
	{
		IsAddTabButtonVisible = false,
		CanReorderTabs = false,
		CanDragTabs = false,
		TabWidthMode = muc.TabViewWidthMode.SizeToContent,
		CloseButtonOverlayMode = muc.TabViewCloseButtonOverlayMode.Auto,
		HorizontalAlignment = mux.HorizontalAlignment.Stretch,
		VerticalAlignment = mux.VerticalAlignment.Stretch
	};

	protected override void Initialize()
	{
		base.Initialize();
		_container.Content = Control;
		// the native SelectionChanged only fires once templated, so watch the property instead
		Control.RegisterPropertyChangedCallback(muc.TabView.SelectedIndexProperty, (sender, dp) => HandleSelectedIndexChanged());
		Control.SelectionChanged += (sender, e) => HandleSelectedIndexChanged();
		Control.Loaded += (sender, e) => HandleSelectedIndexChanged();
	}

	void HandleSelectedIndexChanged()
	{
		var index = Control.SelectedIndex;
		if (index >= 0 && index < Widget.Pages.Count && !Widget.Pages[index].Visible)
		{
			// TabView selects the first tab when loaded, even when it is hidden
			var pages = Widget.Pages;
			var next = pages.Skip(index + 1).FirstOrDefault(r => r.Visible) ?? pages.Take(index).LastOrDefault(r => r.Visible);
			Control.SelectedIndex = next != null ? pages.IndexOf(next) : -1;
			return;
		}
		if (index == _lastSelectedIndex)
			return;
		_lastSelectedIndex = index;
		if (_suppressSelectedIndexChanged == 0)
			Callback.OnSelectedIndexChanged(Widget, EventArgs.Empty);
	}

	public override mux.FrameworkElement ContainerControl => _container;

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public int SelectedIndex
	{
		get => Control.SelectedIndex;
		set
		{
			// a collapsed tab can still be selected, showing a page without a tab
			if (value >= 0 && value < Widget.Pages.Count && !Widget.Pages[value].Visible)
				return;
			Control.SelectedIndex = value;
		}
	}

	// TabView only supports tabs along the top
	public DockPosition TabPosition
	{
		get => _tabPosition;
		set => _tabPosition = value;
	}

	public void InsertTab(int index, TabPage page)
	{
		var item = page.GetContainerControl();
		_suppressSelectedIndexChanged++;
		if (index == -1 || index >= Control.TabItems.Count)
			Control.TabItems.Add(item);
		else
			Control.TabItems.Insert(index, item);
		_suppressSelectedIndexChanged--;

		if (Control.SelectedIndex == -1 && page.Visible)
			Control.SelectedItem = item;
		else
			_lastSelectedIndex = Control.SelectedIndex;
	}

	internal void SetPageVisible(TabPage page, bool visible)
	{
		var item = page.GetContainerControl();
		if (visible)
		{
			if (Control.SelectedIndex == -1)
				Control.SelectedItem = item;
			return;
		}
		if (!ReferenceEquals(Control.SelectedItem, item))
			return;
		// prefer the next visible page, like the other platforms
		var index = Widget.Pages.IndexOf(page);
		var next = Widget.Pages.Skip(index + 1).FirstOrDefault(r => r.Visible)
			?? Widget.Pages.Take(index).LastOrDefault(r => r.Visible);
		if (next != null)
			Control.SelectedItem = next.GetContainerControl();
		else
			Control.SelectedIndex = -1;
	}

	public void ClearTabs()
	{
		_suppressSelectedIndexChanged++;
		Control.TabItems.Clear();
		_lastSelectedIndex = Control.SelectedIndex;
		_suppressSelectedIndexChanged--;
	}

	public void RemoveTab(int index, TabPage page)
	{
		var isSelected = Control.SelectedIndex == index;
		_suppressSelectedIndexChanged++;
		Control.TabItems.Remove(page.GetContainerControl());
		if (isSelected && Control.SelectedIndex == -1 && Control.TabItems.Count > 0)
		{
			var next = Widget.Pages.Skip(index).FirstOrDefault(r => r.Visible && !ReferenceEquals(r, page))
				?? Widget.Pages.Take(index).LastOrDefault(r => r.Visible);
			if (next != null)
				Control.SelectedItem = next.GetContainerControl();
		}
		_lastSelectedIndex = Control.SelectedIndex;
		_suppressSelectedIndexChanged--;
		if (Widget.Loaded && isSelected)
			Callback.OnSelectedIndexChanged(Widget, EventArgs.Empty);
	}
}
