namespace Curl.Kerberos;

/// <summary>
/// An established initiator context's per-message protection: Wrap and MIC tokens made in
/// the context key and numbered from the initiator's sequence number, and the acceptor's
/// tokens checked against the key and the acceptor's sequence number. <see cref="Create" />
/// picks RFC 4757 section 7's tokens for an <c>rc4-hmac</c> key and RFC 4121 section 4.2's
/// for every other. <see cref="Dispose" /> zeroes the key.
/// </summary>
/// <remarks>
/// Every acceptor token must carry exactly the next sequence number: a replayed, reordered
/// or skipped one is refused with <see cref="KerberosGssError.BadSequenceNumber" /> (ADR-0169).
/// </remarks>
internal abstract class KerberosGssMessageProtection : IDisposable
{
    private readonly byte[] key;
    private ulong nextSendSequence;
    private ulong nextReceiveSequence;

    private protected KerberosGssMessageProtection(KerberosKey contextKey, ulong sendSequence, ulong receiveSequence)
    {
        key = contextKey.Value.ToArray();
        nextSendSequence = sendSequence;
        nextReceiveSequence = receiveSequence;
    }

    /// <summary>Gets the context key.</summary>
    private protected ReadOnlySpan<byte> Key => key;

    /// <summary>Makes the protection for <paramref name="contextKey" />'s encryption type.</summary>
    /// <param name="contextKey">The context key; it stays the caller's, the protection keeps a copy.</param>
    /// <param name="acceptorSubkey">Whether the key is the acceptor's subkey from the AP-REP.</param>
    /// <param name="sendSequence">The initiator's initial sequence number.</param>
    /// <param name="receiveSequence">The acceptor's initial sequence number.</param>
    /// <param name="randomSource">Gives confounders.</param>
    /// <returns>The protection; the caller disposes it.</returns>
    /// <exception cref="KerberosCryptographyException">The key's encryption type is not one this library has.</exception>
    public static KerberosGssMessageProtection Create(KerberosKey contextKey, bool acceptorSubkey, ulong sendSequence, ulong receiveSequence, IKerberosRandomSource randomSource) =>
        contextKey.EncryptionType == (int)KerberosEncryptionType.Rc4Hmac
            ? new Rc4HmacGssMessageProtection(contextKey, sendSequence, receiveSequence, randomSource)
            : new Rfc4121GssMessageProtection(
                KerberosEncryption.Create((KerberosEncryptionType)contextKey.EncryptionType, randomSource),
                contextKey,
                acceptorSubkey,
                sendSequence,
                receiveSequence);

    /// <summary>Makes a Wrap token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <param name="encrypt">Whether to encrypt it, or only protect its integrity.</param>
    /// <returns>The token.</returns>
    public abstract byte[] Wrap(ReadOnlySpan<byte> message, bool encrypt);

    /// <summary>Checks the acceptor's Wrap token and gives its message.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The message, and whether it was encrypted.</returns>
    /// <exception cref="KerberosGssException">The token is malformed, altered or out of sequence.</exception>
    public abstract KerberosGssUnwrapped Unwrap(ReadOnlySpan<byte> token);

    /// <summary>Makes a MIC token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The token.</returns>
    public abstract byte[] GetMic(ReadOnlySpan<byte> message);

    /// <summary>Checks the acceptor's MIC token for <paramref name="message" />.</summary>
    /// <param name="message">The message.</param>
    /// <param name="token">The token.</param>
    /// <exception cref="KerberosGssException">The token is malformed, does not match or is out of sequence.</exception>
    public abstract void VerifyMic(ReadOnlySpan<byte> message, ReadOnlySpan<byte> token);

    /// <summary>Zeroes the key.</summary>
    public void Dispose() => Array.Clear(key);

    /// <summary>Gives the sequence number for the next token sent, and counts it.</summary>
    /// <returns>The sequence number.</returns>
    private protected ulong TakeSendSequence() => nextSendSequence++;

    /// <summary>Accepts <paramref name="sequence" /> as the next acceptor token's number.</summary>
    /// <param name="sequence">The number the token carries.</param>
    /// <exception cref="KerberosGssException"><see cref="KerberosGssError.BadSequenceNumber" />: not the next expected.</exception>
    private protected void AcceptReceiveSequence(ulong sequence)
    {
        if (sequence != nextReceiveSequence)
        {
            throw new KerberosGssException(KerberosGssError.BadSequenceNumber);
        }

        nextReceiveSequence++;
    }
}
