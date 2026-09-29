using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Quic;

[TestClass]
public sealed class QuicHandshakeFailuresTests
{
    [TestMethod]
    public void FromServerClose_ApplicationClose_IsDescribedAndExit7()
    {
        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(new QuicConnectionCloseFrame(0x02, null, ReadOnlyMemory<byte>.Empty));

        Assert.AreEqual("The server closed the QUIC connection with application error 0x2.", failure.Message);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
    }

    [TestMethod]
    public void FromServerClose_ErrorPastTheCryptoRange_IsDescribedAsATransportError()
    {
        QuicHandshakeFailure failure = QuicHandshakeFailures.FromServerClose(new QuicConnectionCloseFrame(0x200, 0, ReadOnlyMemory<byte>.Empty));

        Assert.AreEqual("The server closed the QUIC connection with transport error 512.", failure.Message);
    }

    [TestMethod]
    public void CryptoError_Alert_IsTheCryptoErrorBasePlusTheAlert() =>
        Assert.AreEqual((QuicTransportErrorCode)0x12a, QuicHandshakeFailures.CryptoError(TlsAlertDescription.BadCertificate));
}
