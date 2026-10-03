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
    /// Makes the initial response (RFC 4422 section 3.3). The handler asks for it once: before
    /// it sends the command that names the mechanism when the response may go on that command
    /// (<c>--sasl-ir</c>, or IMAP's <c>SASL-IR</c> capability), and otherwise when the server's
    /// first challenge arrives, as curl's <c>Curl_sasl_start</c> does (BL-856).
    /// </summary>
    /// <remarks>
    /// The handler sends it on that command when it fits, and otherwise in answer to the
    /// server's first challenge (RFC 4422 section 5, ADR-0123); <see cref="RespondAsync" />
    /// answers the challenges after that.
    /// </remarks>
    /// <param name="cancellationToken">Cancels any exchange the mechanism makes to build it.</param>
    /// <returns>
    /// The initial response, or <see langword="null" /> when the mechanism has none and answers
    /// the server's first challenge instead (<c>CRAM-MD5</c>, <c>DIGEST-MD5</c>). <c>LOGIN</c>
    /// has one, the user name. An empty array is a present but empty response.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    /// <exception cref="SaslAuthenticationFailedException">
    /// The mechanism cannot make its initial response and curl fails the transfer with the
    /// exception's exit code, sending nothing more, as curl 8.21.0 does with exit 94 for a
    /// GSSAPI or NTLM security context without credentials (BL-856).
    /// </exception>
    ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Answers one server challenge.
    /// </summary>
    /// <remarks>
    /// When the mechanism has an initial response that was not sent on the command, the
    /// handler sends it in answer to the first challenge instead of calling this, and calls
    /// this for the challenges after it (RFC 4422 section 5, ADR-0123): <c>LOGIN</c>'s
    /// password, and <c>OAUTHBEARER</c>'s single <c>0x01</c> byte acknowledging an error.
    /// </remarks>
    /// <param name="challenge">The challenge, already decoded from base64 by the handler.</param>
    /// <param name="cancellationToken">Cancels any exchange the mechanism makes to answer.</param>
    /// <returns>
    /// The response, before base64; or <see langword="null" /> when the exchange cannot
    /// answer. With <see cref="CancelReason" /> <see langword="null" />, that fails the
    /// transfer with exit 67, and whether a cancel line (<c>*</c>) is sent first is each
    /// handler's decision (ADR-0133 decision 6). With <see cref="CancelReason" /> set, the
    /// handler writes it as a <c>-v</c> info line and cancels the exchange as it does for a
    /// challenge that is not base64, going on to the next mechanism.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was cancelled.</exception>
    /// <exception cref="SaslAuthenticationFailedException">
    /// An authentication function rejected the challenge and curl fails the transfer with the
    /// exception's exit code, sending nothing more, as the Schannel build does with exit 94 for
    /// a DIGEST-MD5 challenge SSPI rejects (BL-781).
    /// </exception>
    ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte> challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the <c>-v</c> info line curl writes before it cancels this exchange, set when
    /// <see cref="RespondAsync" /> returned <see langword="null" /> because curl 8.21.0 would
    /// cancel the exchange rather than fail the transfer; <see langword="null" /> otherwise.
    /// </summary>
    /// <remarks>
    /// curl 8.21.0, <c>lib/curl_sasl.c</c> lines 789-793: a mechanism step that returns
    /// <c>CURLE_BAD_CONTENT_ENCODING</c> makes curl call the protocol's <c>cancelauth</c> and
    /// move to <c>SASL_CANCEL</c>, which drops the mechanism and starts the next one. The
    /// GSSAPI security-layer step does that after writing a line such as
    /// <c>GSSAPI handshake failure (invalid security layer)</c>. A <see langword="null" />
    /// answer with a non-null reason means "write this line, then cancel as for an undecodable
    /// challenge"; a <see langword="null" /> answer with a <see langword="null" /> reason
    /// still fails the transfer with exit 67. An implementation that does not override it
    /// reports <see langword="null" />.
    /// </remarks>
    string? CancelReason => null;
}
