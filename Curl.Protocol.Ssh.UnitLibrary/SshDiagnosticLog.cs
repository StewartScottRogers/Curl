using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Writes the SCP and SFTP session steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925): the failure that ends a
/// session as <c>error</c>, with its libssh2 code when it has one; an authentication method
/// refused and a host key taken unchecked as <c>warning</c>; the server, the algorithms, the
/// host key, the method that authenticated and the transfer as <c>info</c>; and each SSH
/// message number, channel request and SFTP request and status as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. No method takes a password, a pass phrase or a
/// private key, and messages are logged by number alone, never by content (ADR-0222,
/// decision 7). Each group of steps lives in its own partial file
/// (<c>SshDiagnosticLog.&lt;Group&gt;.cs</c>), so a change to one group touches only its
/// file (BL-4366).
/// </remarks>
internal sealed partial class SshDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>Gets the log that writes nothing, for a transport built without one.</summary>
    internal static SshDiagnosticLog None { get; } = new(NoDiagnosticLog.Instance);

    private void WriteIfEnabled(DiagnosticLogLevel level, string message)
    {
        if (log.IsEnabled(level))
        {
            Write(level, message);
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ssh, message);
}
