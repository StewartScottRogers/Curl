using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3.Fakes;

/// <summary>
/// An <see cref="ISaslAuthenticator" /> that builds only the mechanisms it is given, ranked in
/// the order given, each answering with fixed Latin-1 messages: the initial response first,
/// then one answer per later challenge, then <see langword="null" />, as
/// <c>Curl.Authentication</c>'s exchanges do (ADR-0123).
/// </summary>
/// <param name="mechanisms">Each mechanism's SASL name and its messages, best first.</param>
public sealed class ScriptedSaslAuthenticator(params (string Mechanism, string[] Messages)[] mechanisms) : ISaslAuthenticator
{
    /// <summary>Gets every request <see cref="ChooseMechanism" /> was given, in order.</summary>
    public List<SaslRequest> Requests { get; } = [];

    /// <summary>Gets every list of offered mechanisms <see cref="ChooseMechanism" /> was given, in order.</summary>
    public List<string[]> Offers { get; } = [];

    /// <summary>Gets every challenge an exchange was asked to answer, as Latin-1 text.</summary>
    public List<string> Challenges { get; } = [];

    /// <inheritdoc />
    public string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms)
    {
        Requests.Add(request);
        Offers.Add([.. offeredMechanisms]);
        return mechanisms
            .Select(built => built.Mechanism)
            .Where(name => request.RequiredMechanism is null || name.Equals(request.RequiredMechanism, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(name => offeredMechanisms.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public ISaslExchange Begin(string mechanism, SaslRequest request)
    {
        string[] messages = mechanisms.Single(built => built.Mechanism == mechanism).Messages;
        return new Exchange(this, mechanism, [.. messages.Select(Encoding.Latin1.GetBytes)]);
    }

    private sealed class Exchange(ScriptedSaslAuthenticator owner, string mechanism, byte[][] messages) : ISaslExchange
    {
        private int answersGiven;

        public string Mechanism => mechanism;

        public byte[]? InitialResponse => messages[0];

        public byte[]? Respond(ReadOnlySpan<byte> challenge)
        {
            owner.Challenges.Add(Encoding.Latin1.GetString(challenge));
            return ++answersGiven < messages.Length ? messages[answersGiven] : null;
        }
    }
}
