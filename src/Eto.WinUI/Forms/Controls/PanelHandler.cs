
using CommunityToolkit.WinUI.Controls;
using Windows.Security.Authentication.OnlineId;

namespace Eto.WinUI.Forms.Controls;

public class EtoContentControl : muc.ContentControl
{
	public IWinUIFrameworkElement? Handler { get; set; }

	public EtoContentControl()
	{
		DefaultStyleKey = typeof(EtoContentControl);
		HorizontalContentAlignment = mux.HorizontalAlignment.Stretch;
		VerticalContentAlignment = mux.VerticalAlignment.Stretch;
	}

	protected override wf.Size MeasureOverride(wf.Size availableSize)
	{
		return Handler?.MeasureOverride(availableSize, base.MeasureOverride) ?? base.MeasureOverride(availableSize);
	}
}

public class PanelHandler : WinUIContainer<EtoContentControl, Panel, Panel.ICallback>, Panel.IHandler
{

	Control? _content;
	public Control? Content
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

	public Padding Padding
	{
		get => Control.Padding.ToEto();
		set => Control.Padding = value.ToWinUI();
	}
	public Size MinimumSize
	{
		get => Control.GetMinSize().ToEtoSize();
		set => Control.SetMinSize(value);
	}
	public override mux.FrameworkElement ContainerControl => Control;

	public override Color BackgroundColor
	{
		get => Control.Background.ToEtoColor();
		set => Control.Background = value.ToWinUIBrush();
	}

	protected override EtoContentControl CreateControl() => new EtoContentControl();
}
