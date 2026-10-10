using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins the exit 35 text for a hand-built handshake's failure in both builds (ADR-0140,
/// "Failures and text"): Schannel's two measured lines, and OpenSSL's error string for each
/// alert, with <c>reason(N)</c> for one OpenSSL has no string for.
/// </summary>
public sealed partial class TlsFailureMessagesTests
{
    private const string SchannelHandshakeNotReceived = "schannel: failed to receive handshake, SSL/TLS connection failed";

    private const string SchannelFatalAlertReceived =
        "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.";

    [TestMethod]
    [DataRow(TlsHandshakeFailureOrigin.TransportClosed, false, SchannelHandshakeNotReceived)]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, true, SchannelHandshakeNotReceived)]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, false, SchannelFatalAlertReceived)]
    [DataRow(TlsHandshakeFailureOrigin.AlertSent, false, SchannelFatalAlertReceived)]
    public void SchannelHandBuiltHandshakeFailure_IsTheLineSchannelsCurlPrints(
        TlsHandshakeFailureOrigin origin,
        bool offersOnlyVersionsBelowTls12,
        string expected)
    {
        var failure = new TlsHandshakeFailure(TlsAlertDescription.HandshakeFailure, null) { Origin = origin };

        Diagnostics.Arrange("origin, offersOnlyVersionsBelowTls12", $"{origin}, {offersOnlyVersionsBelowTls12}");

        var message = TlsFailureMessages.SchannelHandBuiltHandshakeFailure(failure, offersOnlyVersionsBelowTls12);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", expected, message);

        Assert.AreEqual(expected, TlsFailureMessages.SchannelHandBuiltHandshakeFailure(failure, offersOnlyVersionsBelowTls12));
    }

    [TestMethod]
    [DataRow(TlsHandshakeFailureOrigin.TransportClosed, TlsAlertDescription.CloseNotify, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, TlsAlertDescription.HandshakeFailure, "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, TlsAlertDescription.ProtocolVersion, "TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version")]
    [DataRow(TlsHandshakeFailureOrigin.AlertSent, TlsAlertDescription.ProtocolVersion, "TLS connect error: error:0A000102:SSL routines::unsupported protocol")]
    [DataRow(TlsHandshakeFailureOrigin.AlertSent, TlsAlertDescription.DecodeError, "TLS connect error: error:0A00041A:SSL routines::tlsv1 alert decode error")]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, TlsAlertDescription.UnknownPskIdentity, "TLS connect error: error:0A00045B:SSL routines::tlsv1 alert unknown psk identity")]
    [DataRow(TlsHandshakeFailureOrigin.AlertReceived, TlsAlertDescription.EchRequired, "TLS connect error: error:0A000461:SSL routines::reason(1121)")]
    public void OpenSslHandBuiltHandshakeFailure_IsOpenSslsErrorStringForTheFailure(
        TlsHandshakeFailureOrigin origin,
        TlsAlertDescription alert,
        string expected)
    {
        var failure = new TlsHandshakeFailure(alert, null) { Origin = origin };

        Diagnostics.Arrange("origin, alert", $"{origin}, {alert}");

        var message = TlsFailureMessages.OpenSslHandBuiltHandshakeFailure(failure);

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", expected, message);

        Assert.AreEqual(expected, TlsFailureMessages.OpenSslHandBuiltHandshakeFailure(failure));
    }
}
