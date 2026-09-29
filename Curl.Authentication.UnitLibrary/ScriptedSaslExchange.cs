using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// A SASL exchange whose messages do not depend on the server's challenges: an initial
/// response, then a fixed list of answers, one per challenge, in order (ADR-0123).
/// </summary>
/// <param name="mechanism">The SASL name of the mechanism.</param>
/// <param name="initialResponse">
/// The initial response. The handler sends it on the command line under <c>--sasl-ir</c>, and
/// otherwise in answer to the server's first challenge, as RFC 4422 section 5 describes.
/// </param>
/// <param name="answers">
/// The answers to the challenges that follow the initial response; once they run out,
/// <see cref="RespondAsync" /> answers <see langword="null" />, as curl fails a challenge it
/// does not expect.
/// </param>
internal sealed class ScriptedSaslExchange(string mechanism, byte[] initialResponse, params byte[][] answers) : ISaslExchange
{
    private int answersGiven;

    /// <inheritdoc />
    public string Mechanism => mechanism;

    /// <inheritdoc />
    public ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<byte[]?>(initialResponse);

    /// <inheritdoc />
    public ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken) =>
        ValueTask.FromResult(answersGiven < answers.Length ? answers[answersGiven++] : null);
}
