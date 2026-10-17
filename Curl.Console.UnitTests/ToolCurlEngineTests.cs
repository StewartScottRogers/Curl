using Curl.Api.Codes;
using Curl.Engine;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ToolCurlEngine" /> (ADR-0624): <c>curl_easy_init</c> on its library pairs the new
/// <c>Libcurl</c> handle with the engine transfer under it, and every other engine call passes to the
/// runner's engine.
/// </summary>
[TestClass]
public sealed class ToolCurlEngineTests
{
    [TestMethod]
    public async Task EasyInit_SetOptionOnTheHandle_ReachesTheHooksTransfer()
    {
        ToolCurlEngine engine = new(new ManagedCurlEngine());
        ToolEasyHandle easy = engine.EasyInit();

        CurlCode code = easy.Easy.SetOption(CurlOption.Url, "http://example.com/");

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://example.com/", easy.Hooks.Options.Network.Url);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task EasyInit_TwoHandles_EachPairsWithItsOwnTransfer()
    {
        ToolCurlEngine engine = new(new ManagedCurlEngine());
        ToolEasyHandle first = engine.EasyInit();
        ToolEasyHandle second = engine.EasyInit();

        Assert.AreNotSame(first.Hooks, second.Hooks);
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [TestMethod]
    public async Task MultiInit_AddsAHandleFromEasyInit_AcceptsIt()
    {
        ToolCurlEngine engine = new(new ManagedCurlEngine());
        ToolEasyHandle easy = engine.EasyInit();
        Libcurl.Multi.CurlMulti multi = engine.MultiInit();

        CurlMultiCode code = multi.AddHandle(easy.Easy);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        await multi.DisposeAsync();
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task OtherEngineCalls_PassToTheRunnersEngine()
    {
        ManagedCurlEngine managed = new();
        ToolCurlEngine engine = new(managed);

        Assert.AreEqual(managed.VersionInfo, engine.VersionInfo);
        await using (engine.CreateMulti())
        {
        }

        Assert.IsNotNull(engine.CreateShare());
        engine.CreateUrl().Dispose();
        Assert.IsNotNull(engine.CreateMime());
        Assert.IsNotNull(engine.CreateForm());
        await engine.DisposeAsync();
    }
}
