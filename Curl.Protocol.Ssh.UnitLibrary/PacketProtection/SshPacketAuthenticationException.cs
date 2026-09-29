namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A received packet's MAC or authentication tag does not match: the packet was altered
/// on the way, or the two sides derived different keys. The packet is discarded unread.
/// </summary>
/// <param name="libssh2ErrorCode">
/// The libssh2 error code curl prints for it (ADR-0207): <see cref="Libssh2ErrorCode.InvalidMac" />
/// for a MAC, <see cref="Libssh2ErrorCode.Decrypt" /> for an AES-GCM tag.
/// </param>
internal sealed class SshPacketAuthenticationException(int libssh2ErrorCode)
    : Exception("The SSH packet's MAC or authentication tag does not match.")
{
    /// <summary>Gets the libssh2 error code curl prints for the failure.</summary>
    internal int Libssh2ErrorCode { get; } = libssh2ErrorCode;
}
