using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

[TestClass]
public sealed class UrlHandleOverrideTests
{
    [TestMethod]
    public void Apply_HandleSetAfterUrl_ReplacesTheUrlWithTheHandles()
    {
        using ManagedCurlUrlHandle handle = new();
        handle.Set(CurlUrlPart.Url, "http://127.0.0.1:8990/658", CurlUrlFlags.None);
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://www.example.com", UrlHandle = handle } };

        EasyOptions applied = UrlHandleOverride.Apply(options);

        Assert.AreEqual("http://127.0.0.1:8990/658", applied.Network.Url);
    }

    [TestMethod]
    public void Apply_HandleWithNoPath_GivesTheUrlWithASlashPath()
    {
        using ManagedCurlUrlHandle handle = new();
        handle.Set(CurlUrlPart.Host, "www.example.com", CurlUrlFlags.None);
        handle.Set(CurlUrlPart.Scheme, "http", CurlUrlFlags.None);
        handle.Set(CurlUrlPart.Port, "80", CurlUrlFlags.None);
        EasyOptions options = new() { Network = new EasyNetworkOptions { UrlHandle = handle } };

        EasyOptions applied = UrlHandleOverride.Apply(options);

        Assert.AreEqual("http://www.example.com:80/", applied.Network.Url);
    }

    [TestMethod]
    public void Apply_HandleWithNoHost_LeavesNoUrl()
    {
        using ManagedCurlUrlHandle handle = new();
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://www.example.com", UrlHandle = handle } };

        EasyOptions applied = UrlHandleOverride.Apply(options);

        Assert.IsNull(applied.Network.Url);
    }

    [TestMethod]
    public void Apply_NoHandle_LeavesTheOptionsAsTheyAre()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        EasyOptions applied = UrlHandleOverride.Apply(options);

        Assert.AreSame(options, applied);
    }
}
