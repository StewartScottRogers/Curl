using System.ComponentModel;
using System.Security.Authentication;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins the Schannel build's exit 35 text for a TLS version mismatch (BL-5393): a
/// <c>protocol_version</c> alert is <c>SEC_E_UNSUPPORTED_FUNCTION</c>, whether the client's
/// minimum is above the server's range (<c>--tlsv1.3</c>) or its maximum below it
/// (<c>--tls-max 1.2</c>); any other fatal alert keeps its line.
/// </summary>
[TestClass]
public sealed class WindowsTlsVersionMismatchTests
{
    private const string Unsupported =
        "schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported";

    private const string FatalAlert =
        "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.";

    [TestMethod]
    [DataRow(TlsVersion.Tls13, TlsAlertDescription.ProtocolVersion, Unsupported)]
    [DataRow(TlsVersion.Tls12, TlsAlertDescription.ProtocolVersion, Unsupported)]
    [DataRow(TlsVersion.Tls13, TlsAlertDescription.HandshakeFailure, FatalAlert)]
    [DataRow(TlsVersion.Tls12, TlsAlertDescription.HandshakeFailure, FatalAlert)]
    public void SchannelHandBuiltHandshakeFailure_ByAlert_IsTheMeasuredLine(TlsVersion maximumVersion, TlsAlertDescription alert, string expected)
    {
        var failure = new TlsHandshakeFailure(alert, null) { Origin = TlsHandshakeFailureOrigin.AlertReceived };

        var message = TlsFailureMessages.SchannelHandBuiltHandshakeFailure(failure, TlsVersion.Tls13, maximumVersion);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    [DataRow("ProtocolVersion", Unsupported)]
    [DataRow("HandshakeFailure", FatalAlert)]
    public void SchannelSslConnectError_ClientMinimumAboveServerRange_ByAlert_IsTheMeasuredLine(string alert, string expected)
    {
        var exception = new AuthenticationException(
            $"Authentication failed because the remote party sent a TLS alert: '{alert}'.",
            new Win32Exception(unchecked((int)0x80090326), "text"));

        var message = TlsFailureMessages.SchannelSslConnectError(exception, TlsVersion.SystemDefault);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    [DataRow("ProtocolVersion", Unsupported)]
    [DataRow("HandshakeFailure", FatalAlert)]
    public void SchannelSslConnectError_ClientMaximumBelowServerRange_ByAlert_IsTheMeasuredLine(string alert, string expected)
    {
        var exception = new AuthenticationException(
            $"Authentication failed because the remote party sent a TLS alert: '{alert}'.",
            new Win32Exception(unchecked((int)0x80090326), "text"));

        var message = TlsFailureMessages.SchannelSslConnectError(exception, TlsVersion.Tls12);

        Assert.AreEqual(expected, message);
    }
}
