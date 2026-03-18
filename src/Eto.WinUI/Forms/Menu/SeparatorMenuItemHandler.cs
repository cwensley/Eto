namespace Eto.WinUI.Forms.Menu;

public class SeparatorMenuItemHandler : MenuItemBaseHandler<muc.MenuFlyoutSeparator, SeparatorMenuItem, SeparatorMenuItem.ICallback>, SeparatorMenuItem.IHandler
{
	public SeparatorMenuItemHandler()
	{
		Control = new muc.MenuFlyoutSeparator();
	}

	public override string Text
	{
		get => null;
		set => throw new NotSupportedException();
	}

	protected override void OnImageSizeChanged()
	{
		
	}

}

