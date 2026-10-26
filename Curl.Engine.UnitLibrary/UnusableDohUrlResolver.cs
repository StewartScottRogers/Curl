using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The resolver a <c>--doh-url</c> that is not an <c>http</c> or <c>https</c> URL leaves the run with:
/// it answers an IP literal with itself and <c>localhost</c> and every name under <c>.localhost</c>
/// with <c>::1</c> and <c>127.0.0.1</c>, as <c>DohDnsResolver</c> does without a query, and every other
/// name with no address, so its transfer fails with exit 6 <c>Could not resolve host: &lt;host&gt;</c>,
/// as curl 8.22.0 does with <c>--doh-url "a b"</c> and <c>--doh-url ftp://...</c> without asking anyone
/// (measured, BL-642, CGR-045).
/// </summary>
internal sealed class UnusableDohUrlResolver : IDnsResolver
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AddressesOf(host));

    // The literal itself, the loopback pair for localhost, or nothing.
    private static IReadOnlyList<IPAddress> AddressesOf(string host) =>
        IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal]
        : IsLocalhost(host) ? [IPAddress.IPv6Loopback, IPAddress.Loopback]
        : [];

    // curl answers localhost and every name under .localhost itself (TcpConnector.IsLocalhost).
    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
}
