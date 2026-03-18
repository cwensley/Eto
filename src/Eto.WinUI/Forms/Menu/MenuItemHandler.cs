using System.Text;
using System.Text.RegularExpressions;

namespace Eto.WinUI.Forms.Menu;

public class MenuItemHandler<TControl, TWidget, TCallback> : MenuItemBaseHandler<TControl, TWidget, TCallback>, MenuItem.IHandler, IWinUIMenuItemHandler
	where TControl : muc.MenuFlyoutItem
	where TWidget : MenuItem
	where TCallback : MenuItem.ICallback
{
	string? _text;

	protected override void Initialize()
	{
		base.Initialize();
		Control.Click += HandleClick;
	}

	void HandleClick(object sender, mux.RoutedEventArgs e)
	{
		OnClick();
	}

	protected virtual void OnClick()
	{
		Callback.OnClick(Widget, EventArgs.Empty);
	}

	protected override void OnImageSizeChanged()
	{
		Control.Icon = WinUIMenuHelper.CreateIcon(Image, ImageSize);
	}

	public override string? Text
	{
		get => _text;
		set
		{
			_text = value;
			WinUIMenuHelper.GetMenuTextAndAccessKey(value, out var menuText, out var accessKey);
			Control.Text = menuText;
			Control.AccessKey = accessKey;
			if (MenuBarItem != null)
			{
				MenuBarItem.Title = menuText;
				MenuBarItem.AccessKey = accessKey;
			}
		}
	}

}
