using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// A SASL exchange with no initial response whose answers are computed from the server's
/// challenges, one function per challenge, in order: CRAM-MD5 and DIGEST-MD5.
/// </summary>
/// <param name="mechanism">The SASL name of the mechanism.</param>
/// <param name="answerChallenges">
/// One function per expected challenge, each given the decoded challenge and answering the
/// response, or <see langword="null" /> when curl would cancel. Once they run out,
/// <see cref="Respond" /> answers <see langword="null" />, as curl fails a challenge it does
/// not expect.
/// </param>
internal sealed class ChallengeSaslExchange(string mechanism, params Func<byte[], byte[]?>[] answerChallenges) : ISaslExchange
{
    private int challengesAnswered;

    /// <inheritdoc />
    public string Mechanism => mechanism;

    /// <inheritdoc />
    public byte[]? InitialResponse => null;

    /// <inheritdoc />
    public byte[]? Respond(ReadOnlySpan<byte> challenge) =>
        challengesAnswered < answerChallenges.Length ? answerChallenges[challengesAnswered++](challenge.ToArray()) : null;
}
