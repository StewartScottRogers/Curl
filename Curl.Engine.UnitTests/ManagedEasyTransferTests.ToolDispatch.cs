using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolDispatch" /> and <see cref="ManagedEasyTransfer.LastContext" /> (BL-2479,
/// ADR-0614): a perform from the options runs through the curl tool's runner's follower and builds its context with
/// the runner's factory, in place of the engine's dispatcher and a factory with no standard input.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_ToolDispatchSet_RunsThroughItsFollowerNotTheEnginesDispatcher()
    {
        RecordingProtocolHandler engines = RecordingProtocolHandler.WritingPath("http");
        RecordingProtocolHandler runners = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(engines);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        easy.ToolDispatch = DispatchOver(runners, new TransferContextFactory(Stream.Null, TimeProvider.System));

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, runners.Contexts);
        Assert.IsEmpty(engines.Contexts);
        Assert.AreEqual("/x", runners.Contexts[0].Url.AbsolutePath);
    }

    [TestMethod]
    public async Task PerformAsync_ToolDispatchSetForATelnetUrl_UploadsTheFactorysStandardInput()
    {
        RecordingProtocolHandler runners = RecordingProtocolHandler.WritingPath("telnet");
        using MemoryStream standardInput = new([1, 2, 3]);
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("telnet"));
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "telnet://h/" } };
        easy.ToolDispatch = DispatchOver(runners, new TransferContextFactory(standardInput, TimeProvider.System) { ReadsTelnetStandardInputInTextMode = false });

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreSame(standardInput, runners.Contexts[0].Upload);
    }

    [TestMethod]
    public async Task PerformAsync_ToolDispatchSet_CarriesTheFactorysCredentialEncoding()
    {
        RecordingProtocolHandler runners = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        easy.ToolDispatch = DispatchOver(runners, new TransferContextFactory(Stream.Null, TimeProvider.System, Encoding.UTF8));

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreSame(Encoding.UTF8, runners.Contexts[0].Http!.CommandLineTextEncoding);
    }

    [TestMethod]
    public async Task PerformAsync_NoToolDispatchForATelnetUrl_UploadsTheEmptyStandardInput()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("telnet");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "telnet://h/" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreSame(Stream.Null, handler.Contexts[0].Upload);
    }

    [TestMethod]
    public async Task LastContext_PerformFromOptions_IsTheContextTheHandlerSaw()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreSame(handler.Contexts[0], easy.LastContext);
        Assert.AreEqual("/x", easy.LastContext!.Url.AbsolutePath);
    }

    [TestMethod]
    public async Task LastContext_PerformWithNoUrlAfterATransfer_IsNull()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };
        await easy.PerformAsync(CancellationToken.None);
        Assert.IsNotNull(easy.LastContext);
        easy.Options = new EasyOptions();

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(easy.LastContext);
    }

    [TestMethod]
    public async Task Duplicate_HandleWithToolDispatch_DoesNotCopyIt()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolDispatch = DispatchOver(RecordingProtocolHandler.WritingPath("http"), new TransferContextFactory(Stream.Null));

        await using IEasyTransfer copy = easy.Duplicate();

        Assert.IsNull(((ManagedEasyTransfer)copy).ToolDispatch);
        Assert.IsNotNull(easy.ToolDispatch);
    }

    [TestMethod]
    public async Task Reset_HandleWithToolDispatch_ClearsIt()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolDispatch = DispatchOver(RecordingProtocolHandler.WritingPath("http"), new TransferContextFactory(Stream.Null));

        easy.Reset();

        Assert.IsNull(easy.ToolDispatch);
    }

    private static EngineToolTransferDispatch DispatchOver(RecordingProtocolHandler handler, TransferContextFactory factory) =>
        new(new RedirectFollower(new ProtocolDispatcher([handler])), factory);
}
