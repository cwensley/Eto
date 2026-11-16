using Eto.WinUI.Forms.Menu;

namespace Eto.WinUI.Forms.ToolBars;

public static class ToolItemHandler
{
	public static Size? DefaultImageSize = new Size(16, 16);
	internal static readonly object ImageSize_Key = new();
}

public abstract class ToolItemHandler<TControl, TWidget> : WidgetHandler<TControl, TWidget>, ToolItem.IHandler
	where TControl : muc.Control, muc.ICommandBarElement
	where TWidget : ToolItem
{
	Image? _image;
	string? _text;

	public virtual string? Text
	{
		get => _text;
		set
		{
			_text = value;
			WinUIMenuHelper.GetMenuTextAndAccessKey(value, out var label, out var accessKey);
			Control.AccessKey = accessKey;
			switch (Control)
			{
				case muc.AppBarButton button:
					button.Label = label;
					// otherwise an icon-only button reserves room for an empty label
					button.LabelPosition = string.IsNullOrEmpty(label) ? muc.CommandBarLabelPosition.Collapsed : muc.CommandBarLabelPosition.Default;
					break;
				case muc.AppBarToggleButton toggle:
					toggle.Label = label;
					toggle.LabelPosition = string.IsNullOrEmpty(label) ? muc.CommandBarLabelPosition.Collapsed : muc.CommandBarLabelPosition.Default;
					break;
			}
			mux.Automation.AutomationProperties.SetName(Control, label);
		}
	}

	public virtual string? ToolTip
	{
		get => muc.ToolTipService.GetToolTip(Control) as string;
		set => WinUIMenuHelper.SetToolTip(Control, value);
	}

	public virtual Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			OnImageSizeChanged();
		}
	}

	public virtual bool Enabled
	{
		get => Control.IsEnabled;
		set => Control.IsEnabled = value;
	}

	public bool Visible
	{
		get => Control.Visibility == mux.Visibility.Visible;
		set => Control.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
	}

	public Size? ImageSize
	{
		get => Widget.Properties.Get<Size?>(ToolItemHandler.ImageSize_Key, ToolItemHandler.DefaultImageSize);
		set
		{
			if (Widget.Properties.TrySet(ToolItemHandler.ImageSize_Key, value, ToolItemHandler.DefaultImageSize))
				OnImageSizeChanged();
		}
	}

	protected virtual void OnImageSizeChanged()
	{
		var icon = WinUIMenuHelper.CreateIcon(_image, ImageSize);
		switch (Control)
		{
			case muc.AppBarButton button:
				button.Icon = icon;
				break;
			case muc.AppBarToggleButton toggle:
				toggle.Icon = icon;
				break;
		}
	}

	public virtual void CreateFromCommand(Command command)
	{
	}

	public virtual void OnLoad(EventArgs e)
	{
	}

	public virtual void OnPreLoad(EventArgs e)
	{
	}

	public virtual void OnUnLoad(EventArgs e)
	{
	}
}
