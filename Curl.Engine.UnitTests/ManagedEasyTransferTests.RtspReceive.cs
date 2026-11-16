using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// A libcurl handle's RTSP RECEIVE after SETUP and PLAY (BL-5291): it reads on the session's own connection and
// keeps the session ID and the CSeq counters.
public sealed partial class ManagedEasyTransferTests
{
    private const string RtspSetupReply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: abc123\r\nTransport: RTP/AVP/TCP;interleaved=0-1\r\n\r\n";

    private const string RtspPlayReply = "RTSP/1.0 200 OK\r\nCSeq: 2\r\nSession: abc123\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_RtspReceiveAfterSetupAndPlay_ReadsTheSameConnectionAndKeepsTheSession()
    {
        List<byte[]> frames = [];
        RtspPacedConnector server = new(2, false, RtspSetupReply, RtspPlayReply, RtpFrame);

        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: new EngineDialing(new RtspPacedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure)));
        await using IEasyTransfer easy = engine.CreateEasy();

        CurlCode setup = await PerformRtspAsync(easy, RtspRequest.Setup, frames);
        CurlCode play = await PerformRtspAsync(easy, RtspRequest.Play, frames);
        string playText = easy.ErrorText;
        CurlCode receive = await PerformRtspAsync(easy, RtspRequest.Receive, frames);

        Assert.AreEqual(CurlCode.Ok, setup);
        Assert.AreEqual(CurlCode.Ok, play, $"{playText} connects={server.Connects}");
        Assert.AreEqual(CurlCode.Ok, receive);
        Assert.HasCount(1, frames);
        Assert.AreEqual(1, server.Connects);
        Assert.AreEqual("abc123", easy.Info.RtspSessionId);
        Assert.AreEqual(2, easy.Info.RtspCSeqReceived);
        Assert.AreEqual(3, easy.Info.RtspClientCSeq);
    }

    [TestMethod]
    public async Task PerformAsync_RtspReceiveWithNothingToRead_ReturnsOperationTimedOutAfterTheTimeout()
    {
        List<byte[]> frames = [];
        RtspPacedConnector server = new(2, true, RtspSetupReply, RtspPlayReply);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: new EngineDialing(new RtspPacedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure)));
        await using IEasyTransfer easy = engine.CreateEasy();
        await PerformRtspAsync(easy, RtspRequest.Setup, frames);
        await PerformRtspAsync(easy, RtspRequest.Play, frames);

        CurlCode receive = await PerformRtspAsync(easy, RtspRequest.Receive, frames, TimeSpan.FromMilliseconds(300));

        Assert.AreEqual(CurlCode.OperationTimedOut, receive);
        Assert.IsEmpty(frames);
        Assert.AreEqual(1, server.Connects);
    }

    private static Task<CurlCode> PerformRtspAsync(IEasyTransfer easy, RtspRequest request, List<byte[]> frames, TimeSpan timeout = default)
    {
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "rtsp://127.0.0.1:47950/media" },
            Callbacks = new()
            {
                Interleave = data =>
                {
                    frames.Add(data.ToArray());
                    return data.Length;
                },
            },
            Transfer = new EasyTransferOptions { Timeout = timeout },
            Protocols = new EasyProtocolOptions
            {
                Rtsp = new EasyRtspOptions { Request = request, StreamUri = "rtsp://127.0.0.1:47950/media", Transport = "RTP/AVP/TCP;interleaved=0-1" },
            },
        };
        return easy.PerformAsync(CancellationToken.None).AsTask();
    }
}
