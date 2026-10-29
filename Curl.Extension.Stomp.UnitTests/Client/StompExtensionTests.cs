using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Stomp.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class StompExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedStompConnector(ConnectResult.Refused("x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheStompSchemes()
    {
        CollectionAssert.AreEqual(new[] { "stomp", "stomps" }, StompExtension.Description.Schemes.ToArray());
        Assert.AreEqual("stomp", StompExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheStompHandler() =>
        Assert.IsInstanceOfType<StompProtocolHandler>(StompExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => StompExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeStompAndStomps()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("stomp"));
        Assert.IsTrue(dispatcher.Serves("stomps"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsStompInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("stomp", StringComparison.Ordinal)));
        Assert.Contains("Sends and subscribes with STOMP 1.2", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("stomp")]
    [DataRow("stomps")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
