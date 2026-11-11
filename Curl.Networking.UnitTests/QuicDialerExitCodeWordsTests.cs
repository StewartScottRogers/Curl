using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Every exit a QUIC connect can fail with has curl's words, and one without reads as a
/// fallback instead of a <see cref="KeyNotFoundException" /> (BL-4914).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenThePinnedKeyDoesNotMatch_FailsWithExit90AndCurlsMessage()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        var connector = Connector(opener, new ManualTimeProvider(), options);

        var result = await ConnectMultiplexedAsync(connector, Target(events));

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual("SSL: public key does not match pinned public key", result.ErrorMessage);
        Assert.IsTrue(events.Info.Any(line => line.EndsWith("failed: SSL public key does not match pinned public key", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void WordsOf_EveryExitCodeTheEnumNames_ReturnsWordsWithoutThrowing()
    {
        foreach (var exitCode in Enum.GetValues<CurlExitCode>())
        {
            Assert.IsFalse(string.IsNullOrEmpty(QuicDialer.WordsOf(exitCode)), exitCode.ToString());
        }
    }

    [TestMethod]
    public void WordsOf_AnExitCodeWithoutWords_ReturnsCurlsUnknownErrorFallback() =>
        Assert.AreEqual("Unknown error", QuicDialer.WordsOf((CurlExitCode)9999));
}
