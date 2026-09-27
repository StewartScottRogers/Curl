using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One client connection to the sws emulation. Every complete request the client writes is
/// answered at once, and the reply waits to be read; the bytes written are recorded while
/// the server still has the connection open.
/// </summary>
/// <remarks>
/// Once the server has closed its side, reads drain the replies already sent and then return
/// 0, and writes are accepted and dropped unrecorded, as a socket write into a closed
/// connection can succeed without the server ever reading it. A read with no reply waiting
/// also returns 0: in memory the client is the only writer, so nothing else could arrive.
/// </remarks>
internal sealed class SwsHttpServerConnection(SwsHttpReplySelector replySelector, SwsServerCommands serverCommands, List<byte> recording) : IConnection
{
    private readonly List<byte> unservedRequestBytes = [];

    private byte[] unreadReplyBytes = [];

    private bool serverClosed;

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Math.Min(buffer.Length, unreadReplyBytes.Length);
        unreadReplyBytes.AsSpan(0, count).CopyTo(buffer.Span);
        unreadReplyBytes = unreadReplyBytes[count..];
        return ValueTask.FromResult(count);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!serverClosed)
        {
            recording.AddRange(buffer.Span);
            unservedRequestBytes.AddRange(buffer.Span);
            ServeCompleteRequests();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private void ServeCompleteRequests()
    {
        int requestLength;
        while (!serverClosed && (requestLength = SwsHttpRequestFraming.FindRequestLength(unservedRequestBytes.ToArray(), serverCommands)) >= 0)
        {
            SwsHttpReply reply = replySelector.Select(unservedRequestBytes.GetRange(0, requestLength).ToArray());
            unservedRequestBytes.RemoveRange(0, requestLength);
            unreadReplyBytes = [.. unreadReplyBytes, .. reply.Bytes];
            serverClosed = reply.ClosesConnection;
        }
    }
}
