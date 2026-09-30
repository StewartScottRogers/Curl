using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Writes the FTP and FTPS session steps to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ftp" /> (ADR-0222, BL-924): the failure that ends a
/// session as <c>error</c>, a fallback as <c>warning</c>, each milestone as <c>info</c>, and
/// each command and reply as <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see cref="NoDiagnosticLog.Instance" /> writes nothing.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. <c>PASS</c> and <c>ACCT</c> are logged by verb
/// alone: their argument is a credential (ADR-0222, decision 7).
/// </remarks>
internal sealed class FtpDiagnosticLog(IDiagnosticLog log)
{
    /// <summary>The verbs whose argument is a credential, logged without it.</summary>
    private static readonly string[] CredentialVerbs = ["PASS", "ACCT"];

    /// <summary>
    /// Logs, at <c>verbose</c>, a command sent on the control connection, without the
    /// argument of <c>PASS</c> or <c>ACCT</c>.
    /// </summary>
    /// <param name="command">The command line without its line end.</param>
    public void CommandSent(string command)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "sent " + WithoutCredential(command));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, a reply's code and first line.</summary>
    /// <param name="code">The reply's code.</param>
    /// <param name="firstLine">The reply's first line, without its line end.</param>
    public void ReplyRead(int code, string firstLine)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"reply {code}: {firstLine}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, how <c>--ftp-method</c> walks the path.</summary>
    /// <param name="method">The <c>--ftp-method</c> in force.</param>
    /// <param name="directoryCount">How many <c>CWD</c>s the walk sends.</param>
    public void PathWalk(FtpFileMethod method, int directoryCount)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"--ftp-method {method}: {directoryCount} CWD"));
        }
    }

    /// <summary>Logs, at <c>info</c>, that the login is complete.</summary>
    public void LoggedIn() => Info("logged in");

    /// <summary>Logs, at <c>info</c>, that the control connection is TLS after <c>AUTH</c>.</summary>
    public void ControlSecured() => Info("TLS on the control connection");

    /// <summary>
    /// Logs, at <c>warning</c>, that <c>AUTH SSL</c> and <c>AUTH TLS</c> were refused and
    /// <c>--ssl</c> carries on in plaintext.
    /// </summary>
    public void ControlLeftPlaintext() => Warning("AUTH SSL and AUTH TLS refused; the control connection stays plaintext");

    /// <summary>
    /// Logs the <c>PROT</c> outcome: <c>info</c> when data connections will be TLS,
    /// <c>warning</c> when a refused <c>PROT P</c> leaves them plaintext, <c>verbose</c> for
    /// <c>PROT C</c>.
    /// </summary>
    /// <param name="askedPrivate">Whether <c>PROT P</c> was sent.</param>
    /// <param name="accepted">Whether it was answered with a 2xx.</param>
    public void DataProtection(bool askedPrivate, bool accepted)
    {
        if (!askedPrivate)
        {
            Verbose("PROT C: data connections stay plaintext");
        }
        else if (accepted)
        {
            Info("PROT P accepted: data connections will be TLS");
        }
        else
        {
            Warning("PROT P refused; data connections stay plaintext");
        }
    }

    /// <summary>Logs, at <c>info</c>, that the data connection is TLS.</summary>
    public void DataSecured() => Info("TLS on the data connection");

    /// <summary>Logs, at <c>info</c>, the directory the <c>CWD</c>s reached.</summary>
    /// <param name="directories">The <c>CWD</c> arguments, in order; nothing is logged for none.</param>
    public void DirectoryReached(IReadOnlyList<string> directories)
    {
        if (directories.Count > 0 && log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "reached directory " + string.Join('/', directories));
        }
    }

    /// <summary>Logs, at <c>warning</c>, that <c>EPSV</c> was refused and <c>PASV</c> follows.</summary>
    /// <param name="code">The refusal's reply code.</param>
    public void EpsvRefused(int code)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(CultureInfo.InvariantCulture, $"EPSV refused with {code}; falling back to PASV"));
        }
    }

    /// <summary>Logs, at <c>warning</c>, that <c>EPRT</c> was refused and <c>PORT</c> follows.</summary>
    public void EprtRefused() => Warning("EPRT refused; falling back to PORT");

    /// <summary>
    /// Logs, at <c>warning</c>, that the address a <c>227</c> reply named is skipped for the
    /// control connection's host, as <c>--ftp-skip-pasv-ip</c> does.
    /// </summary>
    /// <param name="named">The address the reply named.</param>
    /// <param name="used">The host dialled instead.</param>
    public void PassiveAddressSkipped(string named, string used)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, $"--ftp-skip-pasv-ip: PASV named {named}; using {used}");
        }
    }

    /// <summary>Logs, at <c>info</c>, the passive data connection opened.</summary>
    /// <param name="host">The host dialled.</param>
    /// <param name="port">The port dialled.</param>
    public void PassiveDataConnected(string host, int port)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(CultureInfo.InvariantCulture, $"passive data connection to {host}:{port}"));
        }
    }

    /// <summary>Logs, at <c>info</c>, that an active-mode port was announced.</summary>
    /// <param name="verb"><c>EPRT</c> or <c>PORT</c>.</param>
    public void ActivePortAnnounced(string verb)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "active data connection announced with " + verb);
        }
    }

    /// <summary>Logs, at <c>info</c>, that the server's active-mode data connection was accepted.</summary>
    public void ActiveDataAccepted() => Info("active data connection accepted");

    /// <summary>Logs, at <c>warning</c>, a <c>-Q</c> command marked <c>*</c> whose refusal is ignored.</summary>
    /// <param name="command">The quote command, logged as <see cref="CommandSent" /> would.</param>
    /// <param name="code">The refusal's reply code.</param>
    public void QuoteFailureIgnored(string command, int code)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, string.Create(CultureInfo.InvariantCulture, $"quote {WithoutCredential(command)} refused with {code}; ignored"));
        }
    }

    /// <summary>Logs, at <c>info</c>, that the transfer command was answered and data moves.</summary>
    /// <param name="command">The transfer command, such as <c>RETR file.txt</c>.</param>
    public void TransferStarted(string command)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, "transfer started: " + command);
        }
    }

    /// <summary>
    /// Logs how the session ended: <c>info</c> with the bytes and milliseconds for a success,
    /// <c>error</c> with the <see cref="CurlExitCode" /> and message for a failure.
    /// </summary>
    /// <param name="result">The session's outcome.</param>
    /// <param name="elapsed">How long the session took.</param>
    public void SessionEnded(TransferResult result, TimeSpan elapsed)
    {
        if (!result.IsSuccess)
        {
            Failed(result);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, string.Create(
                CultureInfo.InvariantCulture,
                $"transfer finished: {result.BytesTransferred} bytes in {(long)elapsed.TotalMilliseconds} ms"));
        }
    }

    /// <summary>Logs, at <c>error</c>, a failure that ends the session with its <see cref="CurlExitCode" />.</summary>
    /// <param name="result">The failure.</param>
    public void Failed(TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"failed with {result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    private static string WithoutCredential(string command)
    {
        int space = command.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 && CredentialVerbs.Contains(command[..space], StringComparer.OrdinalIgnoreCase)
            ? command[..space] + " (not logged)"
            : command;
    }

    private void Info(string message) => WriteIfEnabled(DiagnosticLogLevel.Info, message);

    private void Warning(string message) => WriteIfEnabled(DiagnosticLogLevel.Warning, message);

    private void Verbose(string message) => WriteIfEnabled(DiagnosticLogLevel.Verbose, message);

    private void WriteIfEnabled(DiagnosticLogLevel level, string message)
    {
        if (log.IsEnabled(level))
        {
            Write(level, message);
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ftp, message);
}
