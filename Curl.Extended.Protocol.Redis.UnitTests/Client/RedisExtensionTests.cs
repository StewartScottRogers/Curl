using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class RedisExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedRedisConnector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheRedisSchemes()
    {
        CollectionAssert.AreEqual(new[] { "redis", "rediss" }, RedisExtension.Description.Schemes.ToArray());
        Assert.AreEqual("redis", RedisExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheRedisHandler() =>
        Assert.IsInstanceOfType<RedisProtocolHandler>(RedisExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => RedisExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeRedisAndRediss()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("redis"));
        Assert.IsTrue(dispatcher.Serves("rediss"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsRedisInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("redis", StringComparison.Ordinal)));
        Assert.Contains("Sends one Redis command over RESP3", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("redis")]
    [DataRow("rediss")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
