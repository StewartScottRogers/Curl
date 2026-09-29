using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Tells apart the exceptions that mean the SSH connection itself broke - the peer closed
/// or disconnected, broke the framing, or sent a packet that fails its authentication -
/// from the failures a transfer reports on its own.
/// </summary>
internal static class SshConnectionFailure
{
    /// <summary>
    /// Gets whether <paramref name="exception" /> means the connection broke.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true" /> for an <see cref="EndOfStreamException" />, an <see cref="InvalidDataException" /> or an <see cref="SshPacketAuthenticationException" />.</returns>
    internal static bool Is(Exception exception) =>
        exception is EndOfStreamException or InvalidDataException or SshPacketAuthenticationException;
}
