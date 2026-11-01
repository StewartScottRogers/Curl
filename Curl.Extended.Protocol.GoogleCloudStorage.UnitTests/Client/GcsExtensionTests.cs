using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.GoogleCloudStorage.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class GcsExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new QueuedGcsConnector(), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheGsScheme()
    {
        CollectionAssert.AreEqual(new[] { "gs" }, GcsExtension.Description.Schemes.ToArray());
        Assert.AreEqual("gs", GcsExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheGcsHandler() =>
        Assert.IsInstanceOfType<GcsProtocolHandler>(GcsExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => GcsExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeGs()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("gs"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsGsInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("Google Cloud Storage", StringComparison.Ordinal)));
        Assert.Contains("Reads and writes Google Cloud Storage objects", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("gs://bucket/object"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"gs\" not supported", result.ErrorMessage);
    }
}
