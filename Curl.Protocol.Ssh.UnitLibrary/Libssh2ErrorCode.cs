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
    /// (BL-565, ADR-0212) on the Windows reference build for <c>aes*-ctr</c> with
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
    /// cipher refuses to decrypt it. Measured 2026-09-30 (BL-897, ADR-0212) on the OpenSSL
    /// reference build for <c>aes256-gcm@openssh.com</c> and <c>aes128-gcm@openssh.com</c>,
    /// the tag or the ciphertext altered; the Windows build offers no AES-GCM. A failed
    /// <c>chacha20-poly1305@openssh.com</c> tag ends with it too, by decision: both reference
    /// builds loop until they run out of memory there instead (BL-1032, ADR-0259). A packet
    /// whose decrypted <c>packet_length</c> is zero ends with it too: measured 2026-10-01
    /// (BL-1081, ADR-0206) on both reference builds under <c>aes128-ctr</c>, AES-GCM and
    /// <c>chacha20-poly1305@openssh.com</c>.
    /// </summary>
    internal const int Decrypt = -12;

    /// <summary>
    /// <c>LIBSSH2_ERROR_SOCKET_DISCONNECT</c>: the peer closed before sending a line that
    /// starts <c>SSH-</c>, or sent <c>SSH_MSG_DISCONNECT</c> instead of accepting the
    /// <c>ssh-userauth</c> service (measured 2026-09-29, BL-567).
    /// </summary>
    internal const int SocketDisconnect = -13;

    /// <summary>
    /// <c>LIBSSH2_ERROR_PROTO</c>: the server's <c>SSH_MSG_SERVICE_ACCEPT</c> is shorter than
    /// five bytes or names another service. Measured 2026-09-29 (BL-567).
    /// </summary>
    internal const int Protocol = -14;

    /// <summary>
    /// <c>LIBSSH2_ERROR_SOCKET_RECV</c>: the peer closed instead of answering the
    /// <c>ssh-userauth</c> service request. Measured 2026-09-29 (BL-565, BL-567). Also the
    /// code printed with <see cref="FailedGettingBanner" /> when the server resets the
    /// connection before its identification string (measured 2026-09-30, BL-991, ADR-0283).
    /// </summary>
    internal const int SocketReceive = -43;

    /// <summary>
    /// <c>LIBSSH2_ERROR_OUT_OF_BOUNDARY</c>: a packet's decrypted <c>packet_length</c> makes
    /// the packet, MAC or tag included, larger than 40000 bytes. Measured 2026-10-01
    /// (BL-1032, BL-1081, ADR-0206) on both reference builds under <c>aes128-ctr</c>,
    /// AES-GCM and <c>chacha20-poly1305@openssh.com</c>.
    /// </summary>
    internal const int OutOfBoundary = -41;

    /// <summary>The description printed with every failure of the key exchange.</summary>
    internal const string UnableToExchangeEncryptionKeys = "Unable to exchange encryption keys";

    /// <summary>
    /// The description printed when the server's answer to the <c>ssh-userauth</c> service
    /// request, the first packet after the key exchange, cannot be read: measured
    /// 2026-09-29 with <see cref="InvalidMac" /> when its MAC does not match (BL-565),
    /// <see cref="SocketReceive" /> when the peer closes and <see cref="SocketDisconnect" />
    /// when it sends <c>SSH_MSG_DISCONNECT</c> (BL-567).
    /// </summary>
    internal const string FailedToGetUserAuthResponse = "Failed to get response to ssh-userauth request";

    /// <summary>
    /// The description printed with <see cref="Protocol" /> when the server's
    /// <c>SSH_MSG_SERVICE_ACCEPT</c> is shorter than five bytes.
    /// </summary>
    internal const string UnexpectedPacketLength = "Unexpected packet length";

    /// <summary>
    /// The description printed with <see cref="Protocol" /> when the server's
    /// <c>SSH_MSG_SERVICE_ACCEPT</c> names a service other than <c>ssh-userauth</c>.
    /// </summary>
    internal const string InvalidResponseReceivedFromServer = "Invalid response received from server";

    /// <summary>The description printed when no identification string arrives.</summary>
    internal const string FailedGettingBanner = "Failed getting banner";
}
