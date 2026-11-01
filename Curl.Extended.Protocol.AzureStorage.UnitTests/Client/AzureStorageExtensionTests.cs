using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

[TestClass]
public sealed class AzureStorageExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new QueuedAzureStorageConnector(), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheAzureStorageSchemes()
    {
        CollectionAssert.AreEqual(new[] { "az", "abfs", "abfss" }, AzureStorageExtension.Description.Schemes.ToArray());
        Assert.AreEqual("azure-storage", AzureStorageExtension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheAzureStorageHandler() =>
        Assert.IsInstanceOfType<AzureStorageProtocolHandler>(AzureStorageExtension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageExtension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeAzAndAbfs()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("az"));
        Assert.IsTrue(dispatcher.Serves("abfs"));
        Assert.IsTrue(dispatcher.Serves("abfss"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsAzureStorageInBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("azure-storage", StringComparison.Ordinal)));
        Assert.Contains("Reads and writes Azure Blob Storage", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    [DataRow("az://account/container/blob", "az")]
    [DataRow("abfs://fs@account/file", "abfs")]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme(string url, string scheme)
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" not supported", result.ErrorMessage);
    }
}
