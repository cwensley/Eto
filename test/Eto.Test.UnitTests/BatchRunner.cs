#if NET
using System.Diagnostics;
using System.Text;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Eto.Test.UnitTests;

/// <summary>
/// Runs the tests over as many processes as it takes, starting a new one after every <c>--batch-size=N</c>
/// tests or whenever one dies, and carrying on with the tests that haven't run yet.
/// </summary>
/// <remarks>
/// For backends that can't get through the whole suite in one process: WinUI leaks GDI objects for every
/// window it shows with an extended title bar, so a full run hits the per-process GDI quota and dies. A test
/// that crashes its process twice is reported as crashing and skipped, so the rest still run.
/// </remarks>
static class BatchRunner
{
	public const string BatchSizeOption = "--batch-size=";
	public const string BatchTimeoutOption = "--batch-timeout=";
	const string ProgressOption = Program.ProgressOption;
	const string StopAfterOption = Program.StopAfterOption;
	const string FilterFileOption = Program.FilterFileOption;

	// testing platform exit codes that mean the run got to the end: success, failed tests, no tests
	static readonly int[] CompletedExitCodes = { 0, 2, 8 };

	public static int Run(int batchSize, TimeSpan batchTimeout, List<string> args)
	{
		if (args.Contains("--server"))
		{
			// dotnet test talks to the test process over a pipe that the batch processes can't share
			Console.Error.WriteLine($"{BatchSizeOption}N can't be used through dotnet test, run the test executable directly or with dotnet run");
			return 1;
		}
		var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Can't find the test executable");
		var userFilter = GetOptionValue(args, "--filter");
		var resultsDirectory = GetOptionValue(args, "--results-directory") ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
		var workDirectory = Path.Combine(resultsDirectory, $"batches-{DateTime.Now:yyyyMMdd-HHmmss}");
		Directory.CreateDirectory(workDirectory);
		var baseArgs = RemoveOptions(args, "--filter", "--results-directory")
			.Where(a => !a.StartsWith("--report-trx", StringComparison.Ordinal))
			.ToList();

		var outcomes = new Dictionary<string, string>();
		var crashCounts = new Dictionary<string, int>();
		var crashed = new List<string>();
		Console.WriteLine($"Running tests in batches of {batchSize} per process, progress in {workDirectory}");

		for (var batch = 1; ; batch++)
		{
			// the exclusions can be far too long for a command line (and contain characters a response file
			// can't pass through), so the filter goes in a file
			var filterFile = Path.Combine(workDirectory, $"batch-{batch}.filter.txt");
			File.WriteAllText(filterFile, CreateFilter(userFilter, outcomes.Keys.Concat(crashed)) ?? string.Empty);

			var progressFile = Path.Combine(workDirectory, $"batch-{batch}.progress.txt");
			var batchArgs = new List<string>(baseArgs)
			{
				FilterFileOption + filterFile,
				"--results-directory", workDirectory,
				ProgressOption + progressFile,
				StopAfterOption + batchSize
			};
			Console.WriteLine();
			Console.WriteLine($"=== Batch {batch}: {outcomes.Count} tests done so far");
			var exitCode = RunProcess(exe, batchArgs, batchTimeout);

			var (started, finished) = ReadProgress(progressFile);
			var newCount = finished.Keys.Count(uid => !outcomes.ContainsKey(uid));
			foreach (var (uid, outcome) in finished)
				outcomes[uid] = outcome;

			if (CompletedExitCodes.Contains(exitCode))
				break;
			if (exitCode == Program.StopAfterExitCode)
			{
				// guards against looping forever should the filter fail to leave out tests that already ran
				if (newCount == 0)
				{
					Console.WriteLine("=== Batch only re-ran tests that already ran, giving up");
					break;
				}
				continue;
			}

			var inFlight = started.Where(uid => !finished.ContainsKey(uid)).ToList();
			Console.WriteLine();
			Console.WriteLine($"=== Batch {batch} ended abnormally (exit code 0x{exitCode:X8}) after {finished.Count} tests");
			if (inFlight.Count == 0)
			{
				if (finished.Count == 0)
				{
					Console.WriteLine("=== No test was running and none finished, giving up");
					break;
				}
				continue;
			}
			foreach (var uid in inFlight)
			{
				// the process can die from damage earlier tests did, so only blame a test that kills it twice
				crashCounts[uid] = crashCounts.GetValueOrDefault(uid) + 1;
				if (crashCounts[uid] >= 2 || finished.Count == 0)
				{
					Console.WriteLine($"=== Crashing test, skipping it: {uid}");
					crashed.Add(uid);
				}
			}
		}

		return Summarize(outcomes, crashed);
	}

