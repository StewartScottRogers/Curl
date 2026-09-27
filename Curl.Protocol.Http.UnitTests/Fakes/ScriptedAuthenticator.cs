using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that gives one scripted value before any challenge
/// and another in answer to any challenge, and records every call.
/// </summary>
/// <param name="beforeChallenge">The value for a call with no challenges, such as <c>Basic dTpw</c>.</param>
/// <param name="answer">The value for a call with challenges, such as a Digest response.</param>
public sealed class ScriptedAuthenticator(string? beforeChallenge, string? answer) : IHttpAuthenticator
{
    /// <summary>
    /// Gets every call made, in order: the request and the challenges it was given.
    /// </summary>
    public List<(HttpAuthRequest Request, IReadOnlyList<string> Challenges)> Calls { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
    {
        Calls.Add((request, challenges));
        return challenges.Count == 0 ? beforeChallenge : answer;
    }
}
