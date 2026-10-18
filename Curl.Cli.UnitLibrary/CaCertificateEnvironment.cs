namespace Curl.Cli;

/// <summary>
/// Takes an option group's CA certificates from the environment when the command line names none, as
/// curl 8.22.0's tool does in <c>transfer_per_config</c> and <c>cacertpaths</c> (<c>src/tool_operate.c</c>,
/// ADR-0682): with no <c>--cacert</c> or <c>--capath</c>, and the server's certificate checked
/// (no <c>-k</c>, or a <c>--doh-url</c> without <c>--doh-insecure</c>), <c>CURL_CA_BUNDLE</c> sets the CA file;
/// without it, <c>SSL_CERT_DIR</c> sets the CA folder and <c>SSL_CERT_FILE</c> the CA file. curl's Schannel
/// build, which Curl matches on Windows, reads none of the three.
/// </summary>
internal static class CaCertificateEnvironment
{
    /// <summary>The variable that names the CA file before the other two.</summary>
    internal const string CurlCaBundleVariableName = "CURL_CA_BUNDLE";

    /// <summary>The variable that names the CA folder when <c>CURL_CA_BUNDLE</c> is not set.</summary>
    internal const string SslCertDirVariableName = "SSL_CERT_DIR";

    /// <summary>The variable that names the CA file when <c>CURL_CA_BUNDLE</c> is not set.</summary>
    internal const string SslCertFileVariableName = "SSL_CERT_FILE";

    /// <summary>
    /// Sets <paramref name="group"/>'s <see cref="CommandLineOptions.CaCertificateFile"/> and
    /// <see cref="CommandLineOptions.CaCertificateDirectory"/> from the environment, verbatim, when curl's
    /// tool would; leaves the group as it is otherwise.
    /// </summary>
    /// <param name="group">One option group of a fully read command line.</param>
    internal static void Apply(CommandLineOptions group)
    {
        if (!ReadsEnvironment(group))
        {
            return;
        }

        Func<string, string?> environment = group.ReadEnvironmentVariable;
        string? bundle = environment(CurlCaBundleVariableName);
        if (bundle is not null)
        {
            group.CaCertificateFile = bundle;
            return;
        }

        group.CaCertificateDirectory = environment(SslCertDirVariableName);
        group.CaCertificateFile = environment(SslCertFileVariableName);
    }

    private static bool ReadsEnvironment(CommandLineOptions group) =>
        !group.ActsAsWindowsSchannelBuild
        && group.CaCertificateFile is null
        && group.CaCertificateDirectory is null
        && (!group.Insecure || (group.DohUrl is not null && !group.DohInsecure));
}
