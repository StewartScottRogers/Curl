using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Tells apart the exceptions that mean the SSH connection itself broke - the peer closed,
/// disconnected or reset it, broke the framing, or sent a packet that fails its authentication -
/// from the failures a transfer reports on its own.
/// </summary>
internal static class SshConnectionFailure
{
    /// <summary>
    /// Gets whether <paramref name="exception" /> means the connection broke.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true" /> for an <see cref="EndOfStreamException" />, an <see cref="SshConnectionLostException" />, an <see cref="InvalidDataException" /> or an <see cref="SshPacketAuthenticationException" />.</returns>
    internal static bool Is(Exception exception) =>
        exception is EndOfStreamException or SshConnectionLostException or InvalidDataException or SshPacketAuthenticationException;

    /// <summary>
    /// Runs <paramref name="step" />, reporting a broken connection as curl does when an SFTP
    /// request has no message of its own: <see cref="SshTransferException.SshLayerError" />.
    /// </summary>
    /// <typeparam name="T">What the step returns.</typeparam>
    /// <param name="step">The step.</param>
    /// <returns>What the step returned.</returns>
    /// <exception cref="SshTransferException">The step's own failure, or exit 79 when the connection broke.</exception>
    internal static async ValueTask<T> ReportAsSshLayerErrorAsync<T>(Func<ValueTask<T>> step)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception exception) when (Is(exception))
        {
            throw SshTransferException.SshLayerError();
        }
    }
}
