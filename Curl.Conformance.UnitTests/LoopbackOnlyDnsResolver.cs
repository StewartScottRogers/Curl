using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Resolves names as the upstream test suite's machine does: an address literal is itself,
/// <c>localhost</c> and every name under <c>.localhost</c> are ::1 and 127.0.0.1, as curl answers them
/// itself, and every other name does not resolve, so curl exits 6 for it.
/// </summary>
internal sealed class LoopbackOnlyDnsResolver : IDnsResolver
{
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        IReadOnlyList<IPAddress> addresses =
            IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal) ? [literal]
            : IsLocalhost(host) ? [IPAddress.IPv6Loopback, IPAddress.Loopback]
            : [];
        return ValueTask.FromResult(addresses);
    }

    private static bool IsLocalhost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
}
