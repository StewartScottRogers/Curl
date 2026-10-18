using Curl.Api.EngineContracts;

namespace Curl.Api.Client;

[TestClass]
public sealed class CurlClientTests
{
    [TestMethod]
    public void EngineOnlyConstructor_OwnsEngineAndUsesDefaultOptions()
    {
        CurlClient client = new(new FakeCurlEngine());

        Assert.IsTrue(client.OwnsEngine);
        Assert.AreEqual(new CurlClientOptions(), client.Options);
    }

    [TestMethod]
    public void FullConstructor_KeepsOptionsAndOwnership()
    {
        CurlClientOptions options = new() { UserAgent = "agent/1" };

        CurlClient client = new(new FakeCurlEngine(), options, ownsEngine: false);

        Assert.IsFalse(client.OwnsEngine);
        Assert.AreSame(options, client.Options);
    }

    [TestMethod]
    public void Constructor_NullEngine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlClient(null!));
    }

    [TestMethod]
    public void Constructor_NullOptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlClient(new FakeCurlEngine(), null!, ownsEngine: true));
    }

    [TestMethod]
    public void DefaultOptions_StartAtLibcurlDefaults()
    {
        CurlClientOptions options = new();

        Assert.IsNull(options.UserAgent);
        Assert.AreEqual(TimeSpan.Zero, options.Timeout);
        Assert.AreEqual(TimeSpan.Zero, options.ConnectTimeout);
    }

    [TestMethod]
    public async Task CreateEasy_CreatesANewHandleOnTheEngineEachCall()
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine);

        IEasyTransfer first = client.CreateEasy();
        IEasyTransfer second = client.CreateEasy();

        Assert.HasCount(2, engine.CreatedEasyHandles);
        Assert.AreSame(engine.CreatedEasyHandles[0], first);
        Assert.AreSame(engine.CreatedEasyHandles[1], second);
    }

    [TestMethod]
    public async Task CreateEasy_AppliesClientOptionsToTheHandle()
    {
        CurlClientOptions options = new()
        {
            UserAgent = "agent/1",
            Timeout = TimeSpan.FromSeconds(30),
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };
        await using CurlClient client = new(new FakeCurlEngine(), options, ownsEngine: true);

        IEasyTransfer easy = client.CreateEasy();

        Assert.AreEqual("agent/1", easy.Options.Http.UserAgent);
        Assert.AreEqual(TimeSpan.FromSeconds(30), easy.Options.Transfer.Timeout);
        Assert.AreEqual(TimeSpan.FromSeconds(5), easy.Options.Network.ConnectTimeout);
    }

    [TestMethod]
    public async Task CreateEasy_WithDefaultOptions_LeavesHandleAtLibcurlDefaults()
    {
        await using CurlClient client = new(new FakeCurlEngine());

        IEasyTransfer easy = client.CreateEasy();

        Assert.AreEqual(new Options.EasyOptions(), easy.Options);
    }

    [TestMethod]
    public async Task DisposeAsync_OwningClient_DisposesEngine()
    {
        FakeCurlEngine engine = new();
        CurlClient client = new(engine);

        await client.DisposeAsync();

        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_NotOwningClient_LeavesEngineUndisposed()
    {
        FakeCurlEngine engine = new();
        CurlClient client = new(engine, new CurlClientOptions(), ownsEngine: false);

        await client.DisposeAsync();

        Assert.AreEqual(0, engine.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_DisposesEngineOnce()
    {
        FakeCurlEngine engine = new();
        CurlClient client = new(engine);

        await client.DisposeAsync();
        await client.DisposeAsync();

        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task CreateEasy_AfterDispose_ThrowsObjectDisposedException()
    {
        FakeCurlEngine engine = new();
        CurlClient client = new(engine);
        await client.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => client.CreateEasy());
        Assert.IsEmpty(engine.CreatedEasyHandles);
    }

    [TestMethod]
    public async Task CreateEasy_AfterDisposeOfNotOwningClient_ThrowsObjectDisposedException()
    {
        FakeCurlEngine engine = new();
        CurlClient client = new(engine, new CurlClientOptions(), ownsEngine: false);
        await client.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => client.CreateEasy());
    }
}
