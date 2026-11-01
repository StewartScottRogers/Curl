using Curl.Protocol.Abstractions;
using Curl.Ssh.Transport;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Writes the SCP and SFTP session steps above the transport layer to Curl's own diagnostic
/// log, component <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925): the failure
/// that ends a session as <c>error</c>, with its libssh2 code when it has one; an
/// authentication method refused and a host key taken unchecked as <c>warning</c>; the host
/// key check, the method that authenticated and the transfer as <c>info</c>; and each
/// channel request and SFTP request and status as <c>verbose</c>. The transport's own lines
/// (messages, algorithms, key exchange, host key) are <see cref="Transport" />'s, written to
/// the same log.
/// </summary>
/// <param name="transportLog">The transport layer's log, whose diagnostic log these lines also go to.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. No method takes a password, a pass phrase or a
/// private key, and messages are logged by number alone, never by content (ADR-0222,
/// decision 7). Each group of steps lives in its own partial file
/// (<c>SshDiagnosticLog.&lt;Group&gt;.cs</c>), so a change to one group touches only its
/// file (BL-4366).
/// </remarks>
internal sealed partial class SshDiagnosticLog(SshTransportLog transportLog)
{
    private readonly IDiagnosticLog log = transportLog.Log;

    /// <summary>Gets the transport layer's log, which this one writes beside.</summary>
    internal SshTransportLog Transport => transportLog;

    private void WriteIfEnabled(DiagnosticLogLevel level, string message)
    {
        if (log.IsEnabled(level))
        {
            Write(level, message);
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ssh, message);
}
