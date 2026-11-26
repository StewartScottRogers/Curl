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
/// Notes). When that reply is a final status over HTTP/1.x, or a final 2xx on a stream, the wait
/// keeps running while the response is read (<see cref="HoldsBody" />): curl sends the body when it runs out,
/// during a read or after the response, unless the response stops the upload first (measured,
/// CGR-067, BL-3596 and BL-5960 Notes). The read the wait started keeps running after the wait
/// runs out, so a status of 300 or above that arrives while the body is sent is seen in time to
/// stop sending it (<see cref="SendUnlessStoppedAsync" />, BL-319 and BL-395 Notes); the next
/// read waits for it. On a stream (<see cref="IsMultiplexedStream" />) the status is the response's
/// own: a <c>100</c> sends the body at once, a status of 300 or above during the wait leaves it
/// unsent (<see cref="StoppedStreamUpload" />) (curl 8.18.0's nghttp2 build, measured, BL-5970
/// Notes), and one that arrives after the wait ran out stops the body part way (measured over
/// HTTP/2, BL-5973 Notes; HTTP/3 does the same, ADR-1411). Disposing this only ends a wait still
/// holding the body: the connection it wraps belongs to the handler.
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpContinueWaitConnection(IConnection connection) : IConnection, IDisposable
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
    /// Gets what is told when the wait ends, however it ends - for a held body, when it is sent
    /// or given up - or <see langword="null" /> for nothing (BL-3864).
    /// </summary>
    internal Action? WaitEnds { get; init; }

    /// <summary>
    /// Gets a value indicating whether this stands for an HTTP/2 or HTTP/3 stream, where no body
    /// is held after a status of 300 or above.
    /// </summary>
    internal bool IsMultiplexedStream { get; init; }

    /// <summary>Gets the HTTP/2 stream this stands for, or <see langword="null" /> when it is none.</summary>
    internal Http2StreamConnection? Http2Stream { get; init; }

    /// <summary>Gets the HTTP/3 stream this stands for, or <see langword="null" /> when it is none.</summary>
    internal Http3StreamConnection? Http3Stream { get; init; }

    /// <summary>Gets or sets what sends a held body once the wait runs out; nothing by default.</summary>
    internal Func<CancellationToken, ValueTask> SendBody { get; set; } = _ => ValueTask.CompletedTask;

    /// <summary>
    /// Gets or sets the send failure of a held body, kept so the response is delivered first, as
    /// curl 8.22.0 does (BL-4909); <see langword="null" /> for none.
    /// </summary>
    internal HttpTransferException? HeldSendFailure { get; set; }

    /// <summary>
    /// Gets a value indicating whether a final status arrived during the wait and the body still
    /// waits for the wait to run out, or to be given up (<see cref="Dispose" />).
    /// </summary>
    internal bool HoldsBody => heldWaitTimer is not null;

    private CancellationTokenSource? heldWaitTimer;

    private Task? heldWaitEnd;

    private bool sendsHeldBody;

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
    /// <see langword="true" /> to send the body now: the wait ran out, or the first status line
    /// received is informational; <see langword="false" /> when any other reply, or the end of
    /// the connection, came first, a final status leaving the body held (<see cref="HoldsBody" />).
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
            TellWaitEndsUnlessHolding();
        }
    }

    /// <summary>Tells <see cref="WaitEnds" />, unless the wait goes on for a held body.</summary>
    private void TellWaitEndsUnlessHolding()
    {
        if (!HoldsBody)
        {
            WaitEnds?.Invoke();
        }
    }

    /// <summary>
    /// Waits out what is left of a held body's wait, then sends the body through
    /// <see cref="SendBody" />; a status of 300 or above no longer stops it.
    /// </summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes once the body has been sent.</returns>
    /// <exception cref="OperationCanceledException">The wait or the send was cancelled.</exception>
    internal async ValueTask SendHeldBodyAsync(CancellationToken cancellationToken)
    {
        await heldWaitEnd!.ConfigureAwait(false);
        sendsHeldBody = true;
        Dispose();
        await SendBody(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends a wait still holding the body, which is then not sent unless
    /// <see cref="SendHeldBodyAsync" /> is sending it; nothing when none is held.
    /// </summary>
    public void Dispose()
    {
        if (heldWaitTimer is not { } timer)
        {
            return;
        }

        heldWaitTimer = null;
        timer.Cancel();
        timer.Dispose();
        WaitEnds?.Invoke();
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
        CancellationTokenSource waitEnded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task wait = Task.Delay(ContinueWait > LongestTimedWait ? Timeout.InfiniteTimeSpan : ContinueWait, timeProvider, waitEnded.Token);
        Task first = await Task.WhenAny(statusLineRead, wait).ConfigureAwait(false);
        await HoldOrEndWaitAsync(first != wait, wait, waitEnded).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        WaitRanOut = first == wait;
        StopStreamUploadAtErrorStatusAfterTimeout();
        return WaitRanOut || (statusLineRead.IsCompletedSuccessfully && statusLineRead.Result);
    }

    /// <summary>
    /// Once the wait has run out, has the HTTP/2 or HTTP/3 stream stop its body at a status of 300
    /// or above (<see cref="Http2StreamConnection.StopsUploadAtErrorStatus" />,
    /// <see cref="Http3StreamConnection.StopsUploadAtErrorStatus" />).
    /// </summary>
    private void StopStreamUploadAtErrorStatusAfterTimeout()
    {
        if (Http2Stream is { } http2)
        {
            http2.StopsUploadAtErrorStatus = WaitRanOut;
        }

        if (Http3Stream is { } http3)
        {
            http3.StopsUploadAtErrorStatus = WaitRanOut;
        }
    }

    /// <summary>
    /// Keeps <paramref name="wait" /> running to hold the body when a final status over HTTP/1.x,
    /// or a final 2xx on a stream, ended the caller's wait, and otherwise cancels it.
    /// </summary>
    private async ValueTask HoldOrEndWaitAsync(bool answered, Task wait, CancellationTokenSource waitEnded)
    {
        if (answered && firstStatusCode >= 200 && !(IsMultiplexedStream && firstStatusCode >= 300))
        {
            heldWaitTimer = waitEnded;
            heldWaitEnd = wait;
            return;
        }

        await waitEnded.CancelAsync().ConfigureAwait(false);
        waitEnded.Dispose();
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="WaitForContinueAsync" /> ran out of time
    /// before any status line arrived, after which curl 8.21.0 reports
    /// <see cref="HttpConnectionInfoLines.DoneWaitingForContinue" /> (measured, BL-449 Notes).
    /// </summary>
    internal bool WaitRanOut { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the first status line received, after the wait ran out,
    /// has a status of 300 or above, which stops curl 8.21.0 sending the body: a
    /// <c>417 Expectation Failed</c> (BL-319 Notes) or any other redirect or error
    /// (<c>HTTP error before end of send, stop sending</c>, BL-395 Notes). A 1xx or 2xx lets
    /// the body go on, and so does any status that held the body until the wait ran out
    /// (<see cref="SendHeldBodyAsync" />). On an HTTP/2 or HTTP/3 stream the stream itself says so
    /// (<see cref="Http2StreamConnection.IsUploadStopped" />,
    /// <see cref="Http3StreamConnection.IsUploadStopped" />).
    /// </summary>
    internal bool StopsSending => IsStreamUploadStopped ?? StopsHttp1Sending;

    /// <summary>Whether the stream this stands for stopped its upload, or <see langword="null" /> when it stands for none.</summary>
    private bool? IsStreamUploadStopped => Http2Stream?.IsUploadStopped ?? Http3Stream?.IsUploadStopped;

    /// <summary><see cref="StopsSending" /> over HTTP/1.x.</summary>
    private bool StopsHttp1Sending =>
        !IsMultiplexedStream && !sendsHeldBody && statusLineRead is { IsCompletedSuccessfully: true } && firstStatusCode >= 300;

    /// <summary>
    /// Gets a value indicating whether a status of 300 or above arrived on a stream
    /// (<see cref="IsMultiplexedStream" />) during the wait, which leaves the whole body unsent.
    /// </summary>
    internal bool StoppedStreamUpload => IsMultiplexedStream && !WaitRanOut && firstStatusCode >= 300;

    /// <summary>
    /// Gets a value indicating whether the first status line received is a 1xx one, the
    /// <c>100 Continue</c> whose head curl 8.22.0 prints before it sends the body (BL-4932).
    /// </summary>
    internal bool AnsweredWithInformational => statusLineRead is { IsCompletedSuccessfully: true, Result: true };

    /// <summary>
    /// Gets a value indicating whether the first status line received is a final 2xx, which lets
    /// the body go on: a send that then fails still delivers that response first, as curl 8.22.0
    /// does (BL-4909). Its bytes stay in this connection's replay buffer, so a reset the send
    /// causes does not lose them.
    /// </summary>
    internal bool AnsweredWithFinal2xx => statusLineRead is { IsCompletedSuccessfully: true } && firstStatusCode is >= 200 and < 300;

    /// <summary>
    /// Writes one piece of the body, unless a status of 300 or above arrives first: a write
    /// still under way when it arrives is cancelled, as curl 8.21.0 stops sending there
    /// (measured, BL-319 and BL-395 Notes). On a stream a write under way is left to end by
    /// itself, and counts as written (<see cref="Http2StreamConnection.IsUploadStopped" />,
    /// <see cref="Http3StreamConnection.IsUploadStopped" />).
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
        if (!sent.IsCompleted && !IsMultiplexedStream && StopsSending)
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
            return await ReadConnectionAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        int length = Math.Min(buffer.Length, receivedLength - replayed);
        received.AsSpan(replayed, length).CopyTo(buffer.Span);
        replayed += length;
        return length;
    }

    /// <summary>Reads the connection, sending a held body when its wait runs out first.</summary>
    private async ValueTask<int> ReadConnectionAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!HoldsBody)
        {
            return await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        Task<int> read = connection.ReadAsync(buffer, cancellationToken).AsTask();
        if (await Task.WhenAny(read, heldWaitEnd!).ConfigureAwait(false) != read)
        {
            await SendHeldBodyAsync(cancellationToken).ConfigureAwait(false);
        }

        return await read.ConfigureAwait(false);
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

        firstStatusCode = lineFeed >= 0 ? FirstStatusCodeOf(received.AsSpan(0, lineFeed + 1)) : null;
        return firstStatusCode < 200;
    }

    /// <summary>
    /// Gives the status code of the first line received: on a stream
    /// (<see cref="IsMultiplexedStream" />) the line is the <c>HTTP/2 NNN </c> or
    /// <c>HTTP/3 NNN </c> one the stream connection writes, and otherwise it is read as
    /// <see cref="StatusCodeOf" /> reads it.
    /// </summary>
    private int? FirstStatusCodeOf(ReadOnlySpan<byte> line) =>
        IsMultiplexedStream
            ? HttpStatusLine.ParseHttp2OrHttp3(HttpLine.Split(line).Content).StatusCode
            : StatusCodeOf(line);

    /// <summary>
    /// Gives the status code of a first line received over HTTP/1.x, or <see langword="null" />
    /// when it is not a status line.
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
