using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

[TestClass]
public sealed class SchannelTrustTextTests
{
    public TestContext TestContext { get; set; } = null!;

    // Measured: curl 8.21.0 (mingw, Schannel) -v -k https://localhost:18431/ (BL-1083).
    [TestMethod]
    public void Lines_HostName_DisablesTheAutomaticClientCertificateOnly()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "VerifiesPeer=false");
        string[] expected = ["schannel: disabled automatic use of client certificate"];

        string[] actual = SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = false }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Measured: curl 8.21.0 (mingw, Schannel) -v -k https://127.0.0.1:18431/ (BL-1083).
    [TestMethod]
    public void Lines_IpAddress_AddsTheSniLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "VerifiesPeer=false, TargetsIpAddress=true");
        string[] expected =
        [
            "schannel: disabled automatic use of client certificate",
            "schannel: using IP address, SNI is not supported by OS.",
        ];

        string[] actual = SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = false, TargetsIpAddress = true }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Measured: curl 8.21.0 (mingw, Schannel) -v -k --ssl-auto-client-cert https://127.0.0.1:18431/ (BL-1083).
    [TestMethod]
    public void Lines_AutomaticClientCertificate_EnablesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "VerifiesPeer=true, UsesAutomaticClientCertificate=true, TargetsIpAddress=true");
        string[] expected =
        [
            "schannel: enabled automatic use of client certificate",
            "schannel: using IP address, SNI is not supported by OS.",
        ];

        string[] actual = SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true, UsesAutomaticClientCertificate = true, TargetsIpAddress = true }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TlsTrust_SchannelOverTcp_ReturnsTheSchannelLines()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "VerifiesPeer=true, backend Schannel");
        string[] expected = ["schannel: disabled automatic use of client certificate"];

        string[] actual = TransferEventInfoText.TlsTrust(new TlsTrustEvent { VerifiesPeer = true }, TlsBackend.Schannel).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Measured: curl 8.22.0 (Schannel) -v --ciphers CALG_AES_128 (BL-4992, ADR-1250).
    [TestMethod]
    public void Lines_CipherList_AddsTheLessSecureWarningBeforeTheSniLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("event", "HasCipherList=true, TargetsIpAddress=true");
        string[] expected =
        [
            "schannel: disabled automatic use of client certificate",
            "schannel: WARNING: This version of Schannel negotiates a less-secure TLS version than TLS 1.3 because the user set an algorithm cipher list.",
            "schannel: using IP address, SNI is not supported by OS.",
        ];

        string[] actual = SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = false, HasCipherList = true, TargetsIpAddress = true }).ToArray();

        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    private static void Report(TestDiagnostics diagnostics, string[] expected, string[] actual)
    {
        string expectedText = string.Join("\n", expected);
        string actualText = string.Join("\n", actual);
        diagnostics.Act("lines", actualText);
        diagnostics.Diff("lines", expectedText, actualText);
    }
}
