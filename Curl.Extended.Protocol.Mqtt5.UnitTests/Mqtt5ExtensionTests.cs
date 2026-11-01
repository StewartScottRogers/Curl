using Curl.Core;
using Curl.Extension.Composition;
using Curl.Extended.Protocol.Mqtt5.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class Mqtt5ExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedMqtt5Connector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheMqtt5Schemes()
    {
        CollectionAssert.AreEqual(new[] { "mqtt5", "mqtt5s", "mqtt5+ws" }, Mqtt5Extension.Description.Schemes.ToArray());
        Assert.AreEqual("mqtt5", Mqtt5Extension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheSchemeHandlerServingAllThreeSchemes()
    {
        IProtocolHandler handler = Mqtt5Extension.CreateHandler(Services);

        Assert.IsInstanceOfType<Mqtt5SchemeHandler>(handler);
        CollectionAssert.AreEqual(new[] { "mqtt5", "mqtt5s", "mqtt5+ws" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Mqtt5Extension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeEveryMqtt5SchemeAndLeaveMqttToCurl()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("mqtt5"));
        Assert.IsTrue(dispatcher.Serves("mqtt5s"));
        Assert.IsTrue(dispatcher.Serves("mqtt5+ws"));
        Assert.IsFalse(dispatcher.Serves("mqtt"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsMqtt5InBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("mqtt5+ws", StringComparison.Ordinal)));
        Assert.Contains("Publishes and subscribes with MQTT 5", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("mqtt5")]
    [DataRow("mqtt5s")]
    [DataRow("mqtt5+ws")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/t"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
