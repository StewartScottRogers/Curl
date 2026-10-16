using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Url;
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
    public async Task CreateMulti_ProductionEngine_GivesANewManagedMultiHandleEachCall()
    {
        await using ManagedCurlEngine engine = new();

        await using IMultiTransfer first = engine.CreateMulti();
        await using IMultiTransfer second = engine.CreateMulti();

        Assert.IsInstanceOfType<ManagedMultiTransfer>(first);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public async Task CreateUrl_ProductionEngine_GivesAnEmptyManagedUrlHandle()
    {
        await using ManagedCurlEngine engine = new();

        using ICurlUrlHandle url = engine.CreateUrl();

        Assert.IsInstanceOfType<ManagedCurlUrlHandle>(url);
        Assert.AreEqual(CurlUrlCode.NoHost, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public async Task CreateShare_Called_GivesAManagedShareHandle()
    {
        await using ManagedCurlEngine engine = new();

        Assert.IsInstanceOfType<ManagedShareHandle>(engine.CreateShare());
    }

    [TestMethod]
    public async Task CreateMime_Called_GivesAManagedMimeBuilderWithNoParts()
    {
        await using ManagedCurlEngine engine = new();

        IMimeBuilder mime = engine.CreateMime();

        Assert.IsInstanceOfType<ManagedMimeBuilder>(mime);
        Assert.IsEmpty(mime.Build().Parts);
    }

    [TestMethod]
    public async Task CreateForm_NotYetBuilt_ThrowsNotSupported()
    {
        await using ManagedCurlEngine engine = new();

        Assert.ThrowsExactly<NotSupportedException>(() => engine.CreateForm());
    }
}
