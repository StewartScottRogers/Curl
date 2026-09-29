using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Writes the POP3 session's steps to Curl's own diagnostic log under the <c>pop3</c>
/// component (ADR-0222): each method tests <see cref="IDiagnosticLog.IsEnabled" /> before
/// it builds its message, and none is ever handed a credential (decision 7).
/// </summary>
internal static class Pop3DiagnosticLogLines
{
    /// <summary>What the log says in place of a SASL message, which is never logged.</summary>
    public const string SaslResponseNotLogged = "<SASL response not logged>";

    /// <summary>What the log says in place of the password <c>PASS</c> carries.</summary>
    public const string PasswordNotLogged = "<password not logged>";

    /// <summary>What the log says in place of the digest <c>APOP</c> carries.</summary>
    public const string DigestNotLogged = "<digest not logged>";

    /// <summary>Logs a command sent, at <c>verbose</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="command">The command as it may be logged, with any credential already left out.</param>
    public static void CommandSent(IDiagnosticLog log, string command) =>
        Write(log, DiagnosticLogLevel.Verbose, "sent ", command);

    /// <summary>
    /// Logs a status line's status at <c>verbose</c>: <c>+OK</c>, <c>-ERR</c> or <c>+</c>
    /// for a continuation, never the text after it, which may carry a SASL challenge.
    /// </summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="response">The status line.</param>
    public static void ResponseRead(IDiagnosticLog log, Pop3Response response)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Pop3, "reply " + StatusOf(response));
        }
    }

    /// <summary>Logs the answer to <c>CAPA</c>, at <c>verbose</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="capabilities">The capabilities, or <see langword="null" /> when <c>CAPA</c> was refused.</param>
    public static void CapabilitiesRead(IDiagnosticLog log, Pop3Capabilities? capabilities)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            log.Write(
                DiagnosticLogLevel.Verbose,
                DiagnosticLogComponents.Pop3,
                capabilities is null ? "reply -ERR" : "reply +OK, SASL mechanisms: " + string.Join(' ', capabilities.SaslMechanisms));
        }
    }

    /// <summary>Logs the server's greeting, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    public static void GreetingReceived(IDiagnosticLog log) =>
        Write(log, DiagnosticLogLevel.Info, "greeting +OK received", string.Empty);

    /// <summary>Logs an <c>STLS</c> upgrade that succeeded, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    public static void TlsUpgraded(IDiagnosticLog log) =>
        Write(log, DiagnosticLogLevel.Info, "STLS upgraded the connection to TLS", string.Empty);

    /// <summary>Logs an accepted login, at <c>info</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="method">How the session logged in, such as <c>SASL PLAIN</c> or <c>APOP</c>.</param>
    public static void LoggedIn(IDiagnosticLog log, string method) =>
        Write(log, DiagnosticLogLevel.Info, "logged in with ", method);

    /// <summary>Logs that none of the offered SASL mechanisms can be used, at <c>warning</c>.</summary>
    /// <param name="log">The diagnostic log.</param>
    /// <param name="offered">The mechanisms the server offered.</param>
    public static void NoUsableMechanism(IDiagnosticLog log, IReadOnlyList<string> offered)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            log.Write(
                DiagnosticLogLevel.Warning,
                DiagnosticLogComponents.Pop3,
                "no usable SASL mechanism among: " + string.Join(' ', offered) + "; logging in without SASL");
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
                DiagnosticLogComponents.Pop3,
                string.Create(CultureInfo.InvariantCulture, $"transfer done, {result.BytesTransferred} bytes in {milliseconds} ms"));
        }
    }

    private static string StatusOf(Pop3Response response)
    {
        if (response.IsOk)
        {
            return "+OK";
        }

        return response.Line[0] == '-' ? "-ERR" : "+";
    }

    private static void TransferFailed(IDiagnosticLog log, TransferResult result)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            log.Write(
                DiagnosticLogLevel.Error,
                DiagnosticLogComponents.Pop3,
                string.Create(CultureInfo.InvariantCulture, $"transfer failed with CurlExitCode.{result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}"));
        }
    }

    private static void Write(IDiagnosticLog log, DiagnosticLogLevel level, string text, string argument)
    {
        if (log.IsEnabled(level))
        {
            log.Write(level, DiagnosticLogComponents.Pop3, text + argument);
        }
    }
}
