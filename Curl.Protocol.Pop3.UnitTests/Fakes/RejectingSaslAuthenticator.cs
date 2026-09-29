using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that chooses <paramref name="mechanism" /> whenever it
/// is offered and begins an exchange with no initial response that rejects every challenge
/// with a <see cref="SaslAuthenticationFailedException" /> for exit 94, as the Schannel build's
/// DIGEST-MD5 does for a challenge SSPI rejects (BL-781).
/// </summary>
/// <param name="mechanism">The one mechanism this authenticator can use.</param>
public sealed class RejectingSaslAuthenticator(string mechanism) : ISaslAuthenticator
{
    /// <summary>Gets every challenge handed to the exchange, in order.</summary>
    public List<byte[]> Challenges { get; } = [];

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms) =>
        offeredMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase) ? mechanism : null;

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request) => new Exchange(this, mechanism);

    private sealed class Exchange(RejectingSaslAuthenticator owner, string mechanism) : ISaslExchange
    {
        public string Mechanism => mechanism;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken)
        {
            owner.Challenges.Add(challenge.ToArray());
            throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function returned an error");
        }
    }
}
