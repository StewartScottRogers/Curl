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
    /// <c>LIBSSH2_ERROR_INVALID_MAC</c>: a packet's MAC does not match. Measured 2026-09-29
    /// (BL-565, ADR-0207) on the Windows reference build for <c>aes*-ctr</c> with
    /// <c>hmac-sha2-256</c> and <c>hmac-sha2-512-etm@openssh.com</c>, the MAC or the
    /// ciphertext altered.
    /// </summary>
    internal const int InvalidMac = -4;

    /// <summary>
    /// <c>LIBSSH2_ERROR_KEX_FAILURE</c>: the two <c>KEXINIT</c> messages share no algorithm
    /// in one of their lists.
    /// </summary>
    internal const int KeyExchangeFailure = -5;

    /// <summary>
    /// <c>LIBSSH2_ERROR_KEY_EXCHANGE_FAILURE</c>: the key-exchange method itself failed
    /// after the algorithms were agreed - the peer closed or sent an unexpected or malformed
    /// message, a public value outside its group, a host key or signature of the wrong
    /// type, or a signature that does not verify. Measured 2026-09-29 (BL-564) for each of
    /// those cases on the Windows reference build.
    /// </summary>
    internal const int KeyExchangeMethodFailure = -8;

    /// <summary>
    /// <c>LIBSSH2_ERROR_DECRYPT</c>: an AES-GCM packet's tag does not match, so libssh2's
    /// cipher refuses to decrypt it. Taken from libssh2 1.11.1's <c>transport.c</c>, not yet
    /// measured: only the OpenSSL build offers AES-GCM (ADR-0207, BL-889).
    /// </summary>
    internal const int Decrypt = -12;

    /// <summary>
    /// <c>LIBSSH2_ERROR_SOCKET_DISCONNECT</c>: the peer closed before sending a line that
    /// starts <c>SSH-</c>.
    /// </summary>
    internal const int SocketDisconnect = -13;

    /// <summary>The description printed with every failure of the key exchange.</summary>
    internal const string UnableToExchangeEncryptionKeys = "Unable to exchange encryption keys";

    /// <summary>
    /// The description printed when the first packet after the key exchange, the server's
    /// answer to the <c>ssh-userauth</c> service request, cannot be read: measured
    /// 2026-09-29 (BL-565) with <see cref="InvalidMac" /> when its MAC does not match. The
    /// service request is BL-567's.
    /// </summary>
    internal const string FailedToGetUserAuthResponse = "Failed to get response to ssh-userauth request";

    /// <summary>The description printed when no identification string arrives.</summary>
    internal const string FailedGettingBanner = "Failed getting banner";
}
