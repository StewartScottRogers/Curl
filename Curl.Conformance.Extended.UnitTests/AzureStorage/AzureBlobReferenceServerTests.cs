using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage;

/// <summary>The reference Blob endpoint's operations and framing that the curl cases do not reach, served from fixed request bytes.</summary>
[TestClass]
public sealed class AzureBlobReferenceServerTests
{
    private const string Bearer = "Authorization: Bearer t\r\n";

    private static AzureBlobReferenceEndpoint Endpoint(AzureStorageResponseFraming framing = AzureStorageResponseFraming.ContentLength, int listPageSize = 5000)
    {
        AzureBlobReferenceEndpoint endpoint = new(framing, listPageSize);
        endpoint.CreateContainer("c");
        return endpoint;
    }

    private static async Task<string> ServeAsync(AzureBlobReferenceEndpoint endpoint, string requests)
    {
        using RequestStream stream = new(Encoding.Latin1.GetBytes(requests));
        await endpoint.ServeAsync(new ReferenceConnection(stream, new WireExchange(443, ReferenceTransport.Tls)), CancellationToken.None);
        return Encoding.Latin1.GetString(stream.Written.ToArray());
    }

    private static string Put(string target, string body, string headers = Bearer) =>
        $"PUT {target} HTTP/1.1\r\n{headers}Content-Length: {body.Length}\r\n\r\n{body}";

    [TestMethod]
    public async Task ListBlobs_PageSizeTwoWithDelimiter_GivesPrefixesAndANextMarker()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint(listPageSize: 2);
        foreach (string name in new[] { "a.txt", "d/x", "d/y", "e.txt" })
        {
            endpoint.StoreBlob("c", name, [1]);
        }

        string response = await ServeAsync(endpoint, $"GET /c?restype=container&comp=list&delimiter=%2F&flag HTTP/1.1\r\n{Bearer}\r\nGET /c?restype=container&comp=list&delimiter=%2F&marker=e.txt HTTP/1.1\r\n{Bearer}\r\n");

