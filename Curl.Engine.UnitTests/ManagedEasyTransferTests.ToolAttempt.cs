using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolAttempt" /> (ADR-0552): a perform with an attempt the curl tool's
/// runner set up performs that attempt, keeps its whole result, fills <see cref="ManagedEasyTransfer.Info" />
/// from it, and clears the attempt so the next perform runs from the options.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_WithAToolAttempt_PerformsItAndKeepsItsResult()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/abc" } };
        MemoryStream output = new();
        EngineTransferState state = new() { ConnectionId = 4 };
        easy.ToolAttempt = AttemptOf(handler, state, "http://h/abc", output);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("/abc", System.Text.Encoding.ASCII.GetString(output.ToArray()));
        Assert.HasCount(1, handler.Contexts);
        Assert.IsNotNull(easy.ToolAttemptResult);
        Assert.IsTrue(easy.ToolAttemptResult.IsSuccess);
        Assert.IsNull(easy.ToolAttempt);
        Assert.AreEqual(string.Empty, easy.ErrorText);
        Assert.AreEqual("http://h/abc", easy.Info.EffectiveUrl);
        Assert.AreEqual(4, easy.Info.ConnectionId);
    }

    [TestMethod]
    public async Task PerformAsync_WithAFailingToolAttemptAndNoUrlSet_RecordsItsMessageAndNoConnection()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.Failing("http", CurlExitCode.CouldntConnect, "Failed to connect");
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.ToolAttempt = AttemptOf(handler, new EngineTransferState(), "http://h/", new MemoryStream());

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual("Failed to connect", easy.ErrorText);
        Assert.AreEqual(CurlExitCode.CouldntConnect, easy.ToolAttemptResult!.ExitCode);
        Assert.AreEqual(string.Empty, easy.Info.EffectiveUrl);
        Assert.AreEqual(ManagedEasyTransfer.NoConnectionId, easy.Info.ConnectionId);
    }

    [TestMethod]
    public async Task PerformAsync_AfterAToolAttempt_RunsFromTheOptionsAgain()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedCurlEngine engine = new();
        ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.ToolAttempt = AttemptOf(handler, new EngineTransferState(), "http://h/", new MemoryStream());
        await easy.PerformAsync(CancellationToken.None);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual(ManagedEasyTransfer.NoUrlSetText, easy.ErrorText);
        Assert.HasCount(1, handler.Contexts);
    }

    private static ToolAttempt AttemptOf(RecordingProtocolHandler handler, EngineTransferState state, string url, Stream output) =>
        new(
            state,
            new RedirectFollower(new ProtocolDispatcher([handler])),
            new TransferContext { Url = CurlUrl.Parse(url), Output = output },
            new RedirectPolicy());
}
