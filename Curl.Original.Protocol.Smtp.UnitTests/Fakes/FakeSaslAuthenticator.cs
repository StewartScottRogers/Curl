using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Smtp.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that always chooses <paramref name="mechanism" /> and
/// begins an exchange that gives <paramref name="initialResponse" />, then
/// <paramref name="answers" /> one per challenge and <see langword="null" /> after them,
/// recording what the handler asked.
/// </summary>
/// <param name="mechanism">The mechanism to choose, or <see langword="null" /> for none usable.</param>
/// <param name="initialResponse">The exchange's initial response.</param>
/// <param name="answers">The exchange's answers to the challenges after it.</param>
public sealed class FakeSaslAuthenticator(string? mechanism, byte[]? initialResponse, params byte[][] answers) : ISaslAuthenticator
{
    private readonly byte[]? scriptedInitialResponse = initialResponse;

    private readonly byte[][] scriptedAnswers = answers;

    /// <summary>Gets the offered list and request of every <see cref="ChooseMechanism" /> call.</summary>
    public List<(SaslRequest Request, string[] Offered)> Choices { get; } = [];

    /// <summary>Gets every challenge handed to the exchange, in order.</summary>
    public List<byte[]> Challenges { get; } = [];

    /// <summary>Gets what each exchange reports as <see cref="ISaslExchange.SendsLastAnswerAsEmptyLine" />.</summary>
    public bool SendsLastAnswerAsEmptyLine { get; init; }

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        Choices.Add((request, [.. offeredMechanisms]));
        return mechanism;
    }

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request) => new Exchange(this, mechanism);

    private sealed class Exchange(FakeSaslAuthenticator owner, string mechanism) : ISaslExchange
    {
        private int answersGiven;

        public string Mechanism => mechanism;

        public bool SendsLastAnswerAsEmptyLine => owner.SendsLastAnswerAsEmptyLine;

        public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) => ValueTask.FromResult(owner.scriptedInitialResponse);

        public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken)
        {
            owner.Challenges.Add(challenge.ToArray());
            return ValueTask.FromResult(answersGiven < owner.scriptedAnswers.Length ? owner.scriptedAnswers[answersGiven++] : null);
        }
    }
}
