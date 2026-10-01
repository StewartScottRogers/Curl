namespace Curl.Protocol.Ssh.Transport;

/// <summary>
/// A received packet's decrypted <c>packet_length</c> is zero or over the maximum, which
/// libssh2 1.11.1 refuses with its own error code before reading the rest of the packet
/// (BL-1081, ADR-0206). Caught wherever an <see cref="InvalidDataException" /> from a
/// packet read is; a length off the block size is a plain
/// <see cref="InvalidDataException" /> instead.
/// </summary>
/// <param name="libssh2ErrorCode">
/// The libssh2 error code curl prints for it: <see cref="Libssh2ErrorCode.Decrypt" /> for a
/// zero length, <see cref="Libssh2ErrorCode.OutOfBoundary" /> for one over the maximum.
/// </param>
/// <param name="packetLength">The length the packet announced.</param>
internal sealed class SshPacketLengthException(int libssh2ErrorCode, uint packetLength)
    : Exception($"The SSH packet length {packetLength} is not a valid packet length.")
{
    /// <summary>Gets the libssh2 error code curl prints for the failure.</summary>
    internal int Libssh2ErrorCode { get; } = libssh2ErrorCode;
}