	static int Summarize(Dictionary<string, string> outcomes, List<string> crashed)
	{
		int Count(string outcome) => outcomes.Values.Count(o => o == outcome);
		var failed = Count("failed");
		Console.WriteLine();
		Console.WriteLine("Batched test run summary:");
		Console.WriteLine($"  total: {outcomes.Count + crashed.Count}");
		Console.WriteLine($"  failed: {failed}");
		Console.WriteLine($"  succeeded: {Count("passed")}");
		Console.WriteLine($"  skipped: {Count("skipped")}");
		Console.WriteLine($"  crashed: {crashed.Count}");
		foreach (var uid in crashed)
			Console.WriteLine($"    {uid}");
		if (crashed.Count > 0)
			return 3;
		return failed > 0 ? 2 : outcomes.Count == 0 ? 8 : 0;
	}

	/// <summary>
	/// Combines the caller's filter with one that leaves out the tests that have already run.
	/// </summary>
	static string? CreateFilter(string? userFilter, IEnumerable<string> exclude)
	{
		var sb = new StringBuilder();
		if (!string.IsNullOrEmpty(userFilter))
			sb.Append('(').Append(userFilter).Append(')');
		foreach (var uid in exclude)
		{
			if (sb.Length > 0)
				sb.Append('&');
			sb.Append("FullyQualifiedName!=").Append(Escape(uid));
		}
		return sb.Length > 0 ? sb.ToString() : null;
	}

	// characters with a meaning in a filter expression need a backslash
	static string Escape(string value)
	{
		var sb = new StringBuilder();
		foreach (var c in value)
		{
			if (c is '\\' or '(' or ')' or '&' or '|' or '=' or '!' or '~')
				sb.Append('\\');
			sb.Append(c);
		}
		return sb.ToString();
	}

	static (HashSet<string> Started, Dictionary<string, string> Finished) ReadProgress(string file)
	{
		var started = new HashSet<string>();
		var finished = new Dictionary<string, string>();
		if (File.Exists(file))
		{
			foreach (var line in File.ReadAllLines(file))
			{
				var parts = line.Split('\t', 2);
				if (parts.Length != 2)
					continue;
				if (parts[0] == "started")
					started.Add(parts[1]);
				else
					finished[parts[1]] = parts[0];
			}
		}
		return (started, finished);
	}

	static int RunProcess(string exe, List<string> args, TimeSpan timeout)
	{
		var info = new ProcessStartInfo(exe) { UseShellExecute = false };
		foreach (var arg in args)
			info.ArgumentList.Add(arg);
		using var process = Process.Start(info)!;
		if (!process.WaitForExit(timeout))
		{
			Console.WriteLine($"=== Timed out after {timeout}, killing the test process");
			process.Kill(entireProcessTree: true);
			process.WaitForExit();
			return -1;
		}
		return process.ExitCode;
	}

	static string? GetOptionValue(List<string> args, string option)
	{
		for (int i = 0; i < args.Count; i++)
		{
			if (args[i] == option && i + 1 < args.Count)
				return args[i + 1];
			if (args[i].StartsWith(option + "=", StringComparison.Ordinal))
				return args[i].Substring(option.Length + 1);
		}
		return null;
	}

	static IEnumerable<string> RemoveOptions(List<string> args, params string[] options)
	{
		for (int i = 0; i < args.Count; i++)
		{
			if (options.Contains(args[i]))
			{
				i++; // and its value
				continue;
			}
			if (options.Any(o => args[i].StartsWith(o + "=", StringComparison.Ordinal)))
				continue;
			yield return args[i];
		}
	}

}

/// <summary>
/// Records each test as it starts and finishes, so <see cref="BatchRunner"/> knows what has run and which
/// test was running if the process dies. Also ends the process after a given number of tests.
/// </summary>
sealed class TestProgressWriter : IDataConsumer
{
	readonly StreamWriter _writer;
	readonly int _stopAfter;
	int _finished;

	public TestProgressWriter(string path, int stopAfter)
	{
		_writer = new StreamWriter(path, append: true) { AutoFlush = true };
		_stopAfter = stopAfter;
	}

	public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

	public string Uid => nameof(TestProgressWriter);

	public string Version => "1.0.0";

	public string DisplayName => "Test progress writer";

	public string Description => "Records test progress for batched runs.";

	public Task<bool> IsEnabledAsync() => Task.FromResult(true);

	public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
	{
		if (value is not TestNodeUpdateMessage message)
			return Task.CompletedTask;

		var state = message.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
		var kind = state switch
		{
			InProgressTestNodeStateProperty => "started",
			PassedTestNodeStateProperty => "passed",
			SkippedTestNodeStateProperty => "skipped",
			FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty or CancelledTestNodeStateProperty => "failed",
			_ => null
		};
		if (kind == null)
			return Task.CompletedTask;

		lock (_writer)
		{
			_writer.WriteLine($"{kind}\t{message.TestNode.Uid.Value}");
			if (kind != "started" && _stopAfter > 0 && ++_finished >= _stopAfter)
			{
				// the rest are run in a new process by BatchRunner
				Environment.Exit(Program.StopAfterExitCode);
			}
		}
		return Task.CompletedTask;
	}
}
#endif
