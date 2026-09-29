using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The resolver a <c>--doh-url</c> that is not an <c>http</c> or <c>https</c> URL leaves the run with:
/// it resolves every name to no address, so each transfer fails with exit 6
/// <c>Could not resolve host: &lt;host&gt;</c>, as curl 8.21.0 fails <c>--doh-url ftp://...</c> without
/// asking anyone (measured, BL-642).
/// </summary>
internal sealed class UnusableDohUrlResolver : IDnsResolver
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
}
