using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.OciRegistry;

namespace Curl.Conformance.Extended.UnitTests.OciRegistry;

/// <summary>The reference registry's answers that no curl case reaches, so a fault in the server shows here rather than as a client failure.</summary>
[TestClass]
public sealed class OciReferenceRegistryTests
{
    private static OciReferenceRequest Request(string method, string target, string body = "") =>
        OciReferenceRequest.ParseHead($"{method} {target} HTTP/1.1\r\nHost: registry.example") with { Body = Encoding.UTF8.GetBytes(body) };

    private static string Code(OciReferenceResponse response) => Encoding.UTF8.GetString(response.Body).Split('"')[5];

    [TestMethod]
    [DataRow("/v2/")]
    [DataRow("/other/lib/app/manifests/1.0")]
    [DataRow("/v2/lib/app/tags/list")]
    public void Answer_PathOutsideTheApi_IsNameInvalid(string target)
    {
        OciReferenceResponse response = new OciReferenceRegistry().Answer(Request("GET", target));

        Assert.AreEqual(400, response.StatusCode);
        Assert.AreEqual("NAME_INVALID", Code(response));
    }

    [TestMethod]
    public void Answer_DeleteOfAManifest_IsUnsupported()
    {
        OciReferenceResponse response = new OciReferenceRegistry().Answer(Request("DELETE", "/v2/lib/app/manifests/1.0"));

        Assert.AreEqual(405, response.StatusCode);
        Assert.AreEqual("UNSUPPORTED", Code(response));
    }

    [TestMethod]
    public void Answer_UnknownManifest_IsManifestUnknown()
    {
        OciReferenceResponse response = new OciReferenceRegistry().Answer(Request("GET", "/v2/lib/app/manifests/none"));

        Assert.AreEqual(404, response.StatusCode);
        Assert.AreEqual("MANIFEST_UNKNOWN", Code(response));
    }

    [TestMethod]
    public void Answer_UploadWhoseDigestDoesNotMatch_IsDigestInvalidAndStoresNothing()
    {
        OciReferenceRegistry registry = new();
        string digest = OciReferenceRegistry.DigestOf("other"u8.ToArray());

        OciReferenceResponse response = registry.Answer(Request("PUT", $"/v2/lib/app/blobs/uploads/session-1?digest={Uri.EscapeDataString(digest)}", "layer"));

        Assert.AreEqual("DIGEST_INVALID", Code(response));
        Assert.IsNull(registry.GetBlob(digest));
    }

    [TestMethod]
    public void Answer_UploadWithAnEmptyDigest_IsDigestInvalid() =>
        Assert.AreEqual("DIGEST_INVALID", Code(new OciReferenceRegistry().Answer(Request("PUT", "/v2/lib/app/blobs/uploads/session-1?flag=1&digest"))));

    [TestMethod]
    [DataRow("DELETE", "/v2/lib/app/blobs/sha256:00")]
    [DataRow("PATCH", "/v2/lib/app/blobs/uploads/session-1")]
    public void Answer_MethodTheRegistryDoesNotServe_IsUnsupported(string method, string target) =>
        Assert.AreEqual("UNSUPPORTED", Code(new OciReferenceRegistry().Answer(Request(method, target))));

    [TestMethod]
    public void Answer_ManifestPutWithoutAContentType_StoresIt()
    {
        OciReferenceRegistry registry = new();

        OciReferenceResponse response = registry.Answer(Request("PUT", "/v2/lib/app/manifests/1.0", "{}"));

        Assert.AreEqual(201, response.StatusCode);
        CollectionAssert.AreEqual("{}"u8.ToArray(), registry.GetManifest("lib/app", "1.0"));
    }

    [TestMethod]
    public async Task ServeAsync_BodyInALaterReadThenAHeadCutShort_ServesTheWholeRequestAndStops()
    {
        OciReferenceRegistry registry = new();
        byte[] head = "PUT /v2/lib/app/manifests/1.0 HTTP/1.1\r\nContent-Length: 2\r\n\r\n"u8.ToArray();
        WireExchange exchange = new(443, ReferenceTransport.Tcp);

        await registry.ServeAsync(new ReferenceConnection(new ChunkedReadStream(head, "{}"u8.ToArray(), "GET /v2/"u8.ToArray()), exchange), CancellationToken.None);

        Assert.HasCount(1, registry.Requests);
        CollectionAssert.AreEqual("{}"u8.ToArray(), registry.GetManifest("lib/app", "1.0"));
    }

    [TestMethod]
    public async Task ServeAsync_BodyCutShort_KeepsWhatWasSent()
    {
        OciReferenceRegistry registry = new();
        byte[] request = "PUT /v2/lib/app/manifests/1.0 HTTP/1.1\r\nContent-Length: 9\r\n\r\n{}"u8.ToArray();

        await registry.ServeAsync(new ReferenceConnection(new ChunkedReadStream(request), new WireExchange(443, ReferenceTransport.Tcp)), CancellationToken.None);

        CollectionAssert.AreEqual("{}"u8.ToArray(), registry.Requests.Single().Body);
    }

    /// <summary>A stream that gives one chunk per read, then the end of the stream, and discards what is written.</summary>
    private sealed class ChunkedReadStream(params byte[][] chunks) : Stream
    {
        private int next;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] chunk = next < chunks.Length ? chunks[next++] : [];
            chunk.CopyTo(buffer);
            return ValueTask.FromResult(chunk.Length);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }

    [TestMethod]
    public void Answer_UnknownBlobInStorage_IsBlobUnknown() =>
        Assert.AreEqual("BLOB_UNKNOWN", Code(new OciReferenceRegistry().Answer(Request("GET", "/blobstore/sha256:00"))));

    [TestMethod]
    public void GetManifest_NoneStored_IsNull() =>
        Assert.IsNull(new OciReferenceRegistry().GetManifest("lib/app", "1.0"));

    [TestMethod]
    public void ToBytes_WithoutBody_SendsTheLengthButNoBody() =>
        Assert.AreEqual(
            "HTTP/1.1 200 Status\r\nContent-Length: 3\r\n\r\n",
            Encoding.Latin1.GetString(OciReferenceResponse.Of(200, "abc"u8.ToArray()).ToBytes(withBody: false)));
}
