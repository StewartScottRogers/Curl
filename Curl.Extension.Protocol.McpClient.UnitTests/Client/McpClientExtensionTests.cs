using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.McpClient.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class McpClientExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new ScriptedMcpConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "x")), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheThreeSchemes()
    {
        CollectionAssert.AreEqual(new[] { "mcp", "mcps", "mcp+stdio" }, McpClientExtension.Description.Schemes.ToArray());
        Assert.AreEqual("mcp", McpClientExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheMcpHandler() =>
        Assert.IsInstanceOfType<McpProtocolHandler>(McpClientExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => McpClientExtension.CreateHandler(null!));

    [TestMethod]
    [DataRow("mcp")]
    [DataRow("mcps")]
    [DataRow("mcp+stdio")]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeTheScheme(string scheme) =>
        Assert.IsTrue(new ProtocolDispatcher(ExtensionProtocolHandlers.For(_ => null)!(Services)).Serves(scheme));

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsMcpInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("mcp+stdio", StringComparison.Ordinal)));
        Assert.Contains("Lists and calls a Model Context Protocol server's tools", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("mcp://example.com/mcp"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"mcp\" not supported", result.ErrorMessage);
    }
}
