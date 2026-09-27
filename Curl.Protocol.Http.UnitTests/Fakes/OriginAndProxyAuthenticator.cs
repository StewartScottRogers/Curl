using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that answers the origin and a proxy with different
/// scripted values and records every call: the fake for a transfer through a forward proxy,
/// which sends both <c>Authorization</c> and <c>Proxy-Authorization</c>.
/// </summary>
/// <param name="origin">
/// The value for the origin, <see cref="HttpAuthRequest.IsProxy" /> <see langword="false" />,
/// before any challenge.
/// </param>
/// <param name="originAnswer">The value for the origin in answer to a challenge.</param>
/// <param name="proxy">
/// The value for a proxy, <see cref="HttpAuthRequest.IsProxy" /> <see langword="true" />,
/// whatever the challenges.
/// </param>
public sealed class OriginAndProxyAuthenticator(string? origin, string? originAnswer, string? proxy) : IHttpAuthenticator
{
    /// <summary>
    /// Gets every call, in order, with the challenges it was given.
    /// </summary>
    public List<(HttpAuthRequest Request, IReadOnlyList<string> Challenges)> Calls { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        Calls.Add((request, challenges));
        if (request.IsProxy)
        {
            return proxy;
        }

        return challenges.Count == 0 ? origin : originAnswer;
    }
}
