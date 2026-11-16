namespace Eto.WinUI.Forms.ToolBars;

public class SeparatorToolItemHandler : ToolItemHandler<muc.AppBarSeparator, SeparatorToolItem>, SeparatorToolItem.IHandler
{
	SeparatorToolItemType _type;

	public SeparatorToolItemHandler()
	{
		Control = new muc.AppBarSeparator { IsTabStop = false };
		Type = SeparatorToolItemType.Divider;
	}

	public SeparatorToolItemType Type
	{
		get => _type;
		set
		{
			_type = value;
			switch (value)
			{
				case SeparatorToolItemType.Divider:
					Control.ClearValue(muc.Control.ForegroundProperty);
					Control.ClearValue(mux.FrameworkElement.WidthProperty);
					break;
				case SeparatorToolItemType.Space:
				case SeparatorToolItemType.FlexibleSpace:
					// CommandBar can't stretch an item, so a flexible space is a fixed gap like on Wpf
					Control.Foreground = new muxm.SolidColorBrush(mu.Colors.Transparent);
					Control.Width = 16;
					break;
				default:
					throw new NotSupportedException();
			}
		}
	}

	public override string? Text
	{
		get => null;
		set => throw new NotSupportedException();
	}

	public override string? ToolTip
	{
		get => null;
		set => throw new NotSupportedException();
	}

	public override Image? Image
	{
		get => null;
		set => throw new NotSupportedException();
	}

	public override bool Enabled
	{
		get => false;
		set => throw new NotSupportedException();
	}
}
