using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Nats;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class NatsExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedConnector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesBothSchemesAndTheSpecification()
    {
        CollectionAssert.AreEqual(new[] { "nats", "natss" }, NatsExtension.Description.Schemes.ToArray());
        Assert.AreEqual("https://docs.nats.io/reference/reference-protocols/nats-protocol", NatsExtension.Description.Specification.ToString());
    }

    [TestMethod]
    public void CreateHandler_GivesTheNatsHandler() =>
        Assert.IsInstanceOfType<NatsProtocolHandler>(NatsExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => NatsExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeNats()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("nats"));
        Assert.IsTrue(dispatcher.Serves("natss"));
    }

    [TestMethod]
    [DataRow("nats://example.com/a", "nats")]
    [DataRow("natss://example.com/a", "natss")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string url, string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
