using mui = Microsoft.UI.Xaml;

namespace Eto.WinUI.Forms;

public class DialogHandler : WinUIWindow<mui.Window, Dialog, Dialog.ICallback>, Dialog.IHandler
{
	readonly muc.Grid _buttonBar = new();
	readonly muc.Grid _contentGrid = new();
	Button? _defaultButton;
	bool _loadedCompleteRaised;
	bool _isModal;

	public DialogHandler()
	{
		_buttonBar.RowDefinitions.Add(new muc.RowDefinition());
		_buttonBar.Visibility = mux.Visibility.Collapsed;
		_buttonBar.Margin = new mux.Thickness(0);
	}

	public DialogDisplayMode DisplayMode { get; set; }

	public Button? AbortButton { get; set; }

	public Button? DefaultButton
	{
		get => _defaultButton;
		set => _defaultButton = value;
	}

	protected override void Initialize()
	{
		base.Initialize();

		Resizable = false;
		Minimizable = false;
		Maximizable = false;
		ShowInTaskbar = false;

		_contentGrid.RowDefinitions.Add(new muc.RowDefinition { Height = new mux.GridLength(1, mux.GridUnitType.Star) });
		_contentGrid.RowDefinitions.Add(new muc.RowDefinition { Height = mux.GridLength.Auto });
		Control.Closed += Control_Closed;
	}

	public override void SetContainerContent(mui.FrameworkElement content)
	{
		muc.Grid.SetRow(content, 0);
		_contentGrid.Children.Add(content);

		muc.Grid.SetRow(_buttonBar, 1);
		_contentGrid.Children.Add(_buttonBar);

		base.SetContainerContent(_contentGrid);
	}

	public void ShowModal()
	{
		ReloadButtons();
		PrepareToShow();

		var owner = Widget.Owner;
		var ownerHandler = owner?.Handler as Window.IHandler;
		if (ownerHandler != null)
			ownerHandler.Enabled = false;

		_isModal = true;
		try
		{
			Control.Activate();
			Control.DispatcherQueue.RunEventLoop();
		}
		finally
		{
			_isModal = false;
			if (ownerHandler != null && !owner.IsDisposed)
				ownerHandler.Enabled = true;

			ClearButtons();
		}
	}

	public Task ShowModalAsync()
	{
		var tcs = new TaskCompletionSource<bool>();
		Application.Instance.AsyncInvoke(() =>
		{
			if (Widget.IsDisposed)
			{
				tcs.TrySetResult(false);
				return;
			}

			ShowModal();
			tcs.TrySetResult(true);
		});
		return tcs.Task;
	}

	public void InsertDialogButton(bool positive, int index, Button item)
	{
		if (Widget.Visible)
		{
			ClearButtons();
			ReloadButtons();
		}
	}

	public void RemoveDialogButton(bool positive, int index, Button item)
	{
		if (Widget.Visible)
		{
			ClearButtons();
			ReloadButtons();
		}
	}

	void PrepareToShow()
	{
		if (!_loadedCompleteRaised)
		{
			Callback.OnLoadComplete(Widget, EventArgs.Empty);
			_loadedCompleteRaised = true;
		}

		var owner = Widget.Owner;
		if (owner != null && !owner.HasFocus)
			owner.Focus();

		Control.Activate();
	}

	void Control_Closed(object sender, mui.WindowEventArgs args)
	{
		if (_isModal)
			Control.DispatcherQueue.TryEnqueue(Control.DispatcherQueue.EnqueueEventLoopExit);

		Callback.OnClosed(Widget, EventArgs.Empty);
	}

	void ClearButtons()
	{
		_buttonBar.ColumnDefinitions.Clear();
		_buttonBar.Children.Clear();
	}

	void ReloadButtons()
	{
		ClearButtons();
		_buttonBar.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = new mux.GridLength(1, mux.GridUnitType.Star) });

		var negativeButtons = Widget.NegativeButtons;
		var positiveButtons = Widget.PositiveButtons;
		var hasButtons = negativeButtons.Count + positiveButtons.Count > 0;

		for (var i = positiveButtons.Count - 1; i >= 0; i--)
			AddButton(positiveButtons.Count - i, positiveButtons[i]);

		for (var i = 0; i < negativeButtons.Count; i++)
			AddButton(positiveButtons.Count + 1 + i, negativeButtons[i]);

		_buttonBar.Visibility = hasButtons ? mux.Visibility.Visible : mux.Visibility.Collapsed;
		_buttonBar.Margin = new mux.Thickness(0, hasButtons ? 8 : 0, 0, 0);
	}

	void AddButton(int position, Button button)
	{
		var native = (muc.Button)button.ControlObject;
		native.Margin = new mux.Thickness(6, 0, 0, 0);

		muc.Grid.SetColumn(native, position);
		_buttonBar.ColumnDefinitions.Add(new muc.ColumnDefinition { Width = mux.GridLength.Auto });
		_buttonBar.Children.Add(native);
	}
}
