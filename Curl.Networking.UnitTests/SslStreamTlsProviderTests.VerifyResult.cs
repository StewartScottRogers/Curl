using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the certificate verify code the provider reports for <c>%{ssl_verify_result}</c> and
/// <c>%{proxy_ssl_verify_result}</c> (BL-661). Measured 2026-09-30 against curl 8.18.0
/// (OpenSSL 3.5.5) and <c>openssl s_server</c> with a self-signed <c>localhost</c> certificate:
/// <c>-k</c> printed 18, <c>--cacert</c> for it 0, no option 18 with exit 60, a host name
/// the certificate does not name with <c>--cacert</c> 1 with exit 60, and
/// <c>--proxy-insecure</c> through it as an HTTPS proxy <c>0 18</c>.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_SelfSignedServerUnderInsecure_ReportsVerifyResult18()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (18L, false) }, events.VerifyResults);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_ServerTrustedThroughCaCertificateFile_ReportsVerifyResult0()
    {
        var caFile = Path.Combine(_caFileDirectory, "ca.pem");
        await File.WriteAllTextAsync(caFile, s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (0L, false) }, events.VerifyResults);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SelfSignedServerRefused_ReportsVerifyResult18AsItFails()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(), events);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        CollectionAssert.AreEqual(new[] { (18L, false) }, events.VerifyResults);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HostNameTheTrustedCertificateDoesNotName_ReportsVerifyResult1()
    {
        var caFile = Path.Combine(_caFileDirectory, "ca.pem");
        await File.WriteAllTextAsync(caFile, s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), events, targetHost: "other.test");

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        CollectionAssert.AreEqual(new[] { (1L, false) }, events.VerifyResults);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HostNameMismatchUnderInsecure_ReportsTheChainsCode()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events, targetHost: "other.test");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (18L, false) }, events.VerifyResults);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SelfSignedProxyUnderInsecure_ReportsVerifyResult18AsTheProxys()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events, isProxy: true);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (18L, true) }, events.VerifyResults);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SchannelBuild_ReportsNoVerifyResult()
    {
        // curl 8.21.0 (Schannel) printed ssl_verify_result 0 even for a failed verification (ADR-0043).
        var events = new RecordingTransferEvents();

        var result = await AlpnReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events, []);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsEmpty(events.VerifyResults);
        await result.Connection!.DisposeAsync();
    }
}
