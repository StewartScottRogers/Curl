using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that gives nothing before any challenge, a scripted
/// value in answer to any challenge, and, asked to send a value again, that value followed by
/// <c> again for</c>, the method and the request target, so a test sees which request it was
/// counted on for.
/// </summary>
/// <param name="answer">The value for a call with challenges, such as a Digest response.</param>
public sealed class RepeatingAuthenticator(string? answer) : IHttpAuthenticator
{
    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count == 0 ? null : answer;

    /// <inheritdoc />
    public string RepeatAuthorization(HttpAuthRequest request, string sentAuthorization) =>
        $"{sentAuthorization} again for {request.Method} {request.RequestTarget}";
}
