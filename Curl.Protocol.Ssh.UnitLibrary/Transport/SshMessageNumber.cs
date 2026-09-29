namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// The transport-layer message numbers of RFC 4253 section 12 that this library reads or
/// writes.
/// </summary>
internal static class SshMessageNumber
{
    /// <summary><c>SSH_MSG_DISCONNECT</c>.</summary>
    internal const byte Disconnect = 1;

    /// <summary><c>SSH_MSG_IGNORE</c>.</summary>
    internal const byte Ignore = 2;

    /// <summary><c>SSH_MSG_UNIMPLEMENTED</c>.</summary>
    internal const byte Unimplemented = 3;

    /// <summary><c>SSH_MSG_DEBUG</c>.</summary>
    internal const byte Debug = 4;

    /// <summary><c>SSH_MSG_KEXINIT</c>.</summary>
    internal const byte KeyExchangeInit = 20;

    /// <summary><c>SSH_MSG_NEWKEYS</c>: the sender uses the new keys from its next packet.</summary>
    internal const byte NewKeys = 21;

    /// <summary>
    /// <c>SSH_MSG_KEXDH_INIT</c> (RFC 4253 section 8), which RFC 5656 reuses as
    /// <c>SSH_MSG_KEX_ECDH_INIT</c>: the client's ephemeral public value.
    /// </summary>
    internal const byte KeyExchangeDiffieHellmanInit = 30;

    /// <summary>
    /// <c>SSH_MSG_KEXDH_REPLY</c> (RFC 4253 section 8), which RFC 5656 reuses as
    /// <c>SSH_MSG_KEX_ECDH_REPLY</c>: the host key, the server's public value and its
    /// signature over the exchange hash.
    /// </summary>
    internal const byte KeyExchangeDiffieHellmanReply = 31;

    /// <summary>
    /// <c>SSH_MSG_KEX_DH_GEX_GROUP</c> (RFC 4419 section 5): the prime and generator the
    /// server chose. It shares its number with <see cref="KeyExchangeDiffieHellmanReply" />.
    /// </summary>
    internal const byte GroupExchangeGroup = 31;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_INIT</c> (RFC 4419): the client's public value.</summary>
    internal const byte GroupExchangeInit = 32;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_REPLY</c> (RFC 4419): the host key, the server's public value and its signature.</summary>
    internal const byte GroupExchangeReply = 33;

    /// <summary><c>SSH_MSG_KEX_DH_GEX_REQUEST</c> (RFC 4419): the group sizes the client accepts.</summary>
    internal const byte GroupExchangeRequest = 34;
}
