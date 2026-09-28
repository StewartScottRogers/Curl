namespace Curl.Protocol.Abstractions;

/// <summary>
/// Chooses a SASL mechanism (RFC 4422) from a mail server's list and starts an exchange for
/// it (ADR-0121).
/// </summary>
/// <remarks>
/// Implemented in <c>Curl.Authentication.UnitLibrary</c> and handed to the SMTP, POP3 and
/// IMAP handlers by <c>Curl.Console</c>, so no handler references the implementation. A
/// handler built without one authenticates only with its protocol's plain commands (POP3
/// <c>USER</c>/<c>PASS</c>/<c>APOP</c>, IMAP <c>LOGIN</c>).
/// </remarks>
public interface ISaslAuthenticator
{
    /// <summary>
    /// Chooses the mechanism curl 8.21.0 would pick from <paramref name="offeredMechanisms" />.
    /// </summary>
    /// <param name="request">What the transfer may authenticate with.</param>
    /// <param name="offeredMechanisms">
    /// The mechanisms the server advertised, by their SASL names, compared
    /// case-insensitively.
    /// </param>
    /// <returns>
    /// The chosen mechanism's SASL name, or <see langword="null" /> when none can be used with
    /// this request; the handler then falls back to its plain commands or fails with exit 67.
    /// </returns>
    string? ChooseMechanism(SaslRequest request, IReadOnlyList<string> offeredMechanisms);

    /// <summary>
    /// Starts one exchange for <paramref name="mechanism" />.
    /// </summary>
    /// <param name="mechanism">The mechanism <see cref="ChooseMechanism" /> returned.</param>
    /// <param name="request">What the transfer may authenticate with.</param>
    /// <returns>The exchange, which answers the server's challenges in turn.</returns>
    ISaslExchange Begin(string mechanism, SaslRequest request);
}
