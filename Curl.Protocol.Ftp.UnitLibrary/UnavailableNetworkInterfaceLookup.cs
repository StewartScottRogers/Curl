using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The <see cref="INetworkInterfaceLookup" /> of an <see cref="FtpProtocolHandler" /> built
/// without one: it finds no interface, so every name given to <c>-P</c> is resolved as a host
/// name, as the Windows (Schannel) build of curl 8.21.0 resolves it (ADR-0108, ADR-0110).
/// </summary>
internal sealed class UnavailableNetworkInterfaceLookup : INetworkInterfaceLookup
{
    /// <summary>The one instance.</summary>
    public static readonly UnavailableNetworkInterfaceLookup Instance = new();

    private UnavailableNetworkInterfaceLookup()
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<IPAddress>? FindAddresses(string interfaceName) => null;
}
