using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The methods a SOCKS5 greeting offers and how GSS-API authenticates with them (BL-615):
/// <c>--socks5-basic</c>, <c>--socks5-gssapi</c>, <c>--socks5-gssapi-service</c> and
/// <c>--socks5-gssapi-nec</c>, and the contexts GSS-API runs on.
/// </summary>
/// <param name="AllowUserNameAndPassword">
/// Whether user name and password (RFC 1929) is offered when there is a proxy credential, and
/// answered when the proxy picks it. Without it the credential is never sent.
/// </param>
/// <param name="AllowGssapi">Whether GSS-API (RFC 1961) is offered and answered.</param>
/// <remarks>
/// curl 8.21.0 allows both unless <c>--socks5-basic</c> or <c>--socks5-gssapi</c> names one:
/// then only the ones named (measured; BL-615's Notes). ADR-0276 records the choices.
/// </remarks>
public sealed record Socks5AuthenticationOptions(bool AllowUserNameAndPassword, bool AllowGssapi)
{
    /// <summary>The service name GSS-API asks for when none is given, as curl's is.</summary>
    public const string DefaultGssapiServiceName = "rcmd";

    /// <summary>Gets curl's default: both methods allowed, the platform's texts and no contexts.</summary>
    public static Socks5AuthenticationOptions Default { get; } = new(true, true);

    /// <summary>
    /// Gets the service GSS-API authenticates to: <c>--socks5-gssapi-service</c>, else
    /// <c>--proxy-service-name</c>, else <see cref="DefaultGssapiServiceName" />. A value with a
    /// <c>/</c> names the whole target, as curl takes it; any other is joined to the proxy's host.
    /// </summary>
    public string GssapiServiceName { get; init; } = DefaultGssapiServiceName;

    /// <summary>
    /// Gets whether the protection-level message goes unwrapped, as the NEC SOCKS5 server
    /// expects it: <c>--socks5-gssapi-nec</c>.
    /// </summary>
    public bool GssapiNec { get; init; }

    /// <summary>Gets what <c>--delegation</c> asks of the GSS-API context.</summary>
    public SecurityDelegation GssapiDelegation { get; init; }

    /// <summary>
    /// Gets the factory GSS-API's Kerberos contexts come from; <see langword="null" />, the
    /// default, fails GSS-API as a mechanism with no credential does.
    /// </summary>
    public ISecurityContextFactory? SecurityContexts { get; init; }

    /// <summary>
    /// Gets whether failures read as curl's SSPI build words them (Windows, the default there)
    /// rather than as its GSS-API build does (everywhere else).
    /// </summary>
    public bool UsesSspiTexts { get; init; } = OperatingSystem.IsWindows();

    /// <summary>
    /// Gets the credential cache MIT names when it has no credential, for the GSS-API build's
    /// failure text; <see cref="DefaultCredentialCacheName" /> of the running process by default.
    /// </summary>
    public string CredentialCacheName { get; init; } = DefaultCredentialCacheName(Environment.GetEnvironmentVariable, ReadFileOrNull);

    /// <summary>
    /// The cache MIT's <c>krb5_cc_default_name</c> gives: <c>KRB5CCNAME</c> as it is, else
    /// <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c> with the real user ID from <c>/proc/self/status</c>,
    /// or 0 where that file is absent (ADR-0276).
    /// </summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable.</param>
    /// <param name="readFile">Reads a whole file, or gives <see langword="null" /> when it cannot.</param>
    /// <returns>The cache name.</returns>
    public static string DefaultCredentialCacheName(Func<string, string?> getEnvironmentVariable, Func<string, string?> readFile)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(readFile);
        if (getEnvironmentVariable("KRB5CCNAME") is { Length: > 0 } cacheName)
        {
            return cacheName;
        }

        return "FILE:/tmp/krb5cc_" + RealUserId(readFile("/proc/self/status"));
    }

    // The first number of the Uid: line, or 0.
    private static string RealUserId(string? status)
    {
        foreach (var line in (status ?? string.Empty).Split('\n'))
        {
            if (line.StartsWith("Uid:", StringComparison.Ordinal))
            {
                return line[4..].Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0";
            }
        }

        return "0";
    }

    private static string? ReadFileOrNull(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
