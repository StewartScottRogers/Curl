using System.Security.Authentication;
using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-k</c>, <c>--cacert</c>, <c>--capath</c>, <c>--cert</c>, <c>--key</c>,
/// <c>--ciphers</c>, <c>--tls13-ciphers</c>, <c>--cert-type</c>, <c>--key-type</c>, <c>--pass</c>, <c>--ssl-no-revoke</c>, <c>--tlsv1.2</c> and <c>--tlsv1.3</c> become the
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
    public void FromCommandLine_CaCertificateDirectory_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CaCertificateDirectory: "certs"), Map("--capath", "certs", Url));
    }

    [TestMethod]
    public void FromCommandLine_ClientCertificate_CopiesTheValueWithItsPassphraseVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(ClientCertificate: "client.p12:secret"), Map("--cert", "client.p12:secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_PrivateKey_CopiesThePathVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(PrivateKey: "client.key"), Map("--key", "client.key", Url));
    }

    [TestMethod]
    public void FromCommandLine_CertType_CopiesItAsCertificateTypeVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(CertificateType: "p12"), Map("--cert-type", "p12", Url));
    }

    [TestMethod]
    public void FromCommandLine_KeyType_CopiesItAsPrivateKeyTypeVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(PrivateKeyType: "DER"), Map("--key-type", "DER", Url));
    }

    [TestMethod]
    public void FromCommandLine_Pass_CopiesItAsPassphraseVerbatim()
    {
        Assert.AreEqual(new TlsClientOptions(Passphrase: "secret"), Map("--pass", "secret", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevoke_SetsSkipRevocationCheckOnly()
    {
        Assert.AreEqual(new TlsClientOptions(SkipRevocationCheck: true), Map("--ssl-no-revoke", Url));
    }

    [TestMethod]
    public void FromCommandLine_SslNoRevokeWithCaCertificateFile_SetsBoth()
    {
        Assert.AreEqual(
            new TlsClientOptions(CaCertificateFile: "root.pem", SkipRevocationCheck: true),
            Map("--ssl-no-revoke", "--cacert", "root.pem", Url));
    }

    [TestMethod]
    public void FromCommandLine_Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256:BOGUS"),
            Map("--ciphers", "ECDHE-RSA-AES128-GCM-SHA256:BOGUS", Url));
    }

    [TestMethod]
    public void FromCommandLine_Tls13Ciphers_CopiesTheListVerbatim()
    {
        Assert.AreEqual(
            new TlsClientOptions(Tls13Ciphers: "TLS_AES_128_GCM_SHA256"),
            Map("--tls13-ciphers", "TLS_AES_128_GCM_SHA256", Url));
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

    [TestMethod]
    public void ToTlsMinimumVersion_OtherNonNullVersion_MapsToSystemDefault()
    {
        Assert.AreEqual(
            TlsMinimumVersion.SystemDefault,
            TlsClientOptionsMapping.ToTlsMinimumVersion(SslProtocols.None));
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
