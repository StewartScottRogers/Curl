using System.Net;
using System.Runtime.InteropServices;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The connection an HTTP/1.1 request asking to upgrade to h2c runs on (RFC 7540 section 3.2),
/// as curl sends one for <c>--http2</c> over cleartext (measured, BL-716 Notes). Writes go to
/// the transport. Reads give the response as received until it shows whether its first head
/// is <c>HTTP/1.1 101</c>: if not, they carry on reading the transport as HTTP/1.1; if so, they
/// give that head, then report <see cref="HttpConnectionInfoLines.SwitchingToHttp2" />, send the
/// client preface and give the response read from stream 1 as <see cref="Http2StreamConnection" />
/// writes it - <c>HTTP/2 200 </c>, the headers, the empty line, then the body.
/// </summary>
/// <param name="transport">The connection the request is sent on; the handler keeps ownership.</param>
/// <param name="events">Where the switch's <c>-v</c> lines are reported.</param>
/// <param name="scheme">The URL's scheme.</param>
/// <param name="traceEvents">
/// Where the switch's and stream 1's <c>--trace-config http/2</c> lines go (<see cref="Http2FrameTrace" />,
/// BL-1205), or <see langword="null" /> for none.
/// </param>
internal sealed class HttpH2cUpgradeConnection(IConnection transport, ITransferEvents events, string scheme, ITransferEvents? traceEvents = null) : IConnection
{
    private const int ReadSize = 16384;

    private static readonly byte[] SwitchingStatus = "HTTP/1.1 101"u8.ToArray();

    private readonly List<byte> received = [];

    private bool isWatching = true;

    /// <summary>The read that watches for the <c>101</c>, started once the request head is written (BL-3910).</summary>
    private Task? watch;

    private int handedOut;

    private int switchingHeadLength;

    private bool isRequestWritten;

    /// <summary>Whether the upgraded request, the first one written, is a HEAD, whose response on stream 1 has no body (BL-3667).</summary>
    private bool isHeadRequest;

    /// <summary>
    /// Gets a value indicating whether the connection has switched to HTTP/2, so the heads read
    /// from it now are an HTTP/2 stream's.
    /// </summary>
    public bool IsUpgraded => UpgradedStream is not null;

    /// <summary>
    /// Gets stream 1, the upgraded request's, once the connection has switched to HTTP/2, or
    /// <see langword="null" /> before.
    /// </summary>
    public Http2StreamConnection? UpgradedStream { get; private set; }

