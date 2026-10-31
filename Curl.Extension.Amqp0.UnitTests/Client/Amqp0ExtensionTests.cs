using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp0.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class Amqp0ExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedAmqp0Connector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheAmqpSchemes()
    {
        CollectionAssert.AreEqual(new[] { "amqp", "amqps" }, Amqp0Extension.Description.Schemes.ToArray());
        Assert.AreEqual("amqp0", Amqp0Extension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheAmqp0Handler() =>
        Assert.IsInstanceOfType<Amqp0ProtocolHandler>(Amqp0Extension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp0Extension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeAmqpAndAmqps()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("amqp"));
        Assert.IsTrue(dispatcher.Serves("amqps"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsAmqp0InBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("amqp0", StringComparison.Ordinal)));
        Assert.Contains("Publishes or gets one message over AMQP 0-9-1", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("amqp")]
    [DataRow("amqps")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com//q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
