using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that chooses the first of its scripted mechanisms,
/// in the order given, that the server still offers, as curl's ranking would, and begins
/// that mechanism's scripted exchange, recording what the handler asked.
/// </summary>
/// <param name="scripts">Each mechanism with its initial response and its answers to the challenges after it.</param>
public sealed class RankedSaslAuthenticator(params (string Mechanism, byte[]? InitialResponse, byte[][] Answers)[] scripts) : ISaslAuthenticator
{
    /// <summary>Gets the offered list of every <see cref="ChooseMechanism" /> call.</summary>
    public List<string[]> Offers { get; } = [];

    /// <summary>Gets every challenge handed to an exchange, with its mechanism, in order.</summary>
    public List<(string Mechanism, byte[] Challenge)> Challenges { get; } = [];

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        Offers.Add([.. offeredMechanisms]);
        return scripts.Select(script => script.Mechanism)
            .FirstOrDefault(mechanism => offeredMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request) =>
        new Exchange(this, scripts.First(script => script.Mechanism == mechanism));

    private sealed class Exchange(RankedSaslAuthenticator owner, (string Mechanism, byte[]? InitialResponse, byte[][] Answers) script) : ISaslExchange
    {
        private int answersGiven;

        public string Mechanism => script.Mechanism;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) => ValueTask.FromResult(script.InitialResponse);

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken)
        {
            owner.Challenges.Add((script.Mechanism, challenge.ToArray()));
            return ValueTask.FromResult(answersGiven < script.Answers.Length ? script.Answers[answersGiven++] : null);
        }
    }
}
