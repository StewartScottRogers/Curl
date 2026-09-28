using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Stands in for the connection of a request that carries <c>Expect: 100-continue</c>: waits
/// up to <see cref="ContinueWait" /> for the server's first status line before the body is
/// sent, and then replays every byte that wait read before reading the connection again.
/// </summary>
/// <remarks>
/// curl 8.21.0 sends the body when <c>100 Continue</c> arrives or when the wait runs out
/// with nothing received, and leaves it unsent when a final status arrives first (BL-175
/// Notes). The read the wait started keeps running after the wait runs out, so a status of
/// 300 or above that arrives while the body is sent is seen in time to stop sending it
/// (<see cref="SendUnlessStoppedAsync" />, BL-319 and BL-395 Notes); the next read waits
/// for it. Disposing this does nothing: the connection it wraps belongs to the handler.
/// </remarks>
/// <param name="connection">The connection the request head was written to.</param>
internal sealed class HttpContinueWaitConnection(IConnection connection) : IConnection
{
    /// <summary>
    /// How long curl 8.21.0 waits for <c>100 Continue</c> before sending the body anyway.
    /// </summary>
    internal static readonly TimeSpan ContinueWait = TimeSpan.FromSeconds(1);

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
    /// <see langword="true" /> when the wait ran out or the first status line received is
    /// informational; <see langword="false" /> when any other reply, or the end of the
    /// connection, came first.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    internal async ValueTask<bool> WaitForContinueAsync(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        statusLineRead = ReadStatusLineAsync(cancellationToken);
        using CancellationTokenSource waitEnded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task wait = Task.Delay(ContinueWait, timeProvider, waitEnded.Token);
        Task first = await Task.WhenAny(statusLineRead, wait).ConfigureAwait(false);
        await waitEnded.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        WaitRanOut = first == wait;
        return WaitRanOut || (statusLineRead.IsCompletedSuccessfully && statusLineRead.Result);
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

    private static int? StatusCodeOf(ReadOnlySpan<byte> line)
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
