using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>What every flow asks the token endpoint for: the endpoint, the client and the scope (RFC 6749 §3.2, §3.3).</summary>
/// <param name="TokenEndpoint">The token endpoint.</param>
/// <param name="Client">The client and how it authenticates.</param>
/// <param name="Scope">The scope asked for, or <see langword="null"/> to ask for none.</param>
public sealed record OAuth2FlowRequest(Uri TokenEndpoint, OAuth2Client Client, string? Scope)
{
    /// <summary>Gets the key the token this request gets is cached under (ADR-1034).</summary>
    public OAuth2TokenCacheKey CacheKey => new(TokenEndpoint, Client.ClientId, Scope);
}
