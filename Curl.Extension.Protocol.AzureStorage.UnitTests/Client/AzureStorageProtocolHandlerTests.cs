using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.AzureStorage.Client;

/// <summary>
/// Pins the requests <see cref="AzureStorageProtocolHandler"/> sends against the Blob service REST API (Get Blob, Put Blob,
/// Put Block, Put Block List, List Blobs) and the Data Lake Storage Gen2 REST API (Path Read, Path Create, Path Update,
/// Path List), with recorded responses and no network.
/// </summary>
[TestClass]
public sealed class AzureStorageProtocolHandlerTests
{
    private const string Sas = "sv=2025-01-05&sig=abc";

    [TestMethod]
    public async Task ExecuteAsync_GetBlobWithSas_SendsTheSasInTheQueryOverHttps()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("hello"));
        MemoryStream output = new();

        TransferResult result = await Handler(connector, sas: Sas).ExecuteAsync(Context("az://account/container/dir/a%20b.txt?snapshot=1", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(output.ToArray()));
        Assert.StartsWith("GET /container/dir/a%20b.txt?snapshot=1&sv=2025-01-05&sig=abc HTTP/1.1\r\n", connector.Connections[0].Written);
        Assert.Contains("x-ms-version: 2025-01-05\r\n", connector.Connections[0].Written);
        Assert.AreEqual("account.blob.core.windows.net", connector.Targets[0].Host);
        Assert.AreEqual(443, connector.Targets[0].Port);
        Assert.IsTrue(connector.Targets[0].UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithOAuth2Bearer_SendsTheEntraIdTokenAsBearer()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("x"));
        TransferContext context = Context("az://account/container/blob", new MemoryStream(), bearer: "token");

        await Handler(connector, sas: Sas).ExecuteAsync(context);

        Assert.Contains("Authorization: Bearer token\r\n", connector.Connections[0].Written);
        Assert.DoesNotContain("sig=", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListBlobs_FollowsNextMarkerAndWritesPrefixesThenBlobs()
    {
        QueuedAzureStorageConnector connector = new(
            QueuedAzureStorageConnector.Reply(Listing("<BlobPrefix><Name>dir/sub/</Name></BlobPrefix>" + Blob("dir/a"), "<NextMarker>m 1</NextMarker>")),
            QueuedAzureStorageConnector.Reply(Listing(Blob("dir/b"), "<NextMarker/>")));
        MemoryStream output = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context("az://account/container/dir/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("dir/sub/\ndir/a\ndir/b\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.StartsWith("GET /container?restype=container&comp=list&delimiter=%2F&prefix=dir%2F HTTP/1.1", connector.Connections[0].Written);
        Assert.StartsWith("GET /container?restype=container&comp=list&delimiter=%2F&prefix=dir%2F&marker=m%201 HTTP/1.1", connector.Connections[1].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListBlobsOfABadBody_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector(QueuedAzureStorageConnector.Reply("<Other/>"))).ExecuteAsync(Context("az://account/container", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.StartsWith("Azure Storage sent a body curl cannot read: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SmallUpload_SendsOnePutBlobOfABlockBlob()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply(string.Empty, status: "201 Created"));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("az://account/container/blob", new MemoryStream(), upload: "1234567"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(7, result.BytesTransferred);
        string written = connector.Connections[0].Written;
        Assert.StartsWith("PUT /container/blob HTTP/1.1\r\n", written);
        Assert.Contains("x-ms-blob-type: BlockBlob\r\n", written);
        Assert.EndsWith("\r\n\r\n1234567", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOfABlockOrMore_SendsPutBlockPerBlockThenPutBlockList()
    {
        string created = QueuedAzureStorageConnector.Reply(string.Empty, status: "201 Created");
        QueuedAzureStorageConnector connector = new(created, created, created);

        TransferResult result = await Handler(connector).ExecuteAsync(Context("az://account/container/blob", new MemoryStream(), upload: "123456789A"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.StartsWith("PUT /container/blob?comp=block&blockid=MDAwMDAw HTTP/1.1", connector.Connections[0].Written);
        Assert.EndsWith("12345678", connector.Connections[0].Written);
        Assert.StartsWith("PUT /container/blob?comp=block&blockid=MDAwMDAx HTTP/1.1", connector.Connections[1].Written);
        Assert.StartsWith("PUT /container/blob?comp=blocklist HTTP/1.1", connector.Connections[2].Written);
        Assert.Contains("<Latest>MDAwMDAw</Latest><Latest>MDAwMDAx</Latest>", connector.Connections[2].Written);
    }

    [TestMethod]
    [DataRow("az://account/", null, "An az:// URL needs a container after the account")]
    [DataRow("az://account/container/", "x", "An Azure Storage upload needs a blob name in the URL")]
    [DataRow("abfs://fs@account/dir/", "x", "An Azure Storage upload needs a file name in the URL")]
    public async Task ExecuteAsync_WithoutAName_FailsWithExit3(string url, string? upload, string message)
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector()).ExecuteAsync(Context(url, new MemoryStream(), upload: upload));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutAccount_FailsWithExit3()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector()).ExecuteAsync(Context("abfs://account/dir", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SasTokenWithoutSig_FailsWithExit67()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector(), sas: "sv=1").ExecuteAsync(Context("az://account/c/b", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorWithAnErrorBody_FailsWithExit22NamingTheCode()
    {
        string body = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Error><Code>BlobNotFound</Code><Message>The specified blob does not exist.</Message></Error>";

        TransferResult result = await Handler(new QueuedAzureStorageConnector(QueuedAzureStorageConnector.Reply(body, status: "404 Not Found"))).ExecuteAsync(Context("az://account/c/b", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Azure Storage returned error 404: BlobNotFound: The specified blob does not exist.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorWithoutAnErrorBody_FailsWithExit22NamingTheStatus()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector(QueuedAzureStorageConnector.Reply("{}", status: "403 Forbidden"))).ExecuteAsync(Context("abfss://fs@account/f", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Azure Storage returned error 403", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionRefused_FailsWithTheConnectorsExitCode()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector(QueuedAzureStorageConnector.Refuse)).ExecuteAsync(Context("az://account/c/b", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedResponse_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedAzureStorageConnector("nonsense\r\n\r\n")).ExecuteAsync(Context("az://account/c/b", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathRead_GetsTheFileFromTheDfsEndpoint()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("data"));
        MemoryStream output = new();

        await Handler(connector).ExecuteAsync(Context("abfss://fs@account/dir/file.txt", output));

        Assert.AreEqual("data", Encoding.UTF8.GetString(output.ToArray()));
        Assert.StartsWith("GET /fs/dir/file.txt HTTP/1.1", connector.Connections[0].Written);
        Assert.AreEqual("account.dfs.core.windows.net", connector.Targets[0].Host);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathList_FollowsTheContinuationHeaderAndMarksDirectories()
    {
        QueuedAzureStorageConnector connector = new(
            QueuedAzureStorageConnector.Reply("""{"paths":[{"name":"dir/sub","isDirectory":"true"}]}""", "x-ms-continuation: c 1\r\n"),
            QueuedAzureStorageConnector.Reply("""{"paths":[{"name":"dir/a.txt","contentLength":"3"}]}""", "x-ms-continuation: \r\n"));
        MemoryStream output = new();

        TransferResult result = await Handler(connector).ExecuteAsync(Context("abfs://fs@account/dir/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("dir/sub/\ndir/a.txt\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.StartsWith("GET /fs?resource=filesystem&recursive=false&directory=dir HTTP/1.1", connector.Connections[0].Written);
        Assert.StartsWith("GET /fs?resource=filesystem&recursive=false&directory=dir&continuation=c%201 HTTP/1.1", connector.Connections[1].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathListOfTheFileSystem_NamesNoDirectory()
    {
        QueuedAzureStorageConnector connector = new(QueuedAzureStorageConnector.Reply("""{"paths":[]}"""));

        await Handler(connector).ExecuteAsync(Context("abfs://fs@account.dfs.core.windows.net", new MemoryStream()));

        Assert.StartsWith("GET /fs?resource=filesystem&recursive=false HTTP/1.1", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_DfsUpload_CreatesAppendsPerBlockAndFlushes()
    {
        string ok = QueuedAzureStorageConnector.Reply(string.Empty, status: "202 Accepted");
        QueuedAzureStorageConnector connector = new(ok, ok, ok, ok);

        TransferResult result = await Handler(connector).ExecuteAsync(Context("abfs://fs@account/f.txt", new MemoryStream(), upload: "123456789A"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.StartsWith("PUT /fs/f.txt?resource=file HTTP/1.1", connector.Connections[0].Written);
        Assert.StartsWith("PATCH /fs/f.txt?action=append&position=0 HTTP/1.1", connector.Connections[1].Written);
        Assert.StartsWith("PATCH /fs/f.txt?action=append&position=8 HTTP/1.1", connector.Connections[2].Written);
        Assert.StartsWith("PATCH /fs/f.txt?action=flush&position=10 HTTP/1.1", connector.Connections[3].Written);
    }

    [TestMethod]
    public void SupportedSchemes_AreAzAbfsAndAbfss() =>
        CollectionAssert.AreEqual(new[] { "az", "abfs", "abfss" }, Handler(new QueuedAzureStorageConnector()).SupportedSchemes.ToArray());

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new QueuedAzureStorageConnector()).ExecuteAsync(null!));

    private static AzureStorageProtocolHandler Handler(QueuedAzureStorageConnector connector, string? sas = null) =>
        new(connector, name => name == "AZURE_STORAGE_SAS_TOKEN" ? sas : null, 8);

    private static TransferContext Context(string url, Stream output, string? upload = null, string? bearer = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Upload = upload is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(upload)),
        TimeProvider = new FixedTimeProvider(),
        Http = bearer is null ? null : new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = bearer },
    };

    private static string Blob(string name) =>
        $"<Blob><Name>{name}</Name><Properties><Last-Modified>Wed, 09 Sep 2009 09:20:02 GMT</Last-Modified><Etag>0x1</Etag><Content-Length>1</Content-Length></Properties></Blob>";

    private static string Listing(string entries, string marker) =>
        $"<EnumerationResults ContainerName=\"container\"><Blobs>{entries}</Blobs>{marker}</EnumerationResults>";

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 1, 5, 0, 0, 0, TimeSpan.Zero);
    }
}
