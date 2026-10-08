using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

namespace Curl.Quic;

[TestClass]
public sealed class QuicHandshakeFailuresTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromServerClose_ApplicationCloseDuringTheHandshake_IsDescribedAndExit7()
    {
        Diagnostics.Arrange("CONNECTION_CLOSE error code / frame type / handshake complete", "0x02 / null (application close) / false");
        QuicConnectionCloseFrame close = new(0x02, null, ReadOnlyMemory<byte>.Empty);

        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(close, handshakeComplete: false);

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure.ExitCode}: {failure.Message}"));
        Diagnostics.Assert("message", "The server closed the QUIC connection with application error 0x2.", failure.Message);
        Assert.AreEqual("The server closed the QUIC connection with application error 0x2.", failure.Message);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Diagnostics.Assert("ServerClose is the close frame", true, ReferenceEquals(close, failure.ServerClose));
        Assert.AreSame(close, failure.ServerClose);
    }

    [TestMethod]
    public void FromServerClose_ErrorPastTheCryptoRange_IsDescribedAsATransportError()
    {
        Diagnostics.Arrange("CONNECTION_CLOSE error code / handshake complete", "0x200 / false");

        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(new QuicConnectionCloseFrame(0x200, 0, ReadOnlyMemory<byte>.Empty), handshakeComplete: false);

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure.ExitCode}: {failure.Message}"));
        Diagnostics.Assert("message", "The server closed the QUIC connection with transport error 512.", failure.Message);
        Assert.AreEqual("The server closed the QUIC connection with transport error 512.", failure.Message);
    }

    [TestMethod]
    [DataRow(0x02UL, DisplayName = "CONNECTION_REFUSED")]
    [DataRow(0x128UL, DisplayName = "A TLS alert")]
    public void FromServerClose_AfterTheHandshake_IsExit56(ulong errorCode)
    {
        Diagnostics.Arrange("CONNECTION_CLOSE error code / handshake complete", FormattableString.Invariant($"0x{errorCode:x} / true"));
        QuicConnectionCloseFrame close = new(errorCode, 0, ReadOnlyMemory<byte>.Empty);

        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(close, handshakeComplete: true);

        Diagnostics.Act("failure", FormattableString.Invariant($"{failure.ExitCode}: {failure.Message}"));
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Diagnostics.Assert("message", "Failure when receiving data from the peer", failure.Message);
        Assert.AreEqual("Failure when receiving data from the peer", failure.Message);
        Diagnostics.Assert("ServerClose is the close frame", true, ReferenceEquals(close, failure.ServerClose));
        Assert.AreSame(close, failure.ServerClose);
    }

    [TestMethod]
    public void FromTransportError_DuringAndAfterTheHandshake_IsExit7ThenExit56()
    {
        Diagnostics.Arrange("transport error", FormattableString.Invariant($"{QuicTransportErrorCode.ProtocolViolation}: bad"));
        QuicTransportException error = new(QuicTransportErrorCode.ProtocolViolation, "bad");

        QuicHandshakeFailure during = QuicHandshakeFailures.FromTransportError(error, handshakeComplete: false);
        QuicHandshakeFailure after = QuicHandshakeFailures.FromTransportError(error, handshakeComplete: true);

        Diagnostics.Act("failure during the handshake", FormattableString.Invariant($"{during.ExitCode}: {during.Message}"));
        Diagnostics.Act("failure after the handshake", FormattableString.Invariant($"{after.ExitCode}: {after.Message}"));
        QuicHandshakeFailure expectedDuring = new(CurlExitCode.CouldntConnect, "QUIC ProtocolViolation: bad");
        Diagnostics.Assert("failure during the handshake", expectedDuring, during);
        Assert.AreEqual(expectedDuring, during);
        QuicHandshakeFailure expectedAfter = new(CurlExitCode.RecvError, "Failure when receiving data from the peer");
        Diagnostics.Assert("failure after the handshake", expectedAfter, after);
        Assert.AreEqual(expectedAfter, after);
        Diagnostics.Assert("ServerClose after the handshake", "null", after.ServerClose is null ? "null" : "set");
        Assert.IsNull(after.ServerClose);
    }

    [TestMethod]
    public void IdleTimeout_IsExit55WithNgtcp2sMessage()
    {
        Diagnostics.Arrange("failure", "QuicHandshakeFailures.IdleTimeout");
        QuicHandshakeFailure actual = QuicHandshakeFailures.IdleTimeout;

        Diagnostics.Act("failure", FormattableString.Invariant($"{actual.ExitCode}: {actual.Message}"));
        QuicHandshakeFailure expected = new(CurlExitCode.SendError, "ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE");
        Diagnostics.Assert("failure", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void CryptoError_Alert_IsTheCryptoErrorBasePlusTheAlert()
    {
        Diagnostics.Arrange("alert", TlsAlertDescription.BadCertificate);

        QuicTransportErrorCode actual = QuicHandshakeFailures.CryptoError(TlsAlertDescription.BadCertificate);

        Diagnostics.Act("error code", FormattableString.Invariant($"0x{(int)actual:x}"));
        Diagnostics.Assert("error code", "0x12a", FormattableString.Invariant($"0x{(int)actual:x}"));
        Assert.AreEqual((QuicTransportErrorCode)0x12a, actual);
    }
}
