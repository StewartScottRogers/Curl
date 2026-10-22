using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// The QUIC connect reports the origin's certificate verify result as the origin's, never the
/// proxy's, so <c>%{ssl_verify_result}</c> and <c>CURLINFO_SSL_VERIFYRESULT</c> read it (AF-0341).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_OnTheOpenSslBuild_ReportsTheVerifyResultAsTheOrigins()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("Schannel build", false);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider(), matchesSchannelBuild: false), Target(events));

        await using var connection = result.Connection!;
        var reported = events.VerifyResults.Single();
        Diagnostics.Act("verify result", $"{reported.VerifyResult}, proxy {reported.IsProxy}");
        Diagnostics.Assert("reported as the proxy's", false, reported.IsProxy);
        Assert.IsFalse(reported.IsProxy);
    }
}
