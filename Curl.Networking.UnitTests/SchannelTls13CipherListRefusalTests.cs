using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking.UnitTests;

/// <summary>
/// Pins the Schannel build's refusal of a TLS 1.3 minimum with a <c>--ciphers</c> list
/// (BL-5134): exit 35 before any handshake, as curl's Schannel build does.
/// </summary>
[TestClass]
public sealed class SchannelTls13CipherListRefusalTests
{
    private const string ExpectedMessage = "schannel: TLS 1.3 is not supported with a cipher list; remove the cipher list or allow a lower TLS version";

    [TestMethod]
    public void Refusal_Tls13MinimumWithCipherList_IsExit35WithCurlsMessage()
    {
        var refusal = SchannelAlgorithmList.Refusal("CALG_AES_256", TlsVersion.Tls13);

        Assert.AreEqual(CurlExitCode.SslConnectError, refusal!.ExitCode);
        Assert.AreEqual(ExpectedMessage, refusal.ErrorMessage);
    }

    [TestMethod]
    public void OfferedSuites_Tls13MinimumWithCipherList_IsRefusedBeforeAnySuiteIsChosen()
    {
        var (suites, failure) = SchannelAlgorithmList.OfferedSuites("CALG_AES_256", ClientHelloProfile.Schannel.CipherSuites, TlsVersion.Tls13);

        Assert.IsNull(suites);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure!.ExitCode);
        Assert.AreEqual(ExpectedMessage, failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow(TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls12)]
    public void Refusal_CipherListWithoutTls13Minimum_IsNotRefused(TlsVersion minimum)
    {
        Assert.IsNull(SchannelAlgorithmList.Refusal("CALG_AES_256", minimum));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Refusal_Tls13MinimumWithoutCipherList_IsNotRefused(string? ciphers)
    {
        Assert.IsNull(SchannelAlgorithmList.Refusal(ciphers, TlsVersion.Tls13));
    }
}
