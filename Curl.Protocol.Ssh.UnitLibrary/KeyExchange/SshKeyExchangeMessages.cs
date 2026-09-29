using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// Sends and receives the messages of one key exchange, from the method's first message to
/// <c>NEWKEYS</c>.
/// </summary>
/// <param name="reader">The reader of the server's packets.</param>
/// <param name="writer">The writer of the client's packets.</param>
/// <param name="isStrict">
/// Whether this is the first exchange under strict key exchange, when nothing but the
/// exchange's own messages may arrive; otherwise <c>IGNORE</c>, <c>DEBUG</c> and
/// <c>UNIMPLEMENTED</c> are skipped (RFC 4253 section 11).
/// </param>
internal sealed class SshKeyExchangeMessages(SshPacketReader reader, SshPacketWriter writer, bool isStrict)
{
    /// <summary>
    /// Sends one message.
    /// </summary>
    /// <param name="payload">The message, message number first.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the message has been flushed.</returns>
    internal ValueTask SendAsync(byte[] payload, CancellationToken cancellationToken) =>
        writer.WriteAsync(payload, cancellationToken);

    /// <summary>
    /// Reads the next message, which must be <paramref name="messageNumber" />.
    /// </summary>
    /// <param name="messageNumber">The message expected.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A reader positioned after the message number.</returns>
    /// <exception cref="InvalidDataException">Another message arrived, or the packet framing broke.</exception>
    /// <exception cref="EndOfStreamException">The peer closed.</exception>
    internal async ValueTask<SshWireReader> ReadAsync(byte messageNumber, CancellationToken cancellationToken)
    {
        while (true)
        {
            byte[] payload = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (payload[0] == messageNumber)
            {
                SshWireReader message = new(payload);
                message.ReadByte();
                return message;
            }

            if (isStrict || payload[0] is not (SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented))
            {
                throw new InvalidDataException($"The SSH server sent message {payload[0]} where the key exchange expected {messageNumber}.");
            }
        }
    }
}
