using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OciRegistry.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class OciRegistryExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new SequenceConnector(), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheOciScheme()
    {
        CollectionAssert.AreEqual(new[] { "oci" }, OciRegistryExtension.Description.Schemes.ToArray());
        Assert.AreEqual("oci", OciRegistryExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheOciHandler() =>
        Assert.IsInstanceOfType<OciRegistryProtocolHandler>(OciRegistryExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => OciRegistryExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeOci()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("oci"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsOciInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("oci", StringComparison.Ordinal)));
        Assert.Contains("Pulls from an OCI or Docker registry", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("oci://example.com/alpine"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"oci\" not supported", result.ErrorMessage);
    }
}
