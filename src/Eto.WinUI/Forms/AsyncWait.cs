using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Eto.WinUI.Forms;

/// <summary>
/// Waits for WinRT async operations and modal dialogs from Eto's synchronous APIs.
/// </summary>
/// <remarks>
/// Clipboard and drag data come from OLE, which calls back into the UI thread, so blocking
/// without pumping messages would deadlock.
/// </remarks>
static class AsyncWait
{
	public static T Wait<T>(wf.IAsyncOperation<T> operation)
	{
		var task = operation.AsTask();
		PumpUntil(task);
		return task.GetAwaiter().GetResult();
	}

	public static void Wait(wf.IAsyncAction action)
	{
		var task = action.AsTask();
		PumpUntil(task);
		task.GetAwaiter().GetResult();
	}

	static void PumpUntil(Task task)
	{
		while (!task.IsCompleted)
		{
			MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, 10, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
			while (!task.IsCompleted && PeekMessage(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
			{
				if (msg.message == WM_QUIT)
				{
					// leave it for the real message loop
					PostQuitMessage((int)msg.wParam);
					task.Wait();
					return;
				}
				TranslateMessage(ref msg);
				DispatchMessage(ref msg);
			}
		}
	}

	/// <summary>
	/// Runs a nested message loop until <paramref name="isDone"/> returns true, as modal dialogs do.
	/// </summary>
	/// <remarks>
	/// DispatcherQueue.RunEventLoop can't be used, as exiting it also ends the application's own loop.
	/// Call <see cref="Wake"/> after making it done so the loop notices straight away.
	/// </remarks>
	public static void RunModalLoop(Func<bool> isDone)
	{
		while (!isDone())
		{
			var result = GetMessage(out var msg, IntPtr.Zero, 0, 0);
			if (result == 0)
			{
				// leave it for the real message loop
				PostQuitMessage((int)msg.wParam);
				return;
			}
			if (result < 0)
				return;
			TranslateMessage(ref msg);
			DispatchMessage(ref msg);
		}
	}

	/// <summary>Wakes up a modal loop on the current thread so it can check whether it's done.</summary>
	public static void Wake() => PostThreadMessage(GetCurrentThreadId(), WM_NULL, IntPtr.Zero, IntPtr.Zero);

	const uint WM_NULL = 0x0000;
	const uint QS_ALLINPUT = 0x04FF;
	const uint MWMO_INPUTAVAILABLE = 0x0004;
	const uint PM_REMOVE = 0x0001;
	const uint WM_QUIT = 0x0012;

	[StructLayout(LayoutKind.Sequential)]
	struct MSG
	{
		public IntPtr hwnd;
		public uint message;
		public IntPtr wParam;
		public IntPtr lParam;
		public uint time;
		public int ptX;
		public int ptY;
	}

	[DllImport("user32.dll")]
	static extern uint MsgWaitForMultipleObjectsEx(uint nCount, IntPtr pHandles, uint dwMilliseconds, uint dwWakeMask, uint dwFlags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	static extern IntPtr DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	static extern void PostQuitMessage(int nExitCode);

	[DllImport("user32.dll")]
	static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll")]
	static extern uint GetCurrentThreadId();
}
