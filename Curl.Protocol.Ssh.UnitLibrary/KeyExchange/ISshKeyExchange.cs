using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// One key-exchange method's messages, from the client's first to the server's reply:
/// the shared secret and the exchange hash.
/// </summary>
internal interface ISshKeyExchange
{
    /// <summary>
    /// Runs the method's messages and computes K and H.
    /// </summary>
    /// <param name="messages">The key exchange's message channel.</param>
    /// <param name="handshake">The identification strings and <c>KEXINIT</c> payloads H covers.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The host key, its signature, K and H.</returns>
    /// <exception cref="InvalidDataException">
    /// A message was unexpected or malformed, or the server's public value lies outside its
    /// group.
    /// </exception>
    /// <exception cref="EndOfStreamException">The peer closed.</exception>
    ValueTask<SshKeyExchangeOutcome> ExchangeAsync(
        SshKeyExchangeMessages messages,
        SshNegotiatedHandshake handshake,
        CancellationToken cancellationToken);
}
