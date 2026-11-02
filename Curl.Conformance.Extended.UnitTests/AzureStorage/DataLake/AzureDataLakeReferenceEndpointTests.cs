using System.Text;
using Curl.Conformance.Extended.AzureStorage;
using Curl.Conformance.Extended.AzureStorage.DataLake;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.AzureStorage.DataLake;

/// <summary>The reference DFS endpoint's answers that the curl cases do not reach, served from fixed request bytes.</summary>
[TestClass]
public sealed class AzureDataLakeReferenceEndpointTests
{
    private const string Bearer = "Authorization: Bearer t\r\n";

    private static AzureDataLakeReferenceEndpoint Endpoint(int listPageSize = 5000)
    {
        AzureDataLakeReferenceEndpoint endpoint = new(AzureStorageResponseFraming.ContentLength, listPageSize);
        endpoint.CreateFileSystem("fs");
        return endpoint;
    }

    private static async Task<string> ServeAsync(AzureDataLakeReferenceEndpoint endpoint, string requests)
    {
        using MemoryStream input = new(Encoding.Latin1.GetBytes(requests));
        using MemoryStream output = new();
        using DuplexStream stream = new(input, output);
        await endpoint.ServeAsync(new ReferenceConnection(stream, new WireExchange(443, ReferenceTransport.Tls)), CancellationToken.None);
        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static string Request(string method, string target, string body = "") =>
        $"{method} {target} HTTP/1.1\r\n{Bearer}Content-Length: {body.Length}\r\n\r\n{body}";

    [TestMethod]
    public async Task Answer_MissingFileSystemAndUnsupportedVerb_AnswerTheirJsonErrors()
    {
        AzureDataLakeReferenceEndpoint endpoint = Endpoint();

        string response = await ServeAsync(endpoint, Request("GET", "/nofs/a") + Request("DELETE", "/fs/a"));

        StringAssert.Contains(response, "HTTP/1.1 404 The specified filesystem does not exist.\r\n");
        StringAssert.Contains(response, "x-ms-error-code: FilesystemNotFound\r\n");
        StringAssert.Contains(response, "{\"error\":{\"code\":\"UnsupportedHttpVerb\",\"message\":\"The resource doesn't support the specified HTTP verb.\"}}");
    }

    [TestMethod]
    public async Task Update_PathNeverCreated_AnswersPathNotFound()
    {
        AzureDataLakeReferenceEndpoint endpoint = Endpoint();

        string response = await ServeAsync(endpoint, Request("PATCH", "/fs/a?action=append&position=0", "x") + Request("PATCH", "/fs/a?action=flush&position=0") + Request("GET", "/fs"));

        Assert.AreEqual(3, response.Split("HTTP/1.1 404 The specified path does not exist.\r\n").Length - 1);
    }

    [TestMethod]
    public async Task Update_PositionNotTheLengthSoFar_AnswersInvalidFlushPositionAndCommitsNothing()
    {
        AzureDataLakeReferenceEndpoint endpoint = Endpoint();

        string response = await ServeAsync(
            endpoint,
            Request("PUT", "/fs/a?resource=file") + Request("PATCH", "/fs/a?action=append&position=1", "x") + Request("PATCH", "/fs/a?action=append", "x")
            + Request("PATCH", "/fs/a?action=append&position=0", "xy") + Request("PATCH", "/fs/a?action=flush&position=1"));

        Assert.AreEqual(3, response.Split("x-ms-error-code: InvalidFlushPosition\r\n").Length - 1);
        StringAssert.Contains(response, "HTTP/1.1 202 Accepted\r\n");
        CollectionAssert.AreEqual(Array.Empty<byte>(), endpoint.FileContent("fs", "a"));
    }

    [TestMethod]
    public async Task ListPaths_FileSystemRootWithoutDirectory_ListsTopLevelEntriesWithDirectoriesMarked()
    {
        AzureDataLakeReferenceEndpoint endpoint = Endpoint();
        endpoint.StoreFile("fs", "a.txt", [1, 2]);
        endpoint.StoreFile("fs", "d/x", [1]);

        string response = await ServeAsync(endpoint, Request("GET", "/fs?resource=filesystem&recursive=false&directory="));

        StringAssert.Contains(response, "{\"paths\":[{\"name\":\"a.txt\",\"contentLength\":\"2\",\"lastModified\":\"Mon, 02 Nov 2026 12:00:00 GMT\",\"etag\":\"0x");
        StringAssert.Contains(response, "{\"name\":\"d\",\"isDirectory\":\"true\",\"contentLength\":\"0\",");
        Assert.IsFalse(response.Contains("x-ms-continuation", StringComparison.Ordinal));
        Assert.IsNull(endpoint.FileContent("fs", "missing"));
    }

    // Reads the fixed request bytes and keeps what the endpoint writes.
    private sealed class DuplexStream(MemoryStream input, MemoryStream output) : Stream
    {
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

        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
    }
}
