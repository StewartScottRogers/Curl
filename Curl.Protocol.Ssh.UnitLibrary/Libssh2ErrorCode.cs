namespace Curl.Protocol.Ssh;

/// <summary>
/// The libssh2 1.11.1 error numbers curl 8.21.0 prints in its SSH failure messages, and
/// the descriptions it prints beside them, as measured from the reference builds.
/// </summary>
internal static class Libssh2ErrorCode
{
    /// <summary>
    /// <c>LIBSSH2_ERROR_SOCKET_NONE</c>: the key exchange ended without a usable server
    /// <c>KEXINIT</c> - the peer closed, or sent a packet that breaks the framing rules or a
    /// malformed <c>KEXINIT</c>.
    /// </summary>
    internal const int SocketNone = -1;

    /// <summary>
    /// <c>LIBSSH2_ERROR_KEX_FAILURE</c>: the two <c>KEXINIT</c> messages share no algorithm
    /// in one of their lists.
    /// </summary>
    internal const int KeyExchangeFailure = -5;

    /// <summary>
    /// <c>LIBSSH2_ERROR_SOCKET_DISCONNECT</c>: the peer closed before sending a line that
    /// starts <c>SSH-</c>.
    /// </summary>
    internal const int SocketDisconnect = -13;

    /// <summary>The description printed with every failure of the key exchange.</summary>
    internal const string UnableToExchangeEncryptionKeys = "Unable to exchange encryption keys";

    /// <summary>The description printed when no identification string arrives.</summary>
    internal const string FailedGettingBanner = "Failed getting banner";
}
