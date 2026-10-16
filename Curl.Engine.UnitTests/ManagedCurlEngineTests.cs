using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedCurlEngineTests
{
    [TestMethod]
    public async Task CreateEasy_ProductionEngine_GivesAHandleWithDefaultOptions()
    {
        await using ManagedCurlEngine engine = new();

        await using IEasyTransfer easy = engine.CreateEasy();

        Assert.AreEqual(new EasyOptions(), easy.Options);
        Assert.IsInstanceOfType<ManagedEasyTransfer>(easy);
    }

    [TestMethod]
    public async Task PerformAsync_ProductionDispatcherAndUnknownScheme_ReturnsUnsupportedProtocolWithoutConnecting()
    {
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "bogus://h/" },
            Transfer = new() { Timeout = TimeSpan.FromSeconds(5) },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, code);
    }

    [TestMethod]
    public async Task CreateDispatcher_NoTimeout_BuildsTheProductionHandlers()
    {
        await using ManagedCurlEngine engine = new();

        Core.ProtocolDispatcher dispatcher = ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null);

        Assert.IsNotNull(dispatcher);
    }

    [TestMethod]
    public async Task Shared_ProductionEngine_HasNoEasyErrorText()
    {
        await using ManagedCurlEngine engine = new();

        Assert.AreEqual(string.Empty, engine.Shared.Performer.KnownHostsFileMissingFailure.ErrorMessage);
    }

    [TestMethod]
    public async Task Shared_EasyErrorTextGiven_ReachesThePerformer()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, code => $"text {(int)code}");

        Assert.AreEqual("text 2", engine.Shared.Performer.KnownHostsFileMissingFailure.ErrorMessage);
    }

    [TestMethod]
    public async Task VersionInfo_ProductionEngine_ListsHttpAndHttps()
    {
        await using ManagedCurlEngine engine = new();

        CollectionAssert.Contains(engine.VersionInfo.Protocols.ToArray(), "http");
        CollectionAssert.Contains(engine.VersionInfo.Protocols.ToArray(), "https");
    }

    [TestMethod]
    public async Task TwoEngines_InOneProcess_ShareNoHandleOrVersionInfo()
    {
        await using ManagedCurlEngine first = new();
        await using ManagedCurlEngine second = new();

        Assert.AreNotSame(first.VersionInfo, second.VersionInfo);
        Assert.AreNotSame(first.CreateEasy(), second.CreateEasy());
        Assert.AreNotSame(first.Shared.Cookies, second.Shared.Cookies);
        Assert.AreNotSame(first.Shared.Connections, second.Shared.Connections);
    }

    [TestMethod]
    public async Task MultiShareUrlMimeAndForm_NotYetBuilt_ThrowNotSupported()
    {
        await using ManagedCurlEngine engine = new();

        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateMulti());
        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateShare());
        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateUrl());
        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateMime());
        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateForm());
    }
}
