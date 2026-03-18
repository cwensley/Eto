using Microsoft.UI.Xaml.Input;

namespace Eto.WinUI.Forms.Menu;

public abstract class MenuItemBaseHandler<TControl, TWidget, TCallback> : MenuHandler<TControl, TWidget, TCallback>, MenuItem.IHandler, IWinUIMenuItemHandler
	where TControl : muc.MenuFlyoutItemBase
	where TWidget : MenuItem
	where TCallback : MenuItem.ICallback
{
	Image? _image;
	KeyboardAccelerator? _accelerator;
	Keys _shortcut;
	muc.MenuBarItem? _menuBarItem;

	protected muc.MenuBarItem? MenuBarItem => _menuBarItem;
	
	protected virtual muc.MenuBarItem CreateMenuBarItem()
	{
		WinUIMenuHelper.GetMenuTextAndAccessKey(Text, out var menuText, out var accessKey);
		var item = new muc.MenuBarItem
		{
			Title = menuText,
			AccessKey = accessKey
		};
		if (!string.IsNullOrEmpty(ToolTip))
			WinUIMenuHelper.SetToolTip(item, ToolTip);
			
		if (Shortcut != Keys.None)
		{
			_accelerator = WinUIMenuHelper.CreateKeyboardAccelerator(Shortcut);
			if (_accelerator != null)
			{
				item.KeyboardAccelerators.Add(_accelerator);
				// item.KeyboardAcceleratorPlacementTarget = Shortcut.ToShortcutString();
			}
		}
		return item;
	}

	public Size? ImageSize
	{
		get => Widget.Properties.Get<Size?>(WinUIMenuHelper.ImageSize_Key, WinUIMenuHelper.DefaultImageSize);
		set
		{
			if (Widget.Properties.TrySet(WinUIMenuHelper.ImageSize_Key, value, WinUIMenuHelper.DefaultImageSize))
				OnImageSizeChanged();
		}
	}

	protected abstract void OnImageSizeChanged();
	public Image? Image
	{
		get => _image;
		set
		{
			_image = value;
			OnImageSizeChanged();
		}
	}

	public abstract string? Text { get; set; }

	public string? ToolTip
	{
		get => muc.ToolTipService.GetToolTip(Control) as string;
		set
		{
			WinUIMenuHelper.SetToolTip(Control, value);
			if (MenuBarItem != null)
				WinUIMenuHelper.SetToolTip(MenuBarItem, value);
		}
	}

	public virtual Keys Shortcut
	{
		get => _shortcut;
		set
		{
			_shortcut = value;
			Control.KeyboardAccelerators.Clear();
			_accelerator = WinUIMenuHelper.CreateKeyboardAccelerator(value);
			if (_accelerator != null)
			{
				Control.KeyboardAccelerators.Add(_accelerator);
				SetAcceleratorText(value.ToShortcutString());
			}
			else
			{
				SetAcceleratorText(null);
			}
		}
	}
	
	protected virtual void SetAcceleratorText(string? text)
	{
		if (Control is muc.MenuFlyoutItem item)
			item.KeyboardAcceleratorTextOverride = text;
		// else if (Control is muc.MenuBarItem menuItem)
		// 	menuItem.KeyboardAcceleratorPlacementTarget = text;
		if (MenuBarItem != null)
			MenuBarItem.KeyboardAcceleratorPlacementTarget = new muc.TextBlock { Text = text };
	}

	public bool Enabled
	{
		get => Control.IsEnabled;
		set
		{
			Control.IsEnabled = value;
			if (MenuBarItem != null)
				MenuBarItem.IsEnabled = value;
		}
	}

	public bool Visible
	{
		get => Control.Visibility == mux.Visibility.Visible;
		set
		{
			Control.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
			if (MenuBarItem != null)
				MenuBarItem.Visibility = value ? mux.Visibility.Visible : mux.Visibility.Collapsed;
		}
	}

	public override void AttachEvent(string id)
	{
		switch (id)
		{
			case MenuItem.ValidateEvent:
				break;
			default:
				base.AttachEvent(id);
				break;
		}
	}


	object? IWinUIMenuItemHandler.NativeControlObject => Control;

	public void CreateFromCommand(Command command)
	{
	}

	public virtual void Validate()
	{
		Callback.OnValidate(Widget, EventArgs.Empty);
	}

	public muc.MenuBarItem GetTopLevelMenuBarItem()
	{
		return _menuBarItem ??= CreateMenuBarItem();
	}

}
