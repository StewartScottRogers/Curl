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
}
