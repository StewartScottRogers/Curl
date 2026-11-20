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
    private readonly List<EndPoint> _refused = [];
    private readonly List<IDatagramChannel> _retired = [];
    private int _nextAlternative;
    private volatile bool _hasReceived;
    private bool _connectsToServer;

    /// <inheritdoc />
    public EndPoint ServerEndPoint => _inner.ServerEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => _inner.LocalEndPoint;

    /// <inheritdoc />
    public void ConnectToServer()
    {
        _connectsToServer = true;
        try
        {
            _inner.ConnectToServer();
        }
        catch (SocketException) when (ConnectToNextAddress())
        {
        }
    }

    /// <summary>
    /// After the connect to the current address failed, opens and connects the next resolved address that
    /// accepts it and makes it the current channel; the failed channel is disposed with this one. Returns
    /// <see langword="false" /> when no address is left, so the connect failure propagates.
    /// </summary>
    private bool ConnectToNextAddress()
    {
        while (_nextAlternative < alternatives.Count)
        {
            IDatagramChannel? opened = null;
            try
            {
                opened = openChannel(alternatives[_nextAlternative++]);
                opened.ConnectToServer();
            }
            catch (SocketException)
            {
                if (opened is not null)
                {
                    _retired.Add(opened);
                }

                continue;
            }

            _refused.Add(_inner.ServerEndPoint);
            _retired.Add(_inner);
            _inner = opened;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        if (!_hasReceived)
        {
            _sent.Add(datagram.ToArray());
        }

        try
        {
            await _inner.SendAsync(datagram, _refused.Contains(destination) ? _inner.ServerEndPoint : destination, cancellationToken).ConfigureAwait(false);
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
    public async ValueTask DisposeAsync()
    {
        foreach (var retired in _retired)
        {
            await retired.DisposeAsync().ConfigureAwait(false);
        }

        await _inner.DisposeAsync().ConfigureAwait(false);
    }

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
            var opened = await OpenAndResendAsync(alternatives[_nextAlternative++], cancellationToken).ConfigureAwait(false);
            if (opened is null)
            {
                continue;
            }

            _refused.Add(_inner.ServerEndPoint);
            await _inner.DisposeAsync().ConfigureAwait(false);
            _inner = opened;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Opens a channel to <paramref name="next" /> and sends it every datagram sent so far. Returns
    /// <see langword="null" /> when that fails with a <see cref="SocketException" />; any other failure
    /// propagates. Either way the opened channel is disposed when the resend fails.
    /// </summary>
    private async ValueTask<IDatagramChannel?> OpenAndResendAsync(IPEndPoint next, CancellationToken cancellationToken)
    {
        IDatagramChannel? opened = null;
        Exception failure;
        try
        {
            opened = openChannel(next);
            if (_connectsToServer)
            {
                opened.ConnectToServer();
            }

            foreach (var datagram in _sent)
            {
                await opened.SendAsync(datagram, opened.ServerEndPoint, cancellationToken).ConfigureAwait(false);
            }

            return opened;
        }
        catch (Exception resendFailure)
        {
            failure = resendFailure;
        }

        if (opened is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(false);
        }

        return failure is SocketException ? null : throw failure;
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
