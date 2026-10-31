using Curl.Protocol.Abstractions;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Authentication;

/// <summary>
/// Writes the user authentication steps (RFC 4252) to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925), beside the transport
/// layer's lines: the methods the server allows and the method that authenticated as
/// <c>info</c>, each method tried as <c>verbose</c>, and a method refused as <c>warning</c>.
/// </summary>
/// <param name="transportLog">The transport layer's log, whose diagnostic log these lines also go to.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message,
/// so a disabled level costs no formatting. No method takes a password, a pass phrase or a
/// private key (ADR-0222, decision 7).
/// </remarks>
internal sealed class SshAuthenticationLog(SshTransportLog transportLog)
{
    private readonly IDiagnosticLog log = transportLog.Log;

    /// <summary>Writes the authentication methods the server allows, or that it accepted the user with none.</summary>
    /// <param name="methods">The methods the server allows; <see langword="null" /> when it accepted <c>none</c>.</param>
    internal void AuthenticationMethodsListed(string? methods)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, methods is null ? "authenticated with none" : "server allows authentication: " + methods);
        }
    }

    /// <summary>Writes that one authentication method is being tried.</summary>
    /// <param name="method">The method's name.</param>
    internal void AuthenticationTried(string method)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "trying authentication with " + method);
        }
    }

    /// <summary>Writes whether one authentication method authenticated the user.</summary>
    /// <param name="method">The method's name.</param>
    /// <param name="succeeded">Whether the method authenticated the user.</param>
    internal void AuthenticationEnded(string method, bool succeeded)
    {
        DiagnosticLogLevel level = succeeded ? DiagnosticLogLevel.Info : DiagnosticLogLevel.Warning;
        if (log.IsEnabled(level))
        {
            Write(level, succeeded ? "authenticated with " + method : method + " did not authenticate the user");
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Ssh, message);
}
