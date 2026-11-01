using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Stands in for the connection of a request that carries <c>Expect: 100-continue</c>: waits
/// up to <see cref="ContinueWait" /> for the server's first status line before the body is
/// sent, and then replays every byte that wait read before reading the connection again.
/// </summary>
/// <remarks>
/// curl 8.21.0 sends the body when <c>100 Continue</c> arrives or when the wait runs out
/// with nothing received, and leaves it unsent when any other reply arrives first (BL-175
/// Notes), except a final 2xx: curl 8.22.0 still waits the time out after one, reports
/// <see cref="HttpConnectionInfoLines.DoneWaitingForContinue" /> and sends the body (measured,
/// CGR-067, BL-3596 Notes). The read the wait started keeps running after the wait runs out,
/// so a status of 300 or above that arrives while the body is sent is seen in time to stop sending it
/// (<see cref="SendUnlessStoppedAsync" />, BL-319 and BL-395 Notes); the next read waits
/// for it. Disposing this does nothing: the connection it wraps belongs to the handler.
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpContinueWaitConnection(IConnection connection) : IConnection
{
    /// <summary>
    /// The longest wait a .NET timer accepts, about 49.7 days; a longer
    /// <see cref="ContinueWait" /> is waited out without a timer, as it would outlast any
    /// transfer.
    /// </summary>
    internal static readonly TimeSpan LongestTimedWait = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <summary>
    /// Gets how long to wait for <c>100 Continue</c> before sending the body anyway: the
    /// <c>--expect100-timeout</c> value, or curl 8.21.0's one second
    /// (<see cref="HttpRequestOptions.DefaultContinueWait" />) when none was given.
    /// </summary>
    internal TimeSpan ContinueWait { get; init; } = HttpRequestOptions.DefaultContinueWait;

    /// <summary>
    /// Gets what <see cref="WaitForContinueAsync" /> tells <see cref="ContinueWait" /> as it starts a timed wait
    /// (one no longer than <see cref="LongestTimedWait" />), or <see langword="null" /> for nothing (BL-3864).
    /// </summary>
    internal Action<TimeSpan>? WaitBegins { get; init; }

    /// <summary>
    /// Gets what <see cref="WaitForContinueAsync" /> tells when its wait ends, however it ends, or
    /// <see langword="null" /> for nothing (BL-3864).
    /// </summary>
    internal Action? WaitEnds { get; init; }

    private readonly byte[] received = new byte[HttpLineReader.MaximumLineLength];

    private int receivedLength;

    private int replayed;

    private Task<bool>? statusLineRead;

    private int? firstStatusCode;

    /// <inheritdoc />
    public bool IsSecure => connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

    /// <summary>
    /// Waits for <c>100 Continue</c> and tells whether to send the body.
    /// </summary>
    /// <param name="timeProvider">The clock <see cref="ContinueWait" /> runs on.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>
    /// <see langword="true" /> when the wait ran out, after nothing or after a final 2xx, or
    /// the first status line received is informational; <see langword="false" /> when any
    /// other reply, or the end of the connection, came first.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    internal async ValueTask<bool> WaitForContinueAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        TellWaitBegins();
        try
        {
            return await WaitForStatusLineOrTimeoutAsync(timeProvider, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            WaitEnds?.Invoke();
        }
    }

    /// <summary>Tells <see cref="WaitBegins" /> the wait's length, when the wait is a timed one.</summary>
    private void TellWaitBegins()
    {
        if (ContinueWait <= LongestTimedWait)
        {
            WaitBegins?.Invoke(ContinueWait);
        }
    }

    /// <summary>The wait <see cref="WaitForContinueAsync" /> makes, between telling it begins and ends.</summary>
    private async Task<bool> WaitForStatusLineOrTimeoutAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        statusLineRead = ReadStatusLineAsync(cancellationToken);
        using CancellationTokenSource waitEnded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task wait = Task.Delay(ContinueWait > LongestTimedWait ? Timeout.InfiniteTimeSpan : ContinueWait, timeProvider, waitEnded.Token);
        Task first = await WhenWaitEndsAsync(statusLineRead, wait).ConfigureAwait(false);
        await waitEnded.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        WaitRanOut = first == wait;
        return WaitRanOut || (statusLineRead.IsCompletedSuccessfully && statusLineRead.Result);
    }

    /// <summary>
    /// Waits for the first status line or the end of <paramref name="wait" />, whichever comes
    /// first, and on past a final 2xx to the end of <paramref name="wait" />: curl 8.22.0 still
    /// waits the time out after one and then sends the body (measured, CGR-067, BL-3596 Notes).
    /// </summary>
    /// <returns><paramref name="wait" /> when the wait ended it; otherwise <paramref name="read" />.</returns>
    private async Task<Task> WhenWaitEndsAsync(Task<bool> read, Task wait)
    {
        Task first = await Task.WhenAny(read, wait).ConfigureAwait(false);
        return first != wait && read.IsCompletedSuccessfully && firstStatusCode is >= 200 and < 300
            ? await Task.WhenAny(wait).ConfigureAwait(false)
            : first;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="WaitForContinueAsync" /> ran out of time
    /// before any status line arrived, or after a final 2xx did, after which curl 8.21.0 reports
    /// <see cref="HttpConnectionInfoLines.DoneWaitingForContinue" /> (measured, BL-449 Notes).
    /// </summary>
    internal bool WaitRanOut { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the first status line received, after the wait ran out,
    /// has a status of 300 or above, which stops curl 8.21.0 sending the body: a
    /// <c>417 Expectation Failed</c> (BL-319 Notes) or any other redirect or error
    /// (<c>HTTP error before end of send, stop sending</c>, BL-395 Notes). A 1xx or 2xx lets
    /// the body go on.
    /// </summary>
    internal bool StopsSending => statusLineRead is { IsCompletedSuccessfully: true } && firstStatusCode >= 300;

    /// <summary>
    /// Writes one piece of the body, unless a status of 300 or above arrives first: a write
    /// still under way when it arrives is cancelled, as curl 8.21.0 stops sending there
    /// (measured, BL-319 and BL-395 Notes).
    /// </summary>
    /// <param name="bytes">The piece to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <see langword="true" /> when the piece was written; <see langword="false" /> when a
    /// status of 300 or above arrived before it was.
    /// </returns>
    /// <exception cref="HttpTransferException">The connection failed the write (exit 55).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    internal async ValueTask<bool> SendUnlessStoppedAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (StopsSending)
        {
            return false;
        }

        using CancellationTokenSource sending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task sent = HttpConnectionSend.WriteAsync(connection, bytes, sending.Token).AsTask();
        await Task.WhenAny(sent, statusLineRead!).ConfigureAwait(false);
        if (!sent.IsCompleted && StopsSending)
        {
            await sending.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await sent.ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (statusLineRead is not null)
        {
            await statusLineRead.ConfigureAwait(false);
        }

        if (replayed == receivedLength)
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        int length = Math.Min(buffer.Length, receivedLength - replayed);
        received.AsSpan(replayed, length).CopyTo(buffer.Span);
        replayed += length;
        return length;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => connection.FlushAsync(cancellationToken);

    /// <summary>
    /// Does nothing: the wrapped connection is disposed by whoever opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Reads until a line feed, the end of the connection, or a full buffer, and tells
    /// whether the first line is an informational status line.
    /// </summary>
    private async Task<bool> ReadStatusLineAsync(CancellationToken cancellationToken)
    {
        int lineFeed;
        while ((lineFeed = Array.IndexOf(received, (byte)'\n', 0, receivedLength)) < 0 && receivedLength < received.Length)
        {
            int read = await connection.ReadAsync(received.AsMemory(receivedLength), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            receivedLength += read;
        }

        firstStatusCode = lineFeed >= 0 ? StatusCodeOf(received.AsSpan(0, lineFeed + 1)) : null;
        return firstStatusCode < 200;
    }

    /// <summary>
    /// Gives the status code of a received first line, or <see langword="null" /> when it is not a
    /// status line.
    /// </summary>
    internal static int? StatusCodeOf(ReadOnlySpan<byte> line)
    {
        try
        {
            return HttpStatusLine.Parse(HttpLine.Split(line).Content).StatusCode;
        }
        catch (HttpTransferException)
        {
            return null;
        }
    }
}
