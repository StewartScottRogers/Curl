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
/// <param name="alternatives">The resolved endpoints after the one <paramref name="inner" /> is open to, tried in turn when it refuses before any reply.</param>
/// <param name="openChannel">Opens a channel to one of <paramref name="alternatives" />, or throws <see cref="SocketException" />.</param>
internal sealed class PortUnreachableReportingDatagramChannel(
    IDatagramChannel inner,
    Func<string> describeRefusal,
    IReadOnlyList<IPEndPoint> alternatives,
    Func<IPEndPoint, IDatagramChannel> openChannel) : IDatagramChannel
{
    private readonly List<ReadOnlyMemory<byte>> _sent = [];
    private IDatagramChannel _inner = inner;
    private int _nextAlternative;
    private volatile bool _hasReceived;

    /// <inheritdoc />
    public EndPoint ServerEndPoint => _inner.ServerEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => _inner.LocalEndPoint;

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        if (!_hasReceived)
        {
            _sent.Add(datagram.ToArray());
        }

        try
        {
            await _inner.SendAsync(datagram, destination, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException refused)
        {
            if (!await SwitchToNextAddressAsync(refused, cancellationToken).ConfigureAwait(false))
            {
                throw Translated(refused);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                var received = await _inner.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                _hasReceived = true;
                return received;
            }
            catch (SocketException refused)
            {
                if (!await SwitchToNextAddressAsync(refused, cancellationToken).ConfigureAwait(false))
                {
                    throw Translated(refused);
                }
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _inner.DisposeAsync();

    /// <summary>
    /// After a refusal before any datagram has been received, moves to the next resolved address as curl
    /// moves on after <c>::1</c> refuses (EXT-100): opens a channel to it, disposes the refused one and sends
    /// again every datagram sent so far. Returns <see langword="false" /> when there is no address left.
    /// </summary>
    private async ValueTask<bool> SwitchToNextAddressAsync(SocketException failure, CancellationToken cancellationToken)
    {
        if (_hasReceived || !IsRefusal(failure.SocketErrorCode))
        {
            return false;
        }

        while (_nextAlternative < alternatives.Count)
        {
            var next = alternatives[_nextAlternative++];
            IDatagramChannel? opened = null;
            try
            {
                opened = openChannel(next);
                foreach (var datagram in _sent)
                {
                    await opened.SendAsync(datagram, opened.ServerEndPoint, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SocketException)
            {
                if (opened is not null)
                {
                    await opened.DisposeAsync().ConfigureAwait(false);
                }

                continue;
            }

            await _inner.DisposeAsync().ConfigureAwait(false);
            _inner = opened;
            return true;
        }

        return false;
    }

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
