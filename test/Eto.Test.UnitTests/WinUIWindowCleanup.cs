#if WINUI
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions;

namespace Eto.Test.UnitTests;

/// <summary>
/// Closes the windows each test leaves open once its result is reported.
/// </summary>
/// <remarks>
/// While the WinUI backend is incomplete, many tests time out before they can close their form, and every
/// window left open holds native resources until the process runs out (E_OUTOFMEMORY creating the next one)
/// and the whole run dies. Windows are matched to a test by creation time, as the next test may already be
/// running by the time its result arrives here.
/// </remarks>
sealed class WinUIWindowCleanup : IDataConsumer
{
	readonly object _lock = new();
	readonly List<(Window Window, DateTimeOffset Created)> _windows = new();

	public WinUIWindowCleanup(Platform platform)
	{
		platform.WidgetCreated += (sender, e) =>
		{
			if (e.Instance is Window window)
			{
				lock (_lock)
					_windows.Add((window, DateTimeOffset.Now));
			}
		};
	}

	public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

	public string Uid => nameof(WinUIWindowCleanup);

	public string Version => "1.0.0";

	public string DisplayName => "WinUI window cleanup";

	public string Description => "Closes windows left open by finished tests.";

	public Task<bool> IsEnabledAsync() => Task.FromResult(true);

	public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
	{
		if (value is not TestNodeUpdateMessage message)
			return Task.CompletedTask;

		var state = message.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
		if (state is null or InProgressTestNodeStateProperty or DiscoveredTestNodeStateProperty)
			return Task.CompletedTask;

		var finished = message.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.EndTime ?? DateTimeOffset.Now;

		List<Window> toClose;
		lock (_lock)
		{
			toClose = _windows.Where(r => r.Created <= finished).Select(r => r.Window).ToList();
			_windows.RemoveAll(r => r.Created <= finished);
		}

		if (toClose.Count > 0)
		{
			Application.Instance.AsyncInvoke(() =>
			{
				foreach (var window in toClose)
				{
					try
					{
						// through Eto, which skips windows already closed; closing a closed native window crashes
						if (!window.IsDisposed)
							window.Close();
					}
					catch (Exception ex)
					{
						Console.Error.WriteLine($"Error closing window left open by a test: {ex.Message}");
					}
				}
				// a closed window's native resources are only freed once its WinRT references are collected
				Task.Run(GC.Collect);
			});
		}
		return Task.CompletedTask;
	}
}
#endif
