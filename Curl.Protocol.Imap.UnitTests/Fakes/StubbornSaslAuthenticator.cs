using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that chooses <paramref name="mechanism" /> every time,
/// offered or not, and begins an exchange with no initial response that cannot answer, to
/// prove the handler stops rather than choosing the same mechanism forever.
/// </summary>
/// <param name="mechanism">The mechanism it always chooses.</param>
public sealed class StubbornSaslAuthenticator(string mechanism) : ISaslAuthenticator
{
    /// <summary>Gets how many times <see cref="ChooseMechanism" /> was called.</summary>
    public int ChoicesMade { get; private set; }

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        ChoicesMade++;
        return mechanism;
    }

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request) => new Exchange(mechanism);

    private sealed class Exchange(string mechanism) : ISaslExchange
    {
        public string Mechanism => mechanism;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);
    }
}
