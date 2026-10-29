using Curl.Core;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.S3.Client;

/// <summary>Checks the extension's description, its handler factory and its registration in <c>Curl.Extension.Composition</c>.</summary>
[TestClass]
public sealed class S3ExtensionTests
{
    private static readonly ProtocolHandlerServices Services = new(new QueuedS3Connector(), null!, null!, null!);

    [TestMethod]
    public void Description_NamesTheS3Scheme()
    {
        CollectionAssert.AreEqual(new[] { "s3" }, S3Extension.Description.Schemes.ToArray());
        Assert.AreEqual("s3", S3Extension.Description.Name);
    }

    [TestMethod]
    public void CreateHandler_GivesTheS3Handler() =>
        Assert.IsInstanceOfType<S3ProtocolHandler>(S3Extension.CreateHandler(Services));

    [TestMethod]
    public void CreateHandler_WithoutServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => S3Extension.CreateHandler(null!));

    [TestMethod]
    public void ExtensionProtocolHandlers_WithExtensionsOn_ServeS3()
    {
        ProtocolDispatcher dispatcher = new(ExtensionProtocolHandlers.For(_ => null)!(Services));

        Assert.IsTrue(dispatcher.Serves("s3"));
    }

    [TestMethod]
    public void ExtensionListing_WithExtensionsOn_ListsS3InBothForms()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("s3", StringComparison.Ordinal)));
        Assert.Contains("Reads and writes Amazon S3 objects", ExtensionListing.Markdown(_ => null));
    }

    [TestMethod]
    public async Task Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme()
    {
        Assert.IsNull(ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null));
        ProtocolDispatcher dispatcher = new([]);

        TransferResult result = await dispatcher.DispatchAsync(new TransferContext { Url = CurlUrl.Parse("s3://bucket/key"), Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("Protocol \"s3\" not supported", result.ErrorMessage);
    }
}
