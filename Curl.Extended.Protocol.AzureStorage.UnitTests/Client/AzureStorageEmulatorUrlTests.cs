using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>Checks that a path-style emulator URL (an IP address or localhost) takes the account from the first path segment (BL-5407).</summary>
[TestClass]
public sealed class AzureStorageEmulatorUrlTests
{
    private const string EmptyListing = "<?xml version=\"1.0\"?><EnumerationResults><Blobs><Blob><Name>a.txt</Name><Properties><Last-Modified>Wed, 09 Sep 2009 09:20:02 GMT</Last-Modified><Etag>0x1</Etag><Content-Length>1</Content-Length></Properties></Blob></Blobs></EnumerationResults>";

    [TestMethod]
    [DataRow("az://127.0.0.1:49020/devstoreaccount1/ctr/", "127.0.0.1", 49020, "GET /devstoreaccount1/ctr?restype=container&comp=list&delimiter=%2F&prefix=")]
    [DataRow("az://localhost:10000/devstoreaccount1/ctr/dir/", "localhost", 10000, "GET /devstoreaccount1/ctr?restype=container&comp=list&delimiter=%2F&prefix=dir%2F")]
    [DataRow("az://acct/ctr/", "acct.blob.core.windows.net", 443, "GET /ctr?restype=container&comp=list&delimiter=%2F&prefix=")]
    [DataRow("az://acct.blob.core.windows.net/ctr/", "acct.blob.core.windows.net", 443, "GET /ctr?restype=container&comp=list&delimiter=%2F&prefix=")]
    [DataRow("az://127.0.0.1:49020/devstoreaccount1/ctr/a.txt", "127.0.0.1", 49020, "GET /devstoreaccount1/ctr/a.txt HTTP/1.1")]
    public async Task ExecuteAsync_Url_SendsTheRequestForItsStyle(string url, string host, int port, string requestStart)
    {
        bool listing = url.EndsWith('/');
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply(listing ? EmptyListing : "data"));
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream() };

        TransferResult result = await new AzureStorageProtocolHandler(connector, _ => null, 8, 8).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((host, port), (connector.Targets[0].Host, connector.Targets[0].Port));
        StringAssert.StartsWith(connector.Connections[0].Written, requestStart);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmulatorUploadUrl_KeepsTheAccountInThePath()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply(string.Empty, status: "201 Created"));
        TransferContext context = new() { Url = CurlUrl.Parse("az://127.0.0.1:49020/devstoreaccount1/ctr/a.txt"), Output = new MemoryStream(), Upload = new MemoryStream([1, 2]) };

        TransferResult result = await new AzureStorageProtocolHandler(connector, _ => null, 8, 8).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connector.Connections[0].Written, "PUT /devstoreaccount1/ctr/a.txt HTTP/1.1");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmulatorUrlWithNoContainer_IsMalformed()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("x"));
        TransferContext context = new() { Url = CurlUrl.Parse("az://127.0.0.1:49020/devstoreaccount1"), Output = new MemoryStream() };

        TransferResult result = await new AzureStorageProtocolHandler(connector, _ => null, 8, 8).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
    }
}
