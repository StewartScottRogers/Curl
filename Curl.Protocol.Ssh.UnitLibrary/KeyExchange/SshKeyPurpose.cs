namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// The six keys a key exchange derives, each named by the letter RFC 4253 section 7.2
/// hashes in to derive it.
/// </summary>
internal enum SshKeyPurpose
{
    /// <summary>The initial IV, client to server: <c>HASH(K || H || "A" || session_id)</c>.</summary>
    InitialIvClientToServer = 'A',

    /// <summary>The initial IV, server to client (<c>"B"</c>).</summary>
    InitialIvServerToClient = 'B',

    /// <summary>The encryption key, client to server (<c>"C"</c>).</summary>
    EncryptionKeyClientToServer = 'C',

    /// <summary>The encryption key, server to client (<c>"D"</c>).</summary>
    EncryptionKeyServerToClient = 'D',

    /// <summary>The integrity key, client to server (<c>"E"</c>).</summary>
    IntegrityKeyClientToServer = 'E',

    /// <summary>The integrity key, server to client (<c>"F"</c>).</summary>
    IntegrityKeyServerToClient = 'F',
}