    /// <summary>
    /// Gets the HTTP/2 session the connection switched to, which carries the transfer's later
    /// requests on streams 3, 5, ... (BL-866), or <see langword="null" /> before the switch.
    /// </summary>
    public Http2Session? UpgradedSession { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => transport.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => transport.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => transport.LocalEndPoint;

    /// <inheritdoc />
    /// <exception cref="HttpTransferException">The HTTP/2 stream failed (exit 16, 18, 56 or 92).</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (UpgradedStream is not null)
        {
            return await UpgradedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        if (isWatching)
        {
            await StartWatching(cancellationToken).ConfigureAwait(false);
        }

        if (handedOut < HandOutLength)
        {
            return HandOut(buffer.Span);
        }

        if (switchingHeadLength > 0)
        {
            UpgradedStream = await SwitchAsync(cancellationToken).ConfigureAwait(false);
            return await UpgradedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        return await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes to the transport, and starts watching for the <c>101</c> once the first write, the
    /// request head, is out. Later writes go in pieces of curl's upload buffer, and the pieces
    /// left once the whole <c>101</c> head has arrived are dropped: curl
    /// 8.18.0 stops sending the HTTP/1.1 body when the upgrade is answered before the body has
    /// gone, and goes on in HTTP/2 (measured, BL-3910 Notes).
    /// </summary>
    /// <param name="buffer">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write, and the watch the first write starts.</param>
    /// <returns>The write.</returns>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (isRequestWritten)
        {
            return WriteUntilSwitchingAsync(buffer, cancellationToken);
        }

        isRequestWritten = true;
        isHeadRequest = buffer.Span.StartsWith("HEAD "u8);
        return WriteRequestHeadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => transport.FlushAsync(cancellationToken);

    /// <summary>
    /// Does nothing: the transport is disposed by the handler that opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Gives the length of the bytes handed out before the transport is read again or the
    /// connection switches: the <c>101</c>'s head, or everything read while watching.
    /// </summary>
    private int HandOutLength => switchingHeadLength > 0 ? switchingHeadLength : received.Count;

    /// <summary>
    /// Finds the end of the head as a line feed followed by an empty line, ended by a carriage
    /// return and line feed or by a line feed alone.
    /// </summary>
    /// <returns>The head's length, or 0 when it has not ended yet.</returns>
    private static int HeadLengthOf(ReadOnlySpan<byte> bytes)
    {
        int lineFeedEnd = EndOf(bytes, "\n\n"u8);
        int carriageReturnEnd = EndOf(bytes, "\n\r\n"u8);
        return lineFeedEnd == 0 || (carriageReturnEnd != 0 && carriageReturnEnd < lineFeedEnd) ? carriageReturnEnd : lineFeedEnd;
    }

    private static int EndOf(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> terminator)
    {
        int index = bytes.IndexOf(terminator);
        return index < 0 ? 0 : index + terminator.Length;
    }

    /// <summary>
    /// Gets a value indicating whether the watch has ended on a whole <c>101</c> head, so the
    /// connection is switching to HTTP/2.
    /// </summary>
    private bool HasSwitchingHeadArrived => watch is { IsCompletedSuccessfully: true } && switchingHeadLength > 0;

    private async ValueTask WriteUntilSwitchingAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        for (int sent = 0; sent < buffer.Length && !HasSwitchingHeadArrived; sent += HttpRequestBodyWriter.UploadBufferSize)
        {
            int length = Math.Min(HttpRequestBodyWriter.UploadBufferSize, buffer.Length - sent);
            await transport.WriteAsync(buffer.Slice(sent, length), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteRequestHeadAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await transport.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _ = StartWatching(cancellationToken);
    }

    /// <summary>
    /// Gives the watch for the <c>101</c>, starting it the first time, so the body can be sent
    /// while the response is read.
    /// </summary>
    private Task StartWatching(CancellationToken cancellationToken) => watch ??= WatchAsync(cancellationToken).AsTask();

    /// <summary>
    /// Reads the transport until the bytes cannot begin <c>HTTP/1.1 101</c>, or the whole of
    /// such a head has arrived, or the peer closes.
    /// </summary>
    private async ValueTask WatchAsync(CancellationToken cancellationToken)
    {
        byte[] chunk = new byte[ReadSize];
        while (isWatching)
        {
            int read = await transport.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            received.AddRange(chunk.AsSpan(0, read));
            isWatching = read > 0 && WaitsForMore();
        }
    }

    /// <summary>
    /// Decides whether to read on: the bytes so far begin, or could still begin,
    /// <c>HTTP/1.1 101</c> and the head has not ended; notes the head's length once it has.
    /// </summary>
    private bool WaitsForMore()
    {
        ReadOnlySpan<byte> bytes = CollectionsMarshal.AsSpan(received);
        int compared = Math.Min(bytes.Length, SwitchingStatus.Length);
        if (!bytes[..compared].SequenceEqual(SwitchingStatus.AsSpan(0, compared)))
        {
            return false;
        }

        switchingHeadLength = compared == SwitchingStatus.Length ? HeadLengthOf(bytes) : 0;
        return switchingHeadLength == 0;
    }

    private int HandOut(Span<byte> buffer)
    {
        int length = Math.Min(buffer.Length, HandOutLength - handedOut);
        CollectionsMarshal.AsSpan(received).Slice(handedOut, length).CopyTo(buffer);
        handedOut += length;
        return length;
    }

    /// <summary>
    /// Switches to HTTP/2 once the <c>101</c>'s head has been handed out: reports it, as curl
    /// does, then takes stream 1 on a session that reads first whatever arrived after the head.
    /// </summary>
    private async ValueTask<Http2StreamConnection> SwitchAsync(CancellationToken cancellationToken)
    {
        byte[] afterHead = [.. received.Skip(switchingHeadLength)];
        events.ReportInfo(HttpConnectionInfoLines.SwitchingToHttp2);
        Http2FrameTrace? frameTrace = traceEvents is null ? null : new(traceEvents);
        frameTrace?.UpgradeStarted();
        if (afterHead.Length > 0)
        {
            events.ReportInfo(HttpConnectionInfoLines.CopiedHttp2DataAfterUpgrade(afterHead.Length));
        }

        frameTrace?.SessionCreatedByUpgrade();
        UpgradedSession = new Http2Session(new HttpPrefixedConnection(afterHead, transport));
        Http2StreamConnection stream = new(UpgradedSession, scheme, 0, frameTrace: frameTrace);
        await stream.StartUpgradedAsync(isHeadRequest, cancellationToken).ConfigureAwait(false);
        return stream;
    }
}
