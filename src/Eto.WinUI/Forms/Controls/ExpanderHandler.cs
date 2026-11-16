namespace Eto.WinUI.Forms.Controls;

public class ExpanderHandler : WinUIPanel<muc.Expander, Expander, Expander.ICallback>, Expander.IHandler
{
	readonly EtoContentControl _container;
	Control? _header;

	public ExpanderHandler()
	{
		_container = new EtoContentControl { Handler = this };
	}

	protected override muc.Expander CreateControl() => new muc.Expander
	{
		HorizontalAlignment = mux.HorizontalAlignment.Stretch,
		VerticalAlignment = mux.VerticalAlignment.Stretch,
		HorizontalContentAlignment = mux.HorizontalAlignment.Stretch,
		VerticalContentAlignment = mux.VerticalAlignment.Stretch
	};

	protected override void Initialize()
	{
		_container.Content = Control;
		base.Initialize();
		// update scrollables, etc with the new size
		Control.Expanding += (sender, e) => ContainerControl.InvalidateMeasure();
		Control.Collapsed += (sender, e) => ContainerControl.InvalidateMeasure();
	}

	public override mux.FrameworkElement ContainerControl => _container;

	public override void SetContainerContent(mux.FrameworkElement content)
	{
		// sizing is applied by the outer container, not the content
		if (content is EtoContentControl contentControl)
			contentControl.Handler = null;
		Control.Content = content;
	}

	public bool Expanded
	{
		get => Control.IsExpanded;
		set => Control.IsExpanded = value;
	}

	public Control? Header
	{
		get => _header;
		set
		{
			_header = value;
			Control.Header = value.ToNative();
		}
	}

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case Expander.ExpandedChangedEvent:
				Control.Expanding += (sender, e) => Callback.OnExpandedChanged(Widget, EventArgs.Empty);
				Control.Collapsed += (sender, e) => Callback.OnExpandedChanged(Widget, EventArgs.Empty);
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}
}
