namespace Curl.Extension.ObliviousDoh.Wire;

/// <summary>
/// An <c>ObliviousDoHMessage</c> (RFC 9230 section 6.1): its type, its <c>key_id</c> (the target's key identifier
/// in a query, the response nonce in a response) and its encrypted message.
/// </summary>
/// <param name="Type">The message type.</param>
/// <param name="KeyId">The <c>key_id</c> field.</param>
/// <param name="EncryptedMessage">The <c>encrypted_message</c> field.</param>
public sealed record ObliviousDohMessage(ObliviousDohMessageType Type, byte[] KeyId, byte[] EncryptedMessage)
{
    /// <summary>Reads a message laid out as section 6.1 says.</summary>
    /// <param name="bytes">The message's bytes.</param>
    /// <returns>The message, or <see langword="null" /> when the bytes are short or run past it.</returns>
    public static ObliviousDohMessage? Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new LengthPrefixedReader(bytes);
        return reader.TryReadByte(out byte type) && reader.TryReadVector(out ReadOnlySpan<byte> keyId)
            && reader.TryReadVector(out ReadOnlySpan<byte> encryptedMessage) && reader.IsEmpty
            ? new ObliviousDohMessage((ObliviousDohMessageType)type, keyId.ToArray(), encryptedMessage.ToArray())
            : null;
    }

    /// <summary>Writes the message as section 6.1 lays it out.</summary>
    /// <returns>The message's bytes.</returns>
    public byte[] Encode() => [(byte)Type, .. LengthPrefix.Prepend(KeyId), .. LengthPrefix.Prepend(EncryptedMessage)];
}
