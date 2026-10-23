using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// The HSTS read and write callbacks through a handle (BL-2421, ADR-0620): the read callback's entries loaded
// before the perform, so an http URL they name is switched to https, and the cache handed to the write callback
// when the handle is cleaned up.
public sealed partial class ManagedEasyTransferTests
{
    private const string HstsOk = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_HstsReadCallbackUnderEnable_LoadsSeveralEntriesAndCleanupHandsThemToTheWriteCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(HstsOk)]);
        List<(HstsEntry Entry, int Index, int Total)> saved = [];
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = HstsOptionsOf(
            "http://h/x",
            CallbackHstsCacheTests.ReadOf([new HstsEntry("h", false, null), new HstsEntry("other.example", true, "20991231 00:00:00")], HstsResult.Done),
            RecordingHstsWrite(saved));

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        int savedBeforeCleanup = saved.Count;
        await easy.DisposeAsync();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(443, connector.Targets.Single().Port);
        Assert.AreEqual(0, savedBeforeCleanup);
        CollectionAssert.AreEqual(
            new[] { (new HstsEntry("h", false, null), 0, 2), (new HstsEntry("other.example", true, "20991231 00:00:00"), 1, 2) },
            saved);
    }

    [TestMethod]
    public async Task PerformAsync_HstsReadCallbackFails_AnswersAbortedByCallbackWithoutConnecting()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(HstsOk)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = HstsOptionsOf("http://h/x", CallbackHstsCacheTests.ReadOf([], HstsResult.Fail), write: null);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(string.Empty, easy.ErrorText);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task DisposeAsync_HandleWhoseHstsReadCallbackFailed_HandsTheWriteCallbackNoEntriesOfAnotherHandle()
    {
        ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(HstsOk)]));
        await using ManagedEasyTransfer loader = (ManagedEasyTransfer)engine.CreateEasy();
        loader.Options = HstsOptionsOf("http://h/x", CallbackHstsCacheTests.ReadOf([new HstsEntry("h", false, null)], HstsResult.Done), write: null);
        await loader.PerformAsync(CancellationToken.None);
        List<(HstsEntry Entry, int Index, int Total)> saved = [];
        ManagedEasyTransfer failing = (ManagedEasyTransfer)engine.CreateEasy();
        failing.Options = HstsOptionsOf("http://h/x", CallbackHstsCacheTests.ReadOf([], HstsResult.Fail), RecordingHstsWrite(saved));

        CurlCode code = await failing.PerformAsync(CancellationToken.None);
        await failing.DisposeAsync();

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.IsEmpty(saved);
    }

    [TestMethod]
    public async Task PerformAsync_HstsCallbacksWithHstsDisabled_CallsNeither()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(HstsOk)]);
        List<(HstsEntry Entry, int Index, int Total)> saved = [];
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        EasyOptions options = HstsOptionsOf("http://h/x", CallbackHstsCacheTests.ReadOf([new HstsEntry("h", false, null)], HstsResult.Done), RecordingHstsWrite(saved));
        easy.Options = options with { Http = new EasyHttpOptions() };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        await easy.DisposeAsync();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(80, connector.Targets.Single().Port);
        Assert.IsEmpty(saved);
    }

    [TestMethod]
    public async Task DisposeAsync_HandleThatNeverPerformed_HandsNoEntryAnotherHandleLearnedToTheWriteCallback()
    {
        ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(HstsOk)]));
        await using ManagedEasyTransfer loader = (ManagedEasyTransfer)engine.CreateEasy();
        loader.Options = HstsOptionsOf("http://h/x", CallbackHstsCacheTests.ReadOf([new HstsEntry("h", false, null)], HstsResult.Done), write: null);
        await loader.PerformAsync(CancellationToken.None);
        List<(HstsEntry Entry, int Index, int Total)> saved = [];
        ManagedEasyTransfer saver = (ManagedEasyTransfer)engine.CreateEasy();
        saver.Options = new EasyOptions
        {
            Http = new EasyHttpOptions { HstsControl = HstsControl.Enable },
            Callbacks = new EasyCallbackOptions { HstsWrite = RecordingHstsWrite(saved) },
        };

        await saver.DisposeAsync();

        Assert.IsEmpty(saved);
    }

    [TestMethod]
    public async Task PerformAsync_HstsSwitchedUrlFailsToConnect_EffectiveUrlIsTheHttpsOne()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new RecordingConnector(Protocol.Abstractions.CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = HstsOptionsOf("http://h/1922", CallbackHstsCacheTests.ReadOf([new HstsEntry("h", false, null)], HstsResult.Done), write: null);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual("https://h/1922", easy.Info.EffectiveUrl);
    }

    private static EasyOptions HstsOptionsOf(string url, HstsReadCallback read, HstsWriteCallback? write) =>
        new()
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { HstsControl = HstsControl.Enable },
            Callbacks = new EasyCallbackOptions { HstsRead = read, HstsWrite = write },
        };

    private static HstsWriteCallback RecordingHstsWrite(List<(HstsEntry Entry, int Index, int Total)> saved) =>
        (entry, index, total) =>
        {
            saved.Add((entry, index, total));
            return HstsResult.Ok;
        };
}
