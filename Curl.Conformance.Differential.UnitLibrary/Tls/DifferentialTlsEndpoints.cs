using System.Security.Authentication;

namespace Curl.Conformance.Differential.Tls;

/// <summary>
/// The seven TLS servers of the tester's <c>runner.py</c> <c>PORTS</c> table, each configured as
/// <c>tls_server.py</c> was through <c>CERT</c>, <c>MINV</c>, <c>MAXV</c> and <c>CLIENTAUTH</c>,
/// and the substitution of their placeholders and <c>{PKI}</c> into a case.
/// </summary>
public static class DifferentialTlsEndpoints
{
    /// <summary>The TLS versions Python's default server context accepts: 1.2 and 1.3.</summary>
    private const SslProtocols DefaultProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>Gets every TLS server a CLI TLS case can name.</summary>
    public static IReadOnlyList<DifferentialTlsEndpoint> All { get; } =
    [
        new("T", 18100, "server", DefaultProtocols, RequiresClientCertificate: false),
        new("T12", 18110, "server", SslProtocols.Tls12, RequiresClientCertificate: false),
        new("T13", 18120, "server", SslProtocols.Tls13, RequiresClientCertificate: false),
        new("MTLS", 18130, "server", DefaultProtocols, RequiresClientCertificate: true),
        new("WRONG", 18140, "wrong", DefaultProtocols, RequiresClientCertificate: false),
        new("EXP", 18150, "expired", DefaultProtocols, RequiresClientCertificate: false),
        new("RSA", 18160, "rsa", DefaultProtocols, RequiresClientCertificate: false),
    ];

    /// <summary>
    /// Returns <paramref name="differentialCase"/> with every server placeholder replaced by its
    /// <c>127.0.0.1:port</c> and <c>{PKI}</c> by <paramref name="pkiDirectory"/>, written with
    /// <c>/</c> as <c>runner.py</c> wrote it.
    /// </summary>
    /// <param name="differentialCase">The case read from <c>CliTls/CliTlsCases.json</c>.</param>
    /// <param name="pkiDirectory">The directory holding the tester's <c>pki</c> certificates.</param>
    /// <returns>The case with its TLS placeholders substituted.</returns>
    public static DifferentialCase Substitute(DifferentialCase differentialCase, string pkiDirectory)
    {
        ArgumentNullException.ThrowIfNull(differentialCase);
        ArgumentNullException.ThrowIfNull(pkiDirectory);
        var pki = pkiDirectory.Replace('\\', '/');
        return differentialCase with { Arguments = [.. differentialCase.Arguments.Select(argument => SubstituteOne(argument, pki))] };
    }

    private static string SubstituteOne(string argument, string pki) =>
        All.Aggregate(
            argument.Replace("{PKI}", pki, StringComparison.Ordinal),
            (substituted, endpoint) => substituted.Replace("{" + endpoint.Placeholder + "}", endpoint.Authority, StringComparison.Ordinal));
}
