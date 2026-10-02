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
    /// <remarks>
    /// The <c>-v</c> lines libcurl writes as it chooses go to <paramref name="events" /> (BL-1027): the
    /// <c>Local Interface ... is ip ...</c> line for an interface found (BL-1079), the
    /// <c>Name ... resolved to</c> line for a host resolved, and the <c>Could not ...</c> lines for a host
    /// or an <c>if!</c> interface that gives no address. A family mismatch writes nothing more, as in curl.
    /// </remarks>
    /// <param name="family">The family of the address dialled.</param>
    /// <param name="events">Where the <c>-v</c> lines go.</param>
    /// <param name="cancellationToken">Cancels a host name's resolve.</param>
    /// <returns>The local address to bind.</returns>
    /// <exception cref="LocalBindException">No local address can be chosen, as its <see cref="LocalBindException.Failure" /> says.</exception>
    public async ValueTask<IPAddress> ChooseAsync(AddressFamily family, ITransferEvents events, CancellationToken cancellationToken)
    {
        if (binding.DeviceName is { Length: > LocalBinding.LongestDeviceName })
        {
            throw new LocalBindException(LocalBindFailure.BadArgument);
        }

        if (InterfaceAddress(family) is { } interfaceAddress)
        {
            ReportInterfaceFound(interfaceAddress, events);
            return interfaceAddress;
        }

        if (binding.HostName is { } hostName)
        {
            return await ResolveHostAsync(hostName, family, events, cancellationToken).ConfigureAwait(false);
        }

        if (binding.InterfaceName is { } interfaceName)
        {
            events.ReportInfo(LocalBindLines.CouldNotBindInterface(interfaceName, OperatingSystem.IsWindows()));
            throw new LocalBindException(LocalBindFailure.InterfaceFailed);
        }

        return UnspecifiedAddress(family);
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

    /// <summary>
    /// Writes libcurl's <c>Local Interface ... is ip ...</c> line for the interface address found and, for a
    /// plain name (a host name too), the <c>Name ... resolved to</c> line of that address resolved as the
    /// host; an <c>if!</c> name writes the first line alone (measured, BL-1079 Notes).
    /// </summary>
    private void ReportInterfaceFound(IPAddress interfaceAddress, ITransferEvents events)
    {
        var onWindows = OperatingSystem.IsWindows();
        var onLinux = OperatingSystem.IsLinux();
        events.ReportInfo(LocalBindLines.LocalInterface(binding.InterfaceName!, interfaceAddress, onWindows, onLinux));
        if (binding.HostName is not null)
        {
            events.ReportInfo(LocalBindLines.NameResolved(interfaceAddress.ToString(), interfaceAddress.AddressFamily, interfaceAddress, onWindows, onLinux));
        }
    }

    private static IPAddress UnspecifiedAddress(AddressFamily family) =>
        family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;

    /// <summary>
    /// Resolves <paramref name="hostName" /> as libcurl's <c>bindlocal</c> does, whatever <c>-4</c> or
    /// <c>-6</c> says: an address as written, <c>localhost</c> as <c>::1</c> then <c>127.0.0.1</c>, any
    /// other name through the resolver. The first address is bound; none is
    /// <see cref="LocalBindFailure.InterfaceFailed" />, and one of the other family
    /// <see cref="LocalBindFailure.AddressFamilyMismatch" /> (measured: <c>host!localhost</c> and
    /// <c>::1</c> to <c>127.0.0.1</c> are exit 7). Either way curl's line for it goes to <paramref name="events" />.
    /// </summary>
    private async ValueTask<IPAddress> ResolveHostAsync(string hostName, AddressFamily family, ITransferEvents events, CancellationToken cancellationToken)
    {
        var addresses = await LookUpAsync(hostName, cancellationToken).ConfigureAwait(false);
        if (addresses.Count == 0)
        {
            events.ReportInfo(LocalBindLines.CouldNotResolveHost(hostName));
            events.ReportInfo(LocalBindLines.CouldNotBindHost(hostName, OperatingSystem.IsWindows()));
            throw new LocalBindException(LocalBindFailure.InterfaceFailed);
        }

        events.ReportInfo(LocalBindLines.NameResolved(hostName, family, addresses[0], OperatingSystem.IsWindows(), OperatingSystem.IsLinux()));
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