        StringAssert.Contains(response, "<Blobs><Blob><Name>a.txt</Name><Properties><Content-Length>1</Content-Length><BlobType>BlockBlob</BlobType></Properties></Blob><BlobPrefix><Name>d/</Name></BlobPrefix></Blobs><NextMarker>e.txt</NextMarker>");
        StringAssert.Contains(response, "<Marker>e.txt</Marker><Delimiter>/</Delimiter><Blobs><Blob><Name>e.txt</Name>");
        StringAssert.Contains(response, "<NextMarker /></EnumerationResults>");
        Assert.AreEqual(string.Empty, endpoint.Requests[0].QueryValue("flag"));
    }

    [TestMethod]
    public async Task ListBlobs_NoDelimiterWithPrefix_ListsEveryNameUnderThePrefix()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint();
        endpoint.StoreBlob("c", "d/x", [1]);
        endpoint.StoreBlob("c", "e", [1]);

        string response = await ServeAsync(endpoint, $"GET /c?comp=list&prefix=d%2F HTTP/1.1\r\n{Bearer}\r\n");

        StringAssert.Contains(response, "<Blobs><Blob><Name>d/x</Name>");
        Assert.IsFalse(response.Contains("<Name>e</Name>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PutBlockList_StagedBlocks_CommitsThemInTheListsOrder()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.Chunked);

        string response = await ServeAsync(
            endpoint,
            Put("/c/b?comp=block&blockid=QQ%3D%3D", "one") + Put("/c/b?comp=block&blockid=Qg%3D%3D", "two")
            + Put("/c/b?comp=blocklist", "<BlockList><Latest>Qg==</Latest><Latest>QQ==</Latest></BlockList>"));

        StringAssert.Contains(response, "HTTP/1.1 201 Created\r\nx-ms-version: 2025-01-05\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");
        CollectionAssert.AreEqual(new[] { "Qg==", "QQ==" }, endpoint.CommittedBlockIds("c", "b")!.ToArray());
        Assert.AreEqual("twoone", Encoding.ASCII.GetString(endpoint.BlobContent("c", "b")!));
        Assert.IsNull(endpoint.CommittedBlockIds("c", "other"));
    }

    [TestMethod]
    public async Task PutBlockList_BlockNeverStaged_FailsWithInvalidBlockList()
    {
        string response = await ServeAsync(Endpoint(), Put("/c/b?comp=blocklist", "<BlockList><Latest>QQ==</Latest></BlockList>"));

        StringAssert.StartsWith(response, "HTTP/1.1 400 The specified block list is invalid.\r\n");
        StringAssert.Contains(response, "<Error><Code>InvalidBlockList</Code>");
    }

    [TestMethod]
    public async Task PutBlob_NoBlobType_FailsWithMissingRequiredHeader()
    {
        string response = await ServeAsync(Endpoint(), Put("/c/b", "x"));

        StringAssert.Contains(response, "x-ms-error-code: MissingRequiredHeader\r\n");
    }

    [TestMethod]
    public async Task Answer_UnsupportedVerbMissingContainerAndPrivateAnonymous_AnswerTheirErrors()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint();

        string response = await ServeAsync(
            endpoint,
            $"DELETE /c/b HTTP/1.1\r\n{Bearer}\r\nGET /none/b HTTP/1.1\r\n\r\nGET /c/b HTTP/1.1\r\n\r\nGET /c/b?sig=s HTTP/1.1\r\n\r\n");

        StringAssert.Contains(response, "x-ms-error-code: UnsupportedHttpVerb\r\n");
        StringAssert.Contains(response, "x-ms-error-code: ContainerNotFound\r\n");
        StringAssert.Contains(response, "x-ms-error-code: ResourceNotFound\r\n");
        StringAssert.Contains(response, "x-ms-error-code: BlobNotFound\r\n");
        Assert.HasCount(4, endpoint.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_ConnectionClosedInsideABody_ReadsNoRequest()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint();

        Assert.AreEqual(string.Empty, await ServeAsync(endpoint, "PUT /c/b HTTP/1.1\r\nContent-Length: 5\r\n\r\nab"));
        Assert.AreEqual(0, endpoint.Requests.Count);
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint();
        using RequestStream stream = new([]);
        ReferenceConnection connection = new(stream, new WireExchange(443, ReferenceTransport.Tls));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => endpoint.ServeAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => AzureStorageHttpRequestReader.ReadAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await AzureStorageHttpResponseWriter.WriteAsync(null!, AzureStorageHttpResponse.Success(200, "OK", []), AzureStorageResponseFraming.Close, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await AzureStorageHttpResponseWriter.WriteAsync(connection, null!, AzureStorageResponseFraming.Close, CancellationToken.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => ProbeEndpoint.Probe(null!));
    }

    [TestMethod]
    public async Task ServeAsync_CloseFraming_AnswersOneRequestWithoutALengthAndStops()
    {
        AzureBlobReferenceEndpoint endpoint = Endpoint(AzureStorageResponseFraming.Close);
        endpoint.StoreBlob("c", "b", "xy"u8.ToArray());

        string response = await ServeAsync(endpoint, $"GET /c/b HTTP/1.1\r\n{Bearer}\r\nGET /c/b HTTP/1.1\r\n{Bearer}\r\n");

        Assert.AreEqual("HTTP/1.1 200 OK\r\nx-ms-version: 2025-01-05\r\nContent-Type: application/octet-stream\r\nx-ms-blob-type: BlockBlob\r\nConnection: close\r\n\r\nxy", response);
        Assert.HasCount(1, endpoint.Requests);
    }

    // Reaches the protected member a derived endpoint uses.
    private sealed class ProbeEndpoint() : AzureStorageReferenceEndpoint(AzureStorageResponseFraming.Close)
    {
        public static bool Probe(AzureStorageHttpRequest request) => IsAuthorized(request);

        protected override AzureStorageHttpResponse Answer(AzureStorageHttpRequest request) => AzureStorageHttpResponse.Success(200, "OK", []);
    }

    // Reads the fixed request bytes and keeps what the endpoint writes.
    private sealed class RequestStream(byte[] requests) : Stream
    {
        private readonly MemoryStream input = new(requests);

        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            input.Dispose();
            Written.Dispose();
            base.Dispose(disposing);
        }
    }
}
