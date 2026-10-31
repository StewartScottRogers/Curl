using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp1.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class Amqp1ExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedAmqp1Connector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheAmqp1Schemes()
    {
        CollectionAssert.AreEqual(new[] { "amqp1", "amqp1s" }, Amqp1Extension.Description.Schemes.ToArray());
        Assert.AreEqual("amqp1", Amqp1Extension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheAmqp1Handler() =>
        Assert.IsInstanceOfType<Amqp1ProtocolHandler>(Amqp1Extension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Amqp1Extension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeAmqp1AndAmqp1s()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("amqp1"));
        Assert.IsTrue(dispatcher.Serves("amqp1s"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsAmqp1InBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("amqp1", StringComparison.Ordinal)));
        Assert.Contains("Sends or receives one message over AMQP 1.0", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("amqp1")]
    [DataRow("amqp1s")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
