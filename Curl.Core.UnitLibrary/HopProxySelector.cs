using System.Diagnostics.CodeAnalysis;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Chooses the proxy one <see cref="RedirectFollower" /> hop goes through, from that hop's own
/// URL, as curl 8.21.0 chooses again for each redirect hop.
/// </summary>
/// <param name="url">The hop's URL.</param>
/// <param name="proxy">The proxy, or <see langword="null" /> to connect directly or on failure.</param>
/// <param name="failure">The failure that ends the chain; <see langword="null" /> otherwise.</param>
/// <returns><see langword="true" /> unless the chain must end with <paramref name="failure" />.</returns>
public delegate bool HopProxySelector(
    CurlUrl url,
    out ProxyEndpoint? proxy,
    [NotNullWhen(false)] out TransferResult? failure);
