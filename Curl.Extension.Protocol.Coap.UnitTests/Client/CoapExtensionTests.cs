using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class CoapExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(null!, new ScriptedDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "x")), null!, null!);

    [TestMethod]
    public void Description_NamesTheCoapAndCoapsSchemes()
    {
        CollectionAssert.AreEqual(new[] { "coap", "coaps" }, CoapExtension.Description.Schemes.ToArray());
        Assert.AreEqual("coap", CoapExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheCoapHandler() =>
        Assert.IsInstanceOfType<CoapProtocolHandler>(CoapExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeCoap() =>
        Assert.IsTrue(new ProtocolDispatcher(ExtensionProtocolHandlers.For(_ => null)!(Services)).Serves("coap"));

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsCoapInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("coap", StringComparison.Ordinal)));
        Assert.Contains("Makes CoAP requests over UDP", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("coap")]
    [DataRow("coaps")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
