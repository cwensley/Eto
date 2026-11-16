namespace Eto.WinUI.Forms;

/// <summary>
/// Shows progress on the taskbar button of the main window.
/// </summary>
public class TaskbarHandler : Taskbar.IHandler
{
	const int TBPF_NOPROGRESS = 0x0;
	const int TBPF_INDETERMINATE = 0x1;
	const int TBPF_NORMAL = 0x2;
	const int TBPF_ERROR = 0x4;
	const int TBPF_PAUSED = 0x8;

	static ShellNative.ITaskbarList3? s_taskbar;

	static ShellNative.ITaskbarList3 TaskbarList
	{
		get
		{
			if (s_taskbar == null)
			{
				s_taskbar = (ShellNative.ITaskbarList3)new ShellNative.TaskbarList();
				s_taskbar.HrInit();
			}
			return s_taskbar;
		}
	}

	static IntPtr GetWindowHandle()
	{
		var app = Application.Instance;
		var window = app?.MainForm ?? app?.Windows.FirstOrDefault(w => w.Visible);
		var handle = window?.NativeHandle ?? IntPtr.Zero;
		if (handle == IntPtr.Zero)
		{
			using var process = Process.GetCurrentProcess();
			handle = process.MainWindowHandle;
		}
		return handle;
	}

	public void SetProgress(TaskbarProgressState state, float progress)
	{
		var hwnd = GetWindowHandle();
		if (hwnd == IntPtr.Zero)
			return;

		var flags = state switch
		{
			TaskbarProgressState.Indeterminate => TBPF_INDETERMINATE,
			TaskbarProgressState.Progress => TBPF_NORMAL,
			TaskbarProgressState.Error => TBPF_ERROR,
			TaskbarProgressState.Paused => TBPF_PAUSED,
			_ => TBPF_NOPROGRESS
		};

		var taskbar = TaskbarList;
		taskbar.SetProgressState(hwnd, flags);
		// setting a value would switch an indeterminate bar back to normal
		if (flags != TBPF_NOPROGRESS && flags != TBPF_INDETERMINATE)
			taskbar.SetProgressValue(hwnd, (ulong)(Math.Max(0f, Math.Min(1f, progress)) * 1000), 1000);
	}
}
