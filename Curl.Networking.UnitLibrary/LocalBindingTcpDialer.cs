using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITcpDialer" /> that binds each TCP connection's local end to what a
/// <see cref="LocalBinding" /> asks before it connects, choosing the local address for the family
/// of each address dialled as libcurl's <c>bindlocal</c> chooses it (measured on curl 8.21.0,
/// BL-600 Notes), and dials Unix domain sockets unbound, as libcurl binds only IP sockets.
/// </summary>
/// <param name="inner">Binds and connects each socket.</param>
/// <param name="binding">What to bind to.</param>
/// <param name="interfaces">Finds an interface's addresses; the Windows build finds none (ADR-0110).</param>
/// <param name="dnsResolver">Resolves <see cref="LocalBinding.HostName" /> when it is not an address.</param>
internal sealed class LocalBindingTcpDialer(
    ITcpDialer inner,
    LocalBinding binding,
    INetworkInterfaceLookup interfaces,
    IDnsResolver dnsResolver) : ITcpDialer
{
    private static readonly IPAddress[] LocalhostAddresses = [IPAddress.IPv6Loopback, IPAddress.Loopback];

    /// <inheritdoc />
    /// <exception cref="LocalBindException">The local end could not be bound, as its <see cref="LocalBindException.Failure" /> says.</exception>
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        var localAddress = await ChooseLocalAddressAsync(endPoint.AddressFamily, cancellationToken).ConfigureAwait(false);
        return await inner.DialFromAsync(endPoint, new IPEndPoint(localAddress, binding.FirstPort), binding.PortCount, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        inner.DialFromAsync(endPoint, localEndPoint, localPortCount, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
        inner.DialUnixSocketAsync(address, cancellationToken);

    /// <summary>
    /// Chooses the local address for a dial to an address of <paramref name="family" />: an
    /// <c>ifhost!</c> interface part too long is <see cref="LocalBindFailure.BadArgument" />; an interface
    /// <see cref="LocalBinding.InterfaceName" /> names gives its first address of the family, or
    /// <see cref="LocalBindFailure.AddressFamilyMismatch" /> when it has none; a name that is no interface
    /// is <see cref="LocalBindFailure.InterfaceFailed" /> after <c>if!</c> and is resolved as a host
    /// otherwise; with neither name the unspecified address of the family, so only the port is bound.
    /// </summary>
    private async ValueTask<IPAddress> ChooseLocalAddressAsync(AddressFamily family, CancellationToken cancellationToken)
    {
        if (binding.DeviceName is { Length: > LocalBinding.LongestDeviceName })
        {
            throw new LocalBindException(LocalBindFailure.BadArgument);
        }

        if (InterfaceAddress(family) is { } interfaceAddress)
        {
            return interfaceAddress;
        }

        if (binding.HostName is { } hostName)
        {
            return await ResolveHostAsync(hostName, family, cancellationToken).ConfigureAwait(false);
        }

        return binding.InterfaceName is null
            ? UnspecifiedAddress(family)
            : throw new LocalBindException(LocalBindFailure.InterfaceFailed);
    }

    /// <summary>
    /// The first address of <paramref name="family" /> on the interface <see cref="LocalBinding.InterfaceName" />
    /// names, or <see langword="null" /> when it names none the lookup finds; an interface with no address of
    /// the family is <see cref="LocalBindFailure.AddressFamilyMismatch" />, libcurl's <c>IF2IP_AF_NOT_SUPPORTED</c>.
    /// </summary>
    private IPAddress? InterfaceAddress(AddressFamily family)
    {
        if (binding.InterfaceName is not { } interfaceName || interfaces.FindAddresses(interfaceName) is not { } addresses)
        {
            return null;
        }

        return addresses.FirstOrDefault(address => address.AddressFamily == family)
            ?? throw new LocalBindException(LocalBindFailure.AddressFamilyMismatch);
    }

    private static IPAddress UnspecifiedAddress(AddressFamily family) =>
        family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

    /// <summary>
    /// Resolves <paramref name="hostName" /> as libcurl's <c>bindlocal</c> does, whatever <c>-4</c> or
    /// <c>-6</c> says: an address as written, <c>localhost</c> as <c>::1</c> then <c>127.0.0.1</c>, any
    /// other name through the resolver. The first address is bound; none is
    /// <see cref="LocalBindFailure.InterfaceFailed" />, and one of the other family
    /// <see cref="LocalBindFailure.AddressFamilyMismatch" /> (measured: <c>host!localhost</c> and
    /// <c>::1</c> to <c>127.0.0.1</c> are exit 7).
    /// </summary>
    private async ValueTask<IPAddress> ResolveHostAsync(string hostName, AddressFamily family, CancellationToken cancellationToken)
    {
        var addresses = await LookUpAsync(hostName, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            throw new LocalBindException(LocalBindFailure.InterfaceFailed);
        }

        return addresses[0].AddressFamily == family
            ? addresses[0]
            : throw new LocalBindException(LocalBindFailure.AddressFamilyMismatch);
    }

    private async ValueTask<IReadOnlyList<IPAddress>> LookUpAsync(string hostName, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(hostName, out var address))
        {
            return [address];
        }

        if (TcpConnector.IsLocalhost(hostName))
        {
            return LocalhostAddresses;
        }

        return string.IsNullOrWhiteSpace(hostName)
            ? []
            : await dnsResolver.ResolveAsync(hostName, cancellationToken).ConfigureAwait(false);
    }
}
