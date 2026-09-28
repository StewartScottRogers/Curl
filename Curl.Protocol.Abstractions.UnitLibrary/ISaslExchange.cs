namespace Curl.Protocol.Abstractions;

/// <summary>
/// One SASL authentication exchange for one mechanism, begun by
/// <see cref="ISaslAuthenticator.Begin" /> (ADR-0121).
/// </summary>
/// <remarks>
/// It works in raw bytes. Base64, the <c>334</c> and <c>+</c> prefixes, <c>=</c> for an empty
/// initial response and the line-length rules are each handler's, because they differ per
/// protocol.
/// </remarks>
public interface ISaslExchange
{
    /// <summary>
    /// Gets the SASL name of the mechanism this exchange runs.
    /// </summary>
    string Mechanism { get; }

    /// <summary>
    /// Gets the initial response (RFC 4422 section 3.3), or <see langword="null" /> when the
    /// mechanism has none (<c>LOGIN</c>, <c>CRAM-MD5</c>, <c>DIGEST-MD5</c>). An empty array is
    /// a present but empty response.
    /// </summary>
    byte[]? InitialResponse { get; }

    /// <summary>
    /// Answers one server challenge.
    /// </summary>
    /// <param name="challenge">The challenge, already decoded from base64 by the handler.</param>
    /// <returns>
    /// The response, before base64; or <see langword="null" /> when the exchange cannot
    /// answer, so the handler cancels with <c>*</c> and fails with exit 67.
    /// </returns>
    byte[]? Respond(ReadOnlySpan<byte> challenge);
}
