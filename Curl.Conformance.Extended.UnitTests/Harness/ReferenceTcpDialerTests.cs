using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class ReferenceTcpDialerTests
{
    private const int HttpsPort = 8443;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    // A close-delimited body ends only at the close; curl's TLS client fails a close without
    // close_notify ("server closed abruptly"), so exit 0 and the whole body show the dialer sent it.
    [TestMethod]
    public async Task DialAsync_TlsRouteServerReturns_SendsCloseNotifySoCurlReadsACloseDelimitedBody()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        ScriptedReferenceServer server = new(
            ScriptStep.ReceiveUntil("\r\n\r\n"),
            ScriptStep.Send("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nuntil the close\n"));
        ReferenceRoute route = ReferenceRoute.Tls(HttpsPort, server, certificate);

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [route], ["-sS", "-k", "-m", "10", $"https://localhost:{HttpsPort}/x"]);

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("until the close\n", Encoding.Latin1.GetString(result.StandardOutput));
        Assert.AreEqual(string.Empty, Encoding.Latin1.GetString(result.StandardError));
    }
}
