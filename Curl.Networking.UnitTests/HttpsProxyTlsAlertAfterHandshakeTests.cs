using System.ComponentModel;
using Curl.Networking;

namespace Curl.Networking.UnitTests;

/// <summary>BL-5388: a TLS alert read after the handshake inside an HTTPS proxy's tunnel is a Recv failure, exit 56.</summary>
[TestClass]
public sealed class HttpsProxyTlsAlertAfterHandshakeTests
{
    private const int SecEIllegalMessage = unchecked((int)0x80090326);
    private const int SecEDecryptFailure = unchecked((int)0x80090330);

    private static IOException AlertFailure(int code) => new("read failed", new Win32Exception(code));

    private static void AssertRecvFailure(string? message)
    {
        Assert.IsNotNull(message);
        Assert.IsTrue(
            message.StartsWith("Recv failure: Connection was aborted", StringComparison.Ordinal)
            || message.StartsWith("Recv failure: Connection was reset", StringComparison.Ordinal),
            message);
    }

    [TestMethod]
    [DataRow(SecEIllegalMessage)]
    [DataRow(SecEDecryptFailure)]
    public void AlertReadFailure_SchannelInsideTlsTunnel_IsRecvFailure(int code) =>
        AssertRecvFailure(TlsFailureMessages.AlertReadFailure(AlertFailure(code), matchesSchannelBuild: true, presentedClientCertificate: false, insideTlsTunnel: true));

    [TestMethod]
    public void AlertReadFailure_SchannelDirect_KeepsTheSchannelReadText()
    {
        var message = TlsFailureMessages.AlertReadFailure(AlertFailure(SecEIllegalMessage), matchesSchannelBuild: true, presentedClientCertificate: false);

        Assert.IsNotNull(message);
        Assert.StartsWith("schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326)", message);
    }

    [TestMethod]
    public void SchannelHandBuiltAlertReadFailure_InsideTlsTunnel_IsRecvFailure() =>
        AssertRecvFailure(TlsFailureMessages.SchannelHandBuiltAlertReadFailure(automaticClientCertificate: false, insideTlsTunnel: true));

    [TestMethod]
    public void SchannelHandBuiltAlertReadFailure_Direct_KeepsTheSchannelReadText() =>
        Assert.StartsWith("schannel: failed to read data from server", TlsFailureMessages.SchannelHandBuiltAlertReadFailure(automaticClientCertificate: false));
}
