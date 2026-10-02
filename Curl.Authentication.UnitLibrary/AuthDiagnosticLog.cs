using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Writes the authentication decisions to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Auth" /> (ADR-0222, BL-923): authentication that ends the
/// transfer as <c>error</c>, a scheme or mechanism that cannot be used as <c>warning</c>, the
/// scheme or mechanism chosen and what the server offered as <c>info</c>, and each round of a
/// multi-round scheme, the Digest parameters and which security context answers as
/// <c>verbose</c>.
/// </summary>
/// <param name="log">Where the lines go; <see langword="null" /> writes nothing, as <see cref="NoDiagnosticLog.Instance" /> does.</param>
/// <remarks>
/// Every method tests <see cref="IDiagnosticLog.IsEnabled" /> before it builds its message, so a
/// disabled level costs no formatting. Every message is built from scheme, mechanism, algorithm
/// and status names, the host and the SigV4 scope only: never a password, key, signature, token
/// or message byte (ADR-0222, decision 7), and a user name only as the login of the netrc entry
/// that matched, which ADR-0222 logs at <c>info</c> (BL-1151).
/// </remarks>
internal sealed class AuthDiagnosticLog(IDiagnosticLog? log)
{
    private readonly IDiagnosticLog log = log ?? NoDiagnosticLog.Instance;

    /// <summary>
    /// Logs, at <c>info</c>, the HTTP scheme picked from what a challenge offered and what the
    /// command line allows, or, at <c>warning</c>, that no offered scheme is allowed.
    /// </summary>
    /// <param name="offered">The schemes the challenge offered.</param>
    /// <param name="allowed">The schemes the command line allows.</param>
    /// <param name="picked">The scheme picked; <see cref="HttpAuthSchemes.None" /> for none.</param>
    public void HttpSchemePicked(HttpAuthSchemes offered, HttpAuthSchemes allowed, HttpAuthSchemes picked)
    {
        if (picked == HttpAuthSchemes.None)
        {
            Warning(() => $"server offered {offered}; none of the allowed {allowed} can answer");
            return;
        }

        Info(() => $"server offered {offered}; allowed {allowed}; chose {picked}");
    }

    /// <summary>Logs, at <c>warning</c>, that the Negotiate context made no token, so no Negotiate answer is sent.</summary>
    public void NegotiateMadeNoToken() =>
        Warning(() => "Negotiate context made no token; no Negotiate answer sent");

    /// <summary>Logs, at <c>verbose</c>, the algorithm and quality of protection a Digest answer uses.</summary>
    /// <param name="algorithmName">The challenge's <c>algorithm</c>, or <see langword="null" /> for none (MD5).</param>
    /// <param name="qop">The <c>qop</c> chosen, or <see langword="null" /> for none.</param>
    public void DigestParametersChosen(string? algorithmName, string? qop) =>
        Verbose(() => $"Digest algorithm {algorithmName ?? "MD5 (not named)"}, qop {qop ?? "none"}");

    /// <summary>Logs, at <c>verbose</c>, one round of a multi-round scheme.</summary>
    /// <param name="round">What the round did, such as <c>NTLM type 1 sent</c>.</param>
    public void Round(string round) => Verbose(() => round);

    /// <summary>Logs, at <c>warning</c>, that a scheme's answer was refused or could not be made, so nothing is sent for it.</summary>
    /// <param name="scheme">The scheme or mechanism.</param>
    /// <param name="reason">Why, with no credential in it.</param>
    public void Skipped(string scheme, string reason) => Warning(() => $"{scheme} skipped: {reason}");

    /// <summary>Logs, at <c>error</c>, authentication that ends the transfer, with its exit code and reason.</summary>
    /// <param name="scheme">The scheme or mechanism.</param>
    /// <param name="exitCode">The exit code the transfer ends with.</param>
    /// <param name="reason">Why, with no credential in it.</param>
    public void Failed(string scheme, CurlExitCode exitCode, string reason)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Error))
        {
            Write(DiagnosticLogLevel.Error, string.Create(
                CultureInfo.InvariantCulture,
                $"{scheme} authentication failed with exit {(int)exitCode} ({exitCode}): {reason}"));
        }
    }

    /// <summary>
    /// Logs, at <c>info</c>, the SASL mechanism picked from the server's list, or, at
    /// <c>warning</c>, that none of them can be used.
    /// </summary>
    /// <param name="offered">The mechanisms the server offered.</param>
    /// <param name="picked">The mechanism picked; <see langword="null" /> for none.</param>
    public void SaslMechanismPicked(IReadOnlyList<string> offered, string? picked)
    {
        if (picked is null)
        {
            Warning(() => $"server offered SASL {string.Join(' ', offered)}; none can be used");
            return;
        }

        Info(() => $"server offered SASL {string.Join(' ', offered)}; chose {picked}");
    }

    /// <summary>Logs, at <c>verbose</c>, which security context answers a request, and why.</summary>
    /// <param name="request">The request the context is made for; only its mechanism and host are written.</param>
    /// <param name="context">Which context, such as <c>SSPI</c> or <c>hand-built</c>.</param>
    /// <param name="why">Why that one.</param>
    public void SecurityContextChosen(SecurityContextRequest request, string context, string why) =>
        Verbose(() => $"{request.Mechanism} context for {request.HostName}: {context} ({why})");

    /// <summary>Logs, at <c>info</c>, the netrc entry that matched, by host and login only (BL-1151).</summary>
    /// <param name="hostName">The host the entry was looked up for.</param>
    /// <param name="login">The entry's login, or <see langword="null" /> for an entry with none.</param>
    public void NetrcEntryMatched(string hostName, string? login) =>
        Info(() => $"netrc entry matched for host {hostName}, login {login ?? "(none)"}");

    /// <summary>Logs, at <c>info</c>, the AWS SigV4 scope a request is signed in: never the key or signature (BL-1151).</summary>
    /// <param name="provider">The providers, such as <c>aws:amz</c>.</param>
    /// <param name="region">The region.</param>
    /// <param name="service">The service.</param>
    public void AwsSigV4ScopeChosen(string provider, string region, string service) =>
        Info(() => $"AWS SigV4 scope: provider {provider}, region {region}, service {service}");

    /// <summary>Logs, at <c>warning</c>, a Negotiate context step that failed, with its status (BL-1151).</summary>
    /// <param name="status">The step's status.</param>
    public void NegotiateContextFailed(SecurityContextStatus status) =>
        Warning(() => $"Negotiate context failed: {status}");

    private void Info(Func<string> message)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Info))
        {
            Write(DiagnosticLogLevel.Info, message());
        }
    }

    private void Warning(Func<string> message)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Warning))
        {
            Write(DiagnosticLogLevel.Warning, message());
        }
    }

    private void Verbose(Func<string> message)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, message());
        }
    }

    private void Write(DiagnosticLogLevel level, string message) => log.Write(level, DiagnosticLogComponents.Auth, message);
}
