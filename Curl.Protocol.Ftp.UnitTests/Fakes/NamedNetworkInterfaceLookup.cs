using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="INetworkInterfaceLookup" /> that knows the interfaces in
/// <paramref name="interfaces" /> and no other, and records every name it was asked for.
/// </summary>
/// <param name="interfaces">Each interface name and its addresses, in order.</param>
public sealed class NamedNetworkInterfaceLookup(IReadOnlyDictionary<string, IPAddress[]> interfaces) : INetworkInterfaceLookup
{
    /// <summary>Gets every name asked for, in order.</summary>
    public List<string> Names { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IPAddress>? FindAddresses(string interfaceName)
    {
        Names.Add(interfaceName);
        return interfaces.TryGetValue(interfaceName, out IPAddress[]? addresses) ? addresses : null;
    }
}
