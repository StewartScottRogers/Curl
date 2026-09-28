using System.Text;

namespace Curl.Console;

/// <summary>
/// A memory stream a test can wait on until its bytes, read as Latin-1, end with a given text, so that
/// a <c>-Z</c> test ends the next transfer only once the last one's report is written.
/// </summary>
internal sealed class TextWaitingStream : MemoryStream
{
    private readonly List<(string Text, TaskCompletionSource Written)> waiters = [];

    /// <summary>Gets the bytes written so far, read as Latin-1.</summary>
    public string Text
    {
        get
        {
            lock (waiters)
            {
                return Encoding.Latin1.GetString(ToArray());
            }
        }
    }

    /// <summary>Waits until the bytes written end with <paramref name="text" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when they do.</returns>
    public Task WhenEndsWithAsync(string text)
    {
        TaskCompletionSource written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (waiters)
        {
            waiters.Add((text, written));
            SignalWaiters();
        }

        return written.Task;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (waiters)
        {
            base.Write(buffer, offset, count);
            SignalWaiters();
        }
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => Write(buffer.ToArray(), 0, buffer.Length);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    private void SignalWaiters()
    {
        string written = Encoding.Latin1.GetString(ToArray());
        foreach ((string text, TaskCompletionSource signal) in waiters.Where(waiter => written.EndsWith(waiter.Text, StringComparison.Ordinal)).ToList())
        {
            signal.TrySetResult();
            waiters.Remove((text, signal));
        }
    }
}
