using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Forwards every call to the channel it wraps, and turns a receive or send that fails because the server's
/// port is closed (<see cref="SocketError.ConnectionReset" /> or <see cref="SocketError.ConnectionRefused" />)
/// into a <see cref="DatagramPortUnreachableException" /> with the text <paramref name="describeRefusal" />
/// gives, curl's exit 7 text (BL-4654), but only until the channel has received its first datagram (BL-4675):
/// a port that refuses after the server has answered is not "failed to connect", so that <see cref="SocketException" />
/// passes through unchanged for the handler to report as its own receive or send failure.
/// </summary>
/// <param name="inner">The channel the calls go to.</param>
/// <param name="describeRefusal">Gives curl's exit 7 text at the moment the refusal is seen.</param>
internal sealed class PortUnreachableReportingDatagramChannel(IDatagramChannel inner, Func<string> describeRefusal) : IDatagramChannel
{
    private volatile bool _hasReceived;

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
        catch (SocketException refused)
        {
            throw Translated(refused);
        }
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            var received = await inner.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            _hasReceived = true;
            return received;
        }
        catch (SocketException refused)
        {
            throw Translated(refused);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    /// <summary>
    /// The exception to throw for <paramref name="failure" />: curl's exit 7 refusal while no datagram has
    /// been received yet, else <paramref name="failure" /> itself.
    /// </summary>
    private SocketException Translated(SocketException failure) =>
        !_hasReceived
        && failure is not DatagramPortUnreachableException
        && IsRefusal(failure.SocketErrorCode)
            ? new DatagramPortUnreachableException(failure.SocketErrorCode, describeRefusal())
            : failure;

    private static bool IsRefusal(SocketError error) =>
        error == SocketError.ConnectionReset || error == SocketError.ConnectionRefused;
}
