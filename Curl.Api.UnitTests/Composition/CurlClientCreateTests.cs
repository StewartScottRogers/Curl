using Curl.Api.EngineContracts;
using Curl.Engine;

namespace Curl.Api.Composition;

// The real managed engine, used for version info and handle creation only: nothing is sent.
[TestClass]
public sealed class CurlClientCreateTests
{
    [TestMethod]
    public async Task Create_BuildsTheManagedEngine_OwnsIt_AndUsesDefaultOptions()
    {
        CurlClient client = CurlClient.Create();

        Assert.IsInstanceOfType<ManagedCurlEngine>(client.Engine);
        Assert.IsTrue(client.OwnsEngine);
        Assert.AreEqual(new CurlClientOptions(), client.Options);
        CollectionAssert.Contains(client.Engine.VersionInfo.Protocols.ToList(), "http");
        Assert.IsFalse(string.IsNullOrEmpty(client.Engine.VersionInfo.Host));

        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task Create_WithOptions_AppliesThemToEasyHandlesOfTheManagedEngine()
    {
        CurlClientOptions options = new() { UserAgent = "agent/1" };
        CurlClient client = CurlClient.Create(options);

        IEasyTransfer easy = client.CreateEasy();

        Assert.AreSame(options, client.Options);
        Assert.AreEqual("agent/1", easy.Options.Http.UserAgent);
        await easy.DisposeAsync();
        await client.DisposeAsync();
    }

    [TestMethod]
    public void Create_NullOptions_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlClient.Create(null!));
    }

    [TestMethod]
    public async Task Create_DisposeAsync_DisposesTheEngineItBuilt()
    {
        CurlClient client = CurlClient.Create();

        await client.DisposeAsync();

        Assert.IsTrue(client.OwnsEngine);
        Assert.ThrowsExactly<ObjectDisposedException>(() => client.CreateEasy());
    }
}
