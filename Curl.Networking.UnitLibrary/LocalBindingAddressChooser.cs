using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Chooses the local address a connection's socket binds to for a <see cref="LocalBinding" />, per
/// family of the address dialled, as libcurl's <c>bindlocal</c> chooses it (measured on curl 8.21.0,
/// BL-600 Notes): the TCP dials of <see cref="LocalBindingTcpDialer" /> and the UDP sockets of
/// <see cref="QuicDialer" /> (BL-1025) both bind through it.
/// </summary>
/// <param name="binding">What to bind to.</param>
/// <param name="interfaces">Finds an interface's addresses; the Windows build finds none (ADR-0110).</param>
/// <param name="dnsResolver">Resolves <see cref="LocalBinding.HostName" /> when it is not an address.</param>
internal sealed class LocalBindingAddressChooser(
    LocalBinding binding,
    INetworkInterfaceLookup interfaces,
    IDnsResolver dnsResolver)
{
    private static readonly IPAddress[] LocalhostAddresses = [IPAddress.IPv6Loopback, IPAddress.Loopback];

    /// <summary>Gets what to bind to, whose ports the caller binds.</summary>
    public LocalBinding Binding => binding;

    /// <summary>
    /// Chooses the local address for a dial to an address of <paramref name="family" />: an
    /// <c>ifhost!</c> interface part too long is <see cref="LocalBindFailure.BadArgument" />; an interface
    /// <see cref="LocalBinding.InterfaceName" /> names gives its first address of the family, or
    /// <see cref="LocalBindFailure.AddressFamilyMismatch" /> when it has none; a name that is no interface
    /// is <see cref="LocalBindFailure.InterfaceFailed" /> after <c>if!</c> and is resolved as a host
    /// otherwise; with neither name the unspecified address of the family, so only the port is bound.
    /// </summary>
    /// <param name="family">The family of the address dialled.</param>
    /// <param name="cancellationToken">Cancels a host name's resolve.</param>
    /// <returns>The local address to bind.</returns>
    /// <exception cref="LocalBindException">No local address can be chosen, as its <see cref="LocalBindException.Failure" /> says.</exception>
    public async ValueTask<IPAddress> ChooseAsync(AddressFamily family, CancellationToken cancellationToken)
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
