namespace Curl.Protocol.Abstractions;

/// <summary>
/// One SASL authentication exchange for one mechanism, begun by
/// <see cref="ISaslAuthenticator.Begin" /> (ADR-0121, ADR-0183).
/// </summary>
/// <remarks>
/// It works in raw bytes. Base64, the <c>334</c> and <c>+</c> prefixes, <c>=</c> for an empty
/// initial response and the line-length rules are each handler's, because they differ per
/// protocol. Each message is awaited, because a mechanism answered through an
/// <see cref="ISecurityContext" /> (GSSAPI, NTLM) may ask a KDC for a ticket to make it.
/// </remarks>
public interface ISaslExchange
{
    /// <summary>
    /// Gets the SASL name of the mechanism this exchange runs.
    /// </summary>
    string Mechanism { get; }

    /// <summary>
    /// Makes the initial response (RFC 4422 section 3.3). The handler asks for it once, before
    /// it sends the command that names the mechanism.
    /// </summary>
    /// <param name="cancellationToken">Cancels any exchange the mechanism makes to build it.</param>
    /// <returns>
    /// The initial response, or <see langword="null" /> when the mechanism has none
    /// (<c>LOGIN</c> without <c>--sasl-ir</c>, <c>CRAM-MD5</c>, <c>DIGEST-MD5</c>). An empty
    /// array is a present but empty response.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Answers one server challenge.
    /// </summary>
    /// <param name="challenge">The challenge, already decoded from base64 by the handler.</param>
    /// <param name="cancellationToken">Cancels any exchange the mechanism makes to answer.</param>
    /// <returns>
    /// The response, before base64; or <see langword="null" /> when the exchange cannot
    /// answer, so the handler cancels with <c>*</c> and fails with exit 67.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken);
}
