using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="IHttpAuthenticator" /> for a handshake of more than one leg: it gives one
/// scripted value before any challenge and hands out <paramref name="continuations" /> in
/// turn to <see cref="ContinueAuthorizationAsync" />, a <see langword="null" /> in the list
/// answering nothing, or throws <paramref name="failure" /> there instead; it records every
/// continuation asked for.
/// </summary>
/// <param name="beforeChallenge">The value for the first request, such as an NTLM Type 1 message.</param>
/// <param name="continuations">The values for successive continuations; past the last, nothing.</param>
/// <param name="failure">Thrown by every continuation instead, when not <see langword="null" />.</param>
public sealed class HandshakeAuthenticator(string? beforeChallenge, IReadOnlyList<string?> continuations, HttpAuthenticationFailedException? failure = null) : IHttpAuthenticator
{
    /// <summary>Gets every continuation asked for, in order: the value sent, whether it was sent before any challenge, and the challenges.</summary>
    public List<(string Sent, bool SentBeforeAnyChallenge, IReadOnlyList<string> Challenges)> Continuations { get; } = [];

    /// <inheritdoc />
    public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
        challenges.Count == 0 ? beforeChallenge : null;

    /// <inheritdoc />
    public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken)
    {
        Continuations.Add((sentAuthorization, sentBeforeAnyChallenge, challenges));
        if (failure is not null)
        {
            throw failure;
        }

        int index = Continuations.Count - 1;
        return ValueTask.FromResult(index < continuations.Count ? continuations[index] : null);
    }
}
