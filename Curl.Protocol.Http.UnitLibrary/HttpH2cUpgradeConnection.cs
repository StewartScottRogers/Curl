using System.Net;
using System.Runtime.InteropServices;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

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
internal sealed class HttpH2cUpgradeConnection(IConnection transport, ITransferEvents events, string scheme) : IConnection
{
    private const int ReadSize = 16384;

    private static readonly byte[] SwitchingStatus = "HTTP/1.1 101"u8.ToArray();

    private readonly List<byte> received = [];

    private bool isWatching = true;

    private int handedOut;

    private int switchingHeadLength;

    private Http2StreamConnection? upgradedStream;

    /// <summary>
    /// Gets a value indicating whether the connection has switched to HTTP/2, so the heads read
    /// from it now are an HTTP/2 stream's.
    /// </summary>
    public bool IsUpgraded => upgradedStream is not null;

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
        if (upgradedStream is not null)
        {
            return await upgradedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        if (isWatching)
        {
            await WatchAsync(cancellationToken).ConfigureAwait(false);
        }

        if (handedOut < HandOutLength)
        {
            return HandOut(buffer.Span);
        }

        if (switchingHeadLength > 0)
        {
            upgradedStream = await SwitchAsync(cancellationToken).ConfigureAwait(false);
            return await upgradedStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        return await transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        transport.WriteAsync(buffer, cancellationToken);

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
        if (afterHead.Length > 0)
        {
            events.ReportInfo(HttpConnectionInfoLines.CopiedHttp2DataAfterUpgrade(afterHead.Length));
        }

        Http2StreamConnection stream = new(new Http2Session(new HttpPrefixedConnection(afterHead, transport)), scheme, 0);
        await stream.StartUpgradedAsync(cancellationToken).ConfigureAwait(false);
        return stream;
    }
}
