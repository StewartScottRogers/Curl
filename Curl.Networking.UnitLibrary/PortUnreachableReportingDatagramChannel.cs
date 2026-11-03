using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Forwards every call to the channel it wraps, and turns a receive or send that fails because the server's
/// port is closed (<see cref="SocketError.ConnectionReset" /> or <see cref="SocketError.ConnectionRefused" />)
/// into a <see cref="DatagramPortUnreachableException" /> with the text <paramref name="describeRefusal" />
/// gives, curl's exit 7 text (BL-4654).
/// </summary>
/// <param name="inner">The channel the calls go to.</param>
/// <param name="describeRefusal">Gives curl's exit 7 text at the moment the refusal is seen.</param>
internal sealed class PortUnreachableReportingDatagramChannel(IDatagramChannel inner, Func<string> describeRefusal) : IDatagramChannel
{
    /// <inheritdoc />
    public EndPoint ServerEndPoint => inner.ServerEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        try
        {
            await inner.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException refused) when (IsPortUnreachable(refused))
        {
            throw new DatagramPortUnreachableException(refused.SocketErrorCode, describeRefusal());
        }
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException refused) when (IsPortUnreachable(refused))
        {
            throw new DatagramPortUnreachableException(refused.SocketErrorCode, describeRefusal());
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static bool IsPortUnreachable(SocketException exception) =>
        exception is not DatagramPortUnreachableException
        && exception.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused;
}
