using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITcpDialer" /> that binds each TCP connection's local end to what a
/// <see cref="LocalBinding" /> asks before it connects, choosing the local address for the family
/// of each address dialled through a <see cref="LocalBindingAddressChooser" />, and dials Unix
/// domain sockets unbound, as libcurl binds only IP sockets.
/// </summary>
/// <remarks>
/// An interface name is first bound as a device (<see cref="ITcpDialer.DialFromDeviceAsync" />, BL-1026),
/// as libcurl's <c>bindlocal</c> tries <c>SO_BINDTODEVICE</c>: a plain or <c>if!</c> name bound so needs
/// no address, while <c>ifhost!</c> goes on to bind its host's address.
/// </remarks>
/// <param name="inner">Binds and connects each socket.</param>
/// <param name="addressChooser">Chooses the local address for each dial, and gives the ports to bind.</param>
internal sealed class LocalBindingTcpDialer(ITcpDialer inner, LocalBindingAddressChooser addressChooser) : ITcpDialer
{
    /// <summary>Creates the dialer over a chooser for <paramref name="binding" />.</summary>
    /// <param name="inner">Binds and connects each socket.</param>
    /// <param name="binding">What to bind to.</param>
    /// <param name="interfaces">Finds an interface's addresses; the Windows build finds none (ADR-0110).</param>
    /// <param name="dnsResolver">Resolves <see cref="LocalBinding.HostName" /> when it is not an address.</param>
    public LocalBindingTcpDialer(ITcpDialer inner, LocalBinding binding, INetworkInterfaceLookup interfaces, IDnsResolver dnsResolver)
        : this(inner, new LocalBindingAddressChooser(binding, interfaces, dnsResolver))
    {
    }

    /// <inheritdoc />
    /// <exception cref="LocalBindException">The local end could not be bound, as its <see cref="LocalBindException.Failure" /> says.</exception>
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var binding = addressChooser.Binding;
        if ((binding.InterfaceName ?? binding.DeviceName) is { } deviceName)
        {
            return await inner.DialFromDeviceAsync(
                endPoint,
                deviceName,
                bindsAddressAfterDevice: binding.InterfaceName is null,
                token => ChooseLocalEndAsync(endPoint.AddressFamily, token),
                binding.PortCount,
                cancellationToken).ConfigureAwait(false);
        }

        var localEndPoint = await ChooseLocalEndAsync(endPoint.AddressFamily, cancellationToken).ConfigureAwait(false);
        return await inner.DialFromAsync(endPoint, localEndPoint, binding.PortCount, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IPEndPoint> ChooseLocalEndAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        var localAddress = await addressChooser.ChooseAsync(family, cancellationToken).ConfigureAwait(false);
        return new IPEndPoint(localAddress, addressChooser.Binding.FirstPort);
    }

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        inner.DialUnixSocketAsync(address, cancellationToken);
}
