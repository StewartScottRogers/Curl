using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Writes the IMAP session's steps to Curl's own diagnostic log under the <c>imap</c>
/// component (ADR-0222): each method tests <see cref="IDiagnosticLog.IsEnabled" /> before
/// it builds its message, and none is ever handed a credential (decision 7).
/// </summary>
internal static class ImapDiagnosticLogLines
{
    /// <summary>What the log says in place of a SASL message, which is never logged.</summary>
    public const string SaslResponseNotLogged = "<SASL response not logged>";

    /// <summary>What the log says in place of the password <c>LOGIN</c> carries.</summary>
    public const string PasswordNotLogged = "<password not logged>";

    /// <summary>Logs a command or line sent, at <c>verbose</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="line">The line as it may be logged, with any credential already left out.</param>
    public static void LineSent(IDiagnosticLog log, string line) =>
        Write(log, DiagnosticLogLevel.Verbose, "sent ", line);

    /// <summary>
    /// Logs the status of a tagged response, or a continuation, at <c>verbose</c>: never the
    /// text after it, which may carry a SASL challenge.
    /// </summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="tag">The command's tag, or <c>+</c> for a continuation.</param>
    /// <param name="status">The response's status.</param>
    public static void ResponseRead(IDiagnosticLog log, string tag, ImapResponseStatus status)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Imap, "reply " + tag + " " + status);
        }
    }

    /// <summary>Logs the server's greeting, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="status">The greeting's status: <c>Ok</c> or <c>Preauth</c>.</param>
    public static void GreetingReceived(IDiagnosticLog log, ImapResponseStatus status)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Imap, "greeting " + status + " received");
        }
    }

    /// <summary>Logs a <c>STARTTLS</c> upgrade that succeeded, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    public static void TlsUpgraded(IDiagnosticLog log) =>
        Write(log, DiagnosticLogLevel.Info, "STARTTLS upgraded the connection to TLS", string.Empty);

    /// <summary>Logs an accepted login, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="method">How the session logged in, such as <c>SASL PLAIN</c> or <c>LOGIN</c>.</param>
    public static void LoggedIn(IDiagnosticLog log, string method) =>
        Write(log, DiagnosticLogLevel.Info, "logged in with ", method);

    /// <summary>Logs a SASL mechanism cancelled before the authenticator chooses again, at <c>warning</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="mechanism">The cancelled mechanism.</param>
    public static void MechanismCancelled(IDiagnosticLog log, string mechanism) =>
        Write(log, DiagnosticLogLevel.Warning, "SASL mechanism cancelled, choosing another: ", mechanism);

    /// <summary>Logs that none of the offered SASL mechanisms can be used, at <c>warning</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="offered">The mechanisms the server offered.</param>
    public static void NoUsableMechanism(IDiagnosticLog log, IReadOnlyList<string> offered)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            log.Write(
                DiagnosticLogLevel.Warning,
                DiagnosticLogComponents.Imap,
                "no usable SASL mechanism among: " + string.Join(' ', offered));
        }
    }

    /// <summary>
    /// Logs how the transfer ended: a success at <c>info</c> with its bytes and elapsed
    /// milliseconds, a failure at <c>error</c> with its <see cref="CurlExitCode" />.
    /// </summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="timeProvider">The clock <paramref name="started" /> was read from.</param>
    /// <param name="started">The <see cref="TimeProvider.GetTimestamp" /> the transfer started at.</param>
    public static void TransferEnded(IDiagnosticLog log, TransferResult result, TimeProvider timeProvider, long started)
    {
        if (result.ExitCode != CurlExitCode.Ok)
        {
            TransferFailed(log, result);
        }
        else if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            long milliseconds = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;
            log.Write(
                DiagnosticLogLevel.Info,
                DiagnosticLogComponents.Imap,
                string.Create(CultureInfo.InvariantCulture, $"transfer done, {result.BytesTransferred} bytes in {milliseconds} ms"));
        }
    }

    private static void TransferFailed(IDiagnosticLog log, TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            log.Write(
                DiagnosticLogLevel.Error,
                DiagnosticLogComponents.Imap,
                string.Create(CultureInfo.InvariantCulture, $"transfer failed with CurlExitCode.{result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    private static void Write(IDiagnosticLog log, DiagnosticLogLevel level, string text, string argument)
    {
        if (log.IsEnabled(level))
        {
            log.Write(level, DiagnosticLogComponents.Imap, text + argument);
        }
    }
}
