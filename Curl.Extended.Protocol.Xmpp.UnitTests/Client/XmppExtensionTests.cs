using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class XmppExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedXmppConnector(ConnectResult.Refused("x")), null!, new ScriptedTlsProvider(ConnectResult.Refused("x")), null!);

    [TestMethod]
    public void Description_NamesTheXmppSchemes()
    {
        CollectionAssert.AreEqual(new[] { "xmpp", "xmpps" }, XmppExtension.Description.Schemes.ToArray());
        Assert.AreEqual("xmpp", XmppExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheXmppHandler() =>
        Assert.IsInstanceOfType<XmppProtocolHandler>(XmppExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => XmppExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeXmppAndXmpps()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("xmpp"));
        Assert.IsTrue(dispatcher.Serves("xmpps"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsXmppInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("xmpp", StringComparison.Ordinal)));
        Assert.Contains("Sends XMPP messages and service discovery queries", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse($"{scheme}://example.com/q"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
