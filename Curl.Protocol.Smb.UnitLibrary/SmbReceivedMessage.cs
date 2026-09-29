namespace Curl.Protocol.Smb;

/// <summary>
/// One message <see cref="SmbMessageReader" /> read, or the receive error curl reports
/// instead.
/// </summary>
internal sealed class SmbReceivedMessage
{
    private SmbReceivedMessage(byte[]? bytes, string? errorMessage)
    {
        Bytes = bytes;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets every byte received for the message, NetBIOS header first, or
    /// <see langword="null" /> when the receive failed.
    /// </summary>
    public byte[]? Bytes { get; }

    /// <summary>
    /// Gets the message curl prints with exit 56 when the receive failed, or
    /// <see langword="null" /> when a message was read.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates the result of a message read whole.</summary>
    /// <param name="bytes">Every byte received for it.</param>
    /// <returns>A result carrying <paramref name="bytes" />.</returns>
    public static SmbReceivedMessage Received(byte[] bytes) => new(bytes, null);

    /// <summary>Creates the result of a receive that failed with exit 56.</summary>
    /// <param name="errorMessage">The message curl prints.</param>
    /// <returns>A result carrying <paramref name="errorMessage" />.</returns>
    public static SmbReceivedMessage Failed(string errorMessage) => new(null, errorMessage);
}
