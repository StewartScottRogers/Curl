using Curl.Protocol.Abstractions;

namespace Curl.Output;

[TestClass]
public sealed class SchannelTrustTextTests
{
    // Measured: curl 8.21.0 (mingw, Schannel) -v -k https://localhost:18431/ (BL-1083).
    [TestMethod]
    public void Lines_HostName_DisablesTheAutomaticClientCertificateOnly()
    {
        CollectionAssert.AreEqual(
            new[] { "schannel: disabled automatic use of client certificate" },
            SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = false }).ToArray());
    }

    // Measured: curl 8.21.0 (mingw, Schannel) -v -k https://127.0.0.1:18431/ (BL-1083).
    [TestMethod]
    public void Lines_IpAddress_AddsTheSniLine()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "schannel: disabled automatic use of client certificate",
                "schannel: using IP address, SNI is not supported by OS.",
            },
            SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = false, TargetsIpAddress = true }).ToArray());
    }

    // Measured: curl 8.21.0 (mingw, Schannel) -v -k --ssl-auto-client-cert https://127.0.0.1:18431/ (BL-1083).
    [TestMethod]
    public void Lines_AutomaticClientCertificate_EnablesIt()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "schannel: enabled automatic use of client certificate",
                "schannel: using IP address, SNI is not supported by OS.",
            },
            SchannelTrustText.Lines(new TlsTrustEvent { VerifiesPeer = true, UsesAutomaticClientCertificate = true, TargetsIpAddress = true }).ToArray());
    }

    [TestMethod]
    public void TlsTrust_SchannelOverTcp_ReturnsTheSchannelLines()
    {
        CollectionAssert.AreEqual(
            new[] { "schannel: disabled automatic use of client certificate" },
            TransferEventInfoText.TlsTrust(new TlsTrustEvent { VerifiesPeer = true }, TlsBackend.Schannel).ToArray());
    }
}
