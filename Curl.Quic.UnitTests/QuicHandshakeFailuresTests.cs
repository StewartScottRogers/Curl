using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

[TestClass]
public sealed class QuicHandshakeFailuresTests
{
    [TestMethod]
    public void FromServerClose_ApplicationCloseDuringTheHandshake_IsDescribedAndExit7()
    {
        QuicConnectionCloseFrame close = new(0x02, null, ReadOnlyMemory<byte>.Empty);

        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(close, handshakeComplete: false);

        Assert.AreEqual("The server closed the QUIC connection with application error 0x2.", failure.Message);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreSame(close, failure.ServerClose);
    }

    [TestMethod]
    public void FromServerClose_ErrorPastTheCryptoRange_IsDescribedAsATransportError()
    {
        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(new QuicConnectionCloseFrame(0x200, 0, ReadOnlyMemory<byte>.Empty), handshakeComplete: false);

        Assert.AreEqual("The server closed the QUIC connection with transport error 512.", failure.Message);
    }

    [TestMethod]
    [DataRow(0x02UL, DisplayName = "CONNECTION_REFUSED")]
    [DataRow(0x128UL, DisplayName = "A TLS alert")]
    public void FromServerClose_AfterTheHandshake_IsExit56(ulong errorCode)
    {
        QuicConnectionCloseFrame close = new(errorCode, 0, ReadOnlyMemory<byte>.Empty);

        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(close, handshakeComplete: true);

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", failure.Message);
        Assert.AreSame(close, failure.ServerClose);
    }

    [TestMethod]
    public void FromTransportError_DuringAndAfterTheHandshake_IsExit7ThenExit56()
    {
        QuicTransportException error = new(QuicTransportErrorCode.ProtocolViolation, "bad");

        QuicHandshakeFailure during = QuicHandshakeFailures.FromTransportError(error, handshakeComplete: false);
        QuicHandshakeFailure after = QuicHandshakeFailures.FromTransportError(error, handshakeComplete: true);

        Assert.AreEqual(new QuicHandshakeFailure(CurlExitCode.CouldntConnect, "QUIC ProtocolViolation: bad"), during);
        Assert.AreEqual(new QuicHandshakeFailure(CurlExitCode.RecvError, "Failure when receiving data from the peer"), after);
        Assert.IsNull(after.ServerClose);
    }

    [TestMethod]
    public void IdleTimeout_IsExit55WithNgtcp2sMessage() =>
        Assert.AreEqual(new QuicHandshakeFailure(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE"), QuicHandshakeFailures.IdleTimeout);

    [TestMethod]
    public void CryptoError_Alert_IsTheCryptoErrorBasePlusTheAlert() =>
        Assert.AreEqual((QuicTransportErrorCode)0x12a, QuicHandshakeFailures.CryptoError(TlsAlertDescription.BadCertificate));
}
