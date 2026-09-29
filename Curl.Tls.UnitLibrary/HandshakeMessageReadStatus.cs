namespace Curl.Tls;

/// <summary>What <see cref="HandshakeMessageReader.Read" /> found at the start of its buffer.</summary>
public enum HandshakeMessageReadStatus
{
    /// <summary>A whole message was read.</summary>
    Complete,

    /// <summary>The next message is not whole yet; read again once more bytes arrive.</summary>
    NeedMoreBytes,

    /// <summary>The bytes can never form a valid message; send the alert.</summary>
    Failed,
}
