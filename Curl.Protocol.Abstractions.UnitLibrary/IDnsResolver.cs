using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Resolves a host name to addresses.
/// </summary>
/// <remarks>
/// Injected rather than called statically so that resolution can be faked in a test,
/// and so that curl's <c>--resolve</c> and <c>--connect-to</c> overrides have one
/// place to take effect.
/// </remarks>
public interface IDnsResolver
{
    /// <summary>
    /// Resolves <paramref name="host" /> to one or more addresses.
    /// </summary>
    /// <param name="host">The host name or literal address to resolve.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>
    /// The addresses for <paramref name="host" />, in the order they should be tried.
    /// </returns>
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
        string host,
        CancellationToken cancellationToken);
}
