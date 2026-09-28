using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that returns a fixed <c>Authorization</c> value and
/// records every request and challenge list it is asked about.
/// </summary>
/// <param name="authorization">The value returned, or <see langword="null" /> for no header.</param>
public sealed class RecordingAuthenticator(string? authorization = null) : IHttpAuthenticator
{
    /// <summary>Gets the requests asked about, in order.</summary>
    public List<HttpAuthRequest> Requests { get; } = [];

    /// <summary>Gets the challenge lists passed, in order.</summary>
    public List<IReadOnlyList<string>> Challenges { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        Requests.Add(request);
        Challenges.Add(challenges);
        return authorization;
    }
}
