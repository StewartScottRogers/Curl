using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that chooses <paramref name="mechanism" /> whenever it
/// is offered and begins an exchange with no initial response that rejects every challenge
/// with a <see cref="SaslAuthenticationFailedException" /> for exit 94, as the Schannel build's
/// DIGEST-MD5 does for a challenge SSPI rejects (BL-781); or, with
/// <paramref name="failsInitialResponse" />, that fails when asked for its initial response,
/// as a GSSAPI or NTLM security context without credentials does (BL-856).
/// </summary>
/// <param name="mechanism">The one mechanism this authenticator can use.</param>
/// <param name="failsInitialResponse">Whether the exchange fails when asked for its initial response.</param>
public sealed class RejectingSaslAuthenticator(string mechanism, bool failsInitialResponse = false) : ISaslAuthenticator
{
    /// <summary>Gets every challenge handed to the exchange, in order.</summary>
    public List<byte[]> Challenges { get; } = [];

    /// <summary>Gets how many times the exchange was asked for its initial response.</summary>
    public int InitialResponsesAsked { get; private set; }

    private bool FailsInitialResponse => failsInitialResponse;

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        offeredMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase) ? mechanism : null;

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request) => new Exchange(this, mechanism);

    private sealed class Exchange(RejectingSaslAuthenticator owner, string mechanism) : ISaslExchange
    {
        public string Mechanism => mechanism;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken)
        {
            owner.InitialResponsesAsked++;
            return owner.FailsInitialResponse
                ? throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function returned an error")
                : ValueTask.FromResult<byte[]?>(null);
        }

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken)
        {
            owner.Challenges.Add(challenge.ToArray());
            throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function returned an error");
        }
    }
}
