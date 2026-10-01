using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITcpDialer" /> that binds each TCP connection's local end to what a
/// <see cref="LocalBinding" /> asks before it connects, choosing the local address for the family
/// of each address dialled through a <see cref="LocalBindingAddressChooser" />, and dials Unix
/// domain sockets unbound, as libcurl binds only IP sockets.
/// </summary>
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

        var localAddress = await addressChooser.ChooseAsync(endPoint.AddressFamily, cancellationToken).ConfigureAwait(false);
        var binding = addressChooser.Binding;
        return await inner.DialFromAsync(endPoint, new IPEndPoint(localAddress, binding.FirstPort), binding.PortCount, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        inner.DialUnixSocketAsync(address, cancellationToken);
}
