using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh;

/// <content>The user authentication methods tried and their outcome (BL-4366).</content>
internal sealed partial class SshDiagnosticLog
{
    /// <summary>Logs, at <c>info</c>, the authentication methods the server allows.</summary>
    /// <param name="methods">The server's method list, or <see langword="null" /> when <c>none</c> succeeded.</param>
    internal void AuthenticationMethodsListed(string? methods)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, methods is null ? "authenticated with none" : "server allows authentication: " + methods);
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an authentication method about to be tried.</summary>
    /// <param name="method">The method, such as <c>password</c>.</param>
    internal void AuthenticationTried(string method)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, "trying authentication with " + method);
        }
    }

    /// <summary>
    /// Logs an authentication method's outcome: <c>info</c> when it authenticated the user,
    /// <c>warning</c> when it did not and curl goes on.
    /// </summary>
    /// <param name="method">The method tried.</param>
    /// <param name="succeeded">Whether it authenticated the user.</param>
    internal void AuthenticationEnded(string method, bool succeeded)
    {
        DiagnosticLogLevel level = succeeded ? DiagnosticLogLevel.Info : DiagnosticLogLevel.Warning;
        if (log.IsEnabled(level))
        {
            Write(level, succeeded ? "authenticated with " + method : method + " did not authenticate the user");
        }
    }
}
