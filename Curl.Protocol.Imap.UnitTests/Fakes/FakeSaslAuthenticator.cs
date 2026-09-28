using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that chooses <paramref name="mechanism" /> whenever it
/// is offered and the request requires no other mechanism, and begins an exchange that
/// gives <paramref name="initialResponse" />, then <paramref name="answers" /> one per
/// challenge and <see langword="null" /> after them, recording what the handler asked.
/// </summary>
/// <param name="mechanism">The one mechanism this authenticator can use.</param>
/// <param name="initialResponse">The exchange's initial response.</param>
/// <param name="answers">The exchange's answers to the challenges after it.</param>
public sealed class FakeSaslAuthenticator(string mechanism, byte[]? initialResponse, params byte[][] answers) : ISaslAuthenticator
{
    private readonly byte[]? scriptedInitialResponse = initialResponse;

    private readonly byte[][] scriptedAnswers = answers;

    /// <summary>Gets the request and offered list of every <see cref="ChooseMechanism" /> call.</summary>
    public List<(SaslRequest Request, string[] Offered)> Choices { get; } = [];

    /// <summary>Gets the request every exchange was begun with.</summary>
    public List<SaslRequest> Begun { get; } = [];

    /// <summary>Gets every challenge handed to the exchange, in order.</summary>
    public List<byte[]> Challenges { get; } = [];

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        Choices.Add((request, [.. offeredMechanisms]));
        bool required = request.RequiredMechanism is null || string.Equals(request.RequiredMechanism, mechanism, StringComparison.OrdinalIgnoreCase);
        return required && offeredMechanisms.Contains(mechanism, StringComparer.OrdinalIgnoreCase) ? mechanism : null;
    }

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request)
    {
        Begun.Add(request);
        return new Exchange(this, mechanism);
    }

    private sealed class Exchange(FakeSaslAuthenticator owner, string mechanism) : ISaslExchange
    {
        private int answersGiven;

        public string Mechanism => mechanism;

        public byte[]? InitialResponse => owner.scriptedInitialResponse;

        public byte[]? Respond(ReadOnlySpan<byte> challenge)
        {
            owner.Challenges.Add(challenge.ToArray());
            return answersGiven < owner.scriptedAnswers.Length ? owner.scriptedAnswers[answersGiven++] : null;
        }
    }
}
