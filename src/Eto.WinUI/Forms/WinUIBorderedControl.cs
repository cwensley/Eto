using Eto.WinUI.Forms.Controls;

namespace Eto.WinUI.Forms;

public abstract class WinUIBorderedControl<TControl, TWidget, TCallback> : WinUIFrameworkElement<TControl, TWidget, TCallback>
	where TControl : mux.UIElement
	where TWidget : Control
	where TCallback : Control.ICallback
{
	readonly EtoContentControl _border = new();

	public virtual Padding Padding
	{
		get => _border.Padding.ToEto();
		set => _border.Padding = value.ToWinUI();
	}

	public virtual Size MinimumSize
	{
		get => _border.GetMinSize().ToEtoSize();
		set => _border.SetMinSize(value);

	}
	public sealed override mux.FrameworkElement ContainerControl => _border;

	public override Color BackgroundColor
	{
		get => _border.Background.ToEtoColor();
		set => _border.Background = value.ToWinUIBrush();
	}

	protected override void Initialize()
	{
		base.Initialize();
		_border.Handler = this;
		_border.Content = Control;
	}

}
