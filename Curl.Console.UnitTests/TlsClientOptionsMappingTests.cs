using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-k</c>, <c>--cacert</c>, <c>--tlsv1.2</c> and <c>--tlsv1.3</c> become the
/// <see cref="TlsClientOptions" /> the TLS provider applies. No test here opens a socket or
/// reads a certificate file.
/// </summary>
[TestClass]
public sealed class TlsClientOptionsMappingTests
{
    private const string Url = "gophers://example.com/";

    [TestMethod]
    public void FromCommandLine_NoTlsOptions_VerifiesAgainstSystemStoreAtSystemDefaultVersion()
    {
        Assert.AreEqual(new TlsClientOptions(), Map(Url));
    }

    [TestMethod]
    public void FromCommandLine_Insecure_SetsInsecureOnly()
    {
        Assert.AreEqual(new TlsClientOptions(Insecure: true), Map("-k", Url));
    }

    [TestMethod]
    public void FromCommandLine_CaCertificateFile_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CaCertificateFile: "x.pem"), Map("--cacert", "x.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv12_SetsMinimumVersionTls12()
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: TlsMinimumVersion.Tls12), Map("--tlsv1.2", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13_SetsMinimumVersionTls13()
    {
        Assert.AreEqual(new TlsClientOptions(MinimumVersion: TlsMinimumVersion.Tls13), Map("--tlsv1.3", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tlsv13ThenTlsv12_LastOneWinsAsTls12()
    {
        Assert.AreEqual(
            new TlsClientOptions(MinimumVersion: TlsMinimumVersion.Tls12),
            Map("--tlsv1.3", "--tlsv1.2", Url));
    }

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists, then maps the result.
    /// </summary>
    private static TlsClientOptions Map(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return TlsClientOptionsMapping.FromCommandLine(parsed.Options);
    }
}
