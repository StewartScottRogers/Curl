using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> that sends nothing before any challenge and answers
/// every challenge, fresh or continued, with the empty value (ADR-0232), and counts the calls,
/// so a test sees the handler send the request again without a header only once.
/// </summary>
public sealed class EmptyAnswerAuthenticator : IHttpAuthenticator
{
    /// <summary>Gets how many times <see cref="ContinueAuthorizationAsync" /> was called.</summary>
    public int Continuations { get; private set; }

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count == 0 ? null : string.Empty;

    /// <inheritdoc />
    public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        Continuations++;
        return ValueTask.FromResult<string?>(string.Empty);
    }
}
