using System.Security.Authentication;

namespace Curl.Networking;

/// <summary>
/// CGR-224: a server answering the ClientHello with bytes that are no TLS record is
/// Schannel's <c>SEC_E_INVALID_TOKEN</c> and OpenSSL's <c>wrong version number</c>, not
/// .NET's frame-size text or <c>failed to receive handshake</c>.
/// </summary>
[TestClass]
public sealed class Cgr224Tests
{
    private static AuthenticationException MalformedFrame() =>
        new("Authentication failed, see inner exception.", new IOException("Cannot determine the frame size or a corrupted frame was received."));

    [TestMethod]
    public void SchannelSslConnectError_ServerAnswersWithNoTlsRecord_IsInvalidToken()
    {
        var message = TlsFailureMessages.SchannelSslConnectError(MalformedFrame());

        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid",
            message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_ServerAnswersWithNoTlsRecord_IsWrongVersionNumber()
    {
        var message = TlsFailureMessages.OpenSslSslConnectError(MalformedFrame());

        Assert.AreEqual("TLS connect error: error:0A00010B:SSL routines::wrong version number", message);
    }
}
