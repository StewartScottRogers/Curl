using System.Collections.Concurrent;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// An <c>http</c> handler whose transfers each wait, by URL path, until the test ends them, so that a
/// <c>-Z</c> run's transfers finish in whatever order the test chooses. It counts how many run at once.
/// A held transfer ends when <see cref="Finish" /> or <see cref="Fail" /> releases it, or, when it waits
/// on its context's token, when that token is cancelled.
/// </summary>
/// <param name="observesCancellation">
/// Whether a held transfer stops waiting when its context's token is cancelled, as a real handler does.
/// </param>
/// <param name="reportsDoneWhileHeld">
/// Whether a transfer reports itself started and done to its progress sink before it waits, and
/// reports <c>* Ending &lt;path&gt;</c> to its events once released, before its body.
/// </param>
internal sealed class HeldTransferHandler(bool observesCancellation = true, bool reportsDoneWhileHeld = false) : IProtocolHandler
{
    private readonly ConcurrentDictionary<string, HeldTransfer> transfers = new();

    private readonly ConcurrentQueue<string> started = new();

    private int running;

    private int mostRunning;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

    /// <summary>Gets the paths of the transfers started, in the order they started.</summary>
    public IReadOnlyList<string> Started => [.. started];

    /// <summary>Gets the most transfers that were running at once.</summary>
    public int MostRunningAtOnce => Volatile.Read(ref mostRunning);

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        string path = context.Url.AbsolutePath;
        HeldTransfer transfer = Of(path);
        int now = Interlocked.Increment(ref running);
        InterlockedMax(now);
        started.Enqueue(path);
        if (reportsDoneWhileHeld)
        {
            context.Progress.ReportTransferStarted();
            context.Progress.ReportTransferDone();
        }

        transfer.Started.TrySetResult();
        try
        {
            Task<(string Body, TransferResult Result)> ending = transfer.Ending.Task;
            (string body, TransferResult result) = observesCancellation
                ? await ending.WaitAsync(context.CancellationToken)
                : await ending;
            if (reportsDoneWhileHeld)
            {
                context.Events.ReportInfo($"Ending {path}");
            }

            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(body), CancellationToken.None);
            return result;
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }

    /// <summary>Waits until the transfer of <paramref name="path" /> has started.</summary>
    /// <param name="path">The URL path.</param>
    /// <returns>A task that completes when it has.</returns>
    public Task WhenStartedAsync(string path) => Of(path).Started.Task;

    /// <summary>Ends the transfer of <paramref name="path" /> successfully, writing <paramref name="body" />.</summary>
    /// <param name="path">The URL path.</param>
    /// <param name="body">The body.</param>
    public void Finish(string path, string body) =>
        Of(path).Ending.SetResult((body, TransferResult.Success(body.Length)));

    /// <summary>Ends the transfer of <paramref name="path" /> with a failure and no body.</summary>
    /// <param name="path">The URL path.</param>
    /// <param name="exitCode">The failure's code.</param>
    /// <param name="message">The failure's message.</param>
    public void Fail(string path, CurlExitCode exitCode, string message) =>
        Of(path).Ending.SetResult((string.Empty, TransferResult.Failure(exitCode, message)));

    private HeldTransfer Of(string path) => transfers.GetOrAdd(path, _ => new HeldTransfer());

    private void InterlockedMax(int now)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref mostRunning);
        }
        while (now > seen && Interlocked.CompareExchange(ref mostRunning, now, seen) != seen);
    }

    /// <summary>One held transfer's signals.</summary>
    private sealed class HeldTransfer
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(string Body, TransferResult Result)> Ending { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
