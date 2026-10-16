using Curl.Api.EngineContracts.Mime;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Mime.Mime;

[TestClass]
public sealed class CurlMimeTests
{
    [TestMethod]
    public async Task MimeInit_ALiveLibrary_WrapsANewEngineMimeBuilder()
    {
        var engine = new FakeCurlEngine();
        await using var library = CurlGlobal.InitCounting(engine, CurlGlobalFlags.Default, new GlobalInitCounter());

        using var mime = library.MimeInit();

        Assert.IsFalse(mime.IsFreed);
        Assert.IsInstanceOfType<IMimeBuilder>(engine.Created.Single());
    }

    [TestMethod]
    public async Task MimeInit_ACleanedUpLibrary_ThrowsObjectDisposed()
    {
        var library = CurlGlobal.InitCounting(new FakeCurlEngine(), CurlGlobalFlags.Default, new GlobalInitCounter());
        await library.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => library.MimeInit());
    }

    [TestMethod]
    public void AddPart_AddsOneEnginePartPerCall()
    {
        var builder = new FakeMimeBuilder();
        using var mime = new CurlMime(builder);

        mime.AddPart();
        mime.AddPart();

        Assert.HasCount(2, builder.Parts);
    }

    [TestMethod]
    public void Build_AsksTheEngineForTheBody()
    {
        var builder = new FakeMimeBuilder();
        using var mime = new CurlMime(builder);
        mime.AddPart().SetName("a");

        var body = mime.Build();

        Assert.AreEqual("a", body.Parts.Single().Name);
        Assert.AreEqual(1, builder.BuildCount);
    }

    [TestMethod]
    public void Dispose_FreesTheHandleAndASecondCallDoesNothing()
    {
        var mime = new CurlMime(new FakeMimeBuilder());

        mime.Dispose();
        mime.Dispose();

        Assert.IsTrue(mime.IsFreed);
    }

    [TestMethod]
    public void AddPartAndBuild_AfterFree_ThrowObjectDisposed()
    {
        var builder = new FakeMimeBuilder();
        var mime = new CurlMime(builder);
        mime.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => mime.AddPart());
        Assert.ThrowsExactly<ObjectDisposedException>(() => mime.Build());
        Assert.IsEmpty(builder.Parts);
        Assert.AreEqual(0, builder.BuildCount);
    }
}
