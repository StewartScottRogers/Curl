using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.GoogleCloudStorage.Client;

/// <summary>
/// Drives <see cref="GcsProtocolHandler"/>, and through it <see cref="GcsClient"/> and <see cref="GcsOperations"/>, over recorded
/// responses, checking the requests against the Cloud Storage XML API's "GET Object", "PUT Object", "List objects",
/// "Authentication" (OAuth 2.0 bearer and the V4 signing process with an HMAC key) and "XML API error responses" sections.
/// </summary>
[TestClass]
public sealed class GcsProtocolHandlerTests
{
    private const string Emulator = "http://emulator:4443";
    private const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [TestMethod]
    public async Task ExecuteAsync_ObjectWithBearerToken_GetsItWithTheAccessToken()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok("hello"));
        MemoryStream output = new();

        TransferResult result = await Handler(connector, null).ExecuteAsync(Context("gs://bucket/dir/a%20b.txt", output, bearerToken: "ya29.token"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(("storage.googleapis.com", 443, true), (connector.Targets[0].Host, connector.Targets[0].Port, connector.Targets[0].UseTls));
        Assert.AreEqual("GET /bucket/dir/a%20b.txt HTTP/1.1\r\nhost: storage.googleapis.com\r\nAuthorization: Bearer ya29.token\r\n\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ObjectWithHmacKey_SignsWithGoog4HmacSha256()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(string.Empty));

        await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream(), new NetworkCredential("GOOGID", "secret")));

        string sent = connector.Connections[0].Written;
        Assert.StartsWith("GET /bucket/k HTTP/1.1\r\nhost: emulator:4443\r\nx-goog-date: 20200101T000000Z\r\n", sent);
        Assert.Contains($"x-goog-content-sha256: {EmptyPayloadHash}\r\n", sent);
        Assert.Contains("Authorization: GOOG4-HMAC-SHA256 Credential=GOOGID/20200101/auto/storage/goog4_request, SignedHeaders=host;x-goog-content-sha256;x-goog-date, Signature=", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerTokenAndHmacKey_SendsOnlyTheBearerToken()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(string.Empty));

        await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream(), new NetworkCredential("GOOGID", "secret"), bearerToken: "tok"));

        Assert.AreEqual("GET /bucket/k HTTP/1.1\r\nhost: emulator:4443\r\nAuthorization: Bearer tok\r\n\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(" ")]
    public async Task ExecuteAsync_NoCredentials_SendsAnUnauthenticatedRequest(string? bearerToken)
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(string.Empty));

        TransferResult result = await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream(), bearerToken: bearerToken, withHttp: bearerToken is not null));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("GET /bucket/k HTTP/1.1\r\nhost: emulator:4443\r\n\r\n", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorResponse_FailsWithExit22NamingTheCode()
    {
        const string Body = "<Error><Code>NoSuchKey</Code><Message>The specified key does not exist.</Message></Error>";
        QueuedGcsConnector connector = new($"HTTP/1.1 404 Not Found\r\nContent-Length: {Body.Length}\r\n\r\n{Body}");

        TransferResult result = await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Cloud Storage returned error 404: NoSuchKey: The specified key does not exist.", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorResponseWithoutAnErrorBody_NamesOnlyTheStatus()
    {
        QueuedGcsConnector connector = new("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream()));

        Assert.AreEqual("Cloud Storage returned error 403", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionRefused_KeepsTheConnectorsFailure()
    {
        TransferResult result = await Handler(new QueuedGcsConnector(QueuedGcsConnector.Refuse), Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to emulator port 4443", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnreadableReply_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedGcsConnector("nonsense\r\n\r\n"), Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmulatorIsNotHttp_FailsWithExit3()
    {
        TransferResult result = await Handler(new QueuedGcsConnector(), "ftp://emulator").ExecuteAsync(Context("gs://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("The Cloud Storage endpoint \"ftp://emulator\" is not an http or https URL", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Prefix_ListsEveryPageByContinuationToken()
    {
        QueuedGcsConnector connector = new(
            QueuedGcsConnector.Ok(Listing("<CommonPrefixes><Prefix>dir/sub/</Prefix></CommonPrefixes>", "true", "<NextContinuationToken>t/1</NextContinuationToken>")),
            QueuedGcsConnector.Ok(Listing(Entry("dir/a.txt"), "false", string.Empty)));
        MemoryStream output = new();

        TransferResult result = await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/dir/", output));

        Assert.AreEqual("dir/sub/\ndir/a.txt\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(19, result.BytesTransferred);
        Assert.StartsWith("GET /bucket?list-type=2&delimiter=%2F&prefix=dir%2F HTTP/1.1\r\n", connector.Connections[0].Written);
        Assert.StartsWith("GET /bucket?list-type=2&delimiter=%2F&prefix=dir%2F&continuation-token=t%2F1 HTTP/1.1\r\n", connector.Connections[1].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_TruncatedListingWithoutAToken_StopsAfterThePage()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(Listing(Entry("a"), "true", string.Empty)));
        MemoryStream output = new();

        await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket", output));

        Assert.AreEqual("a\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.HasCount(1, connector.Connections);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingBodyNotAListBucketResult_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedGcsConnector(QueuedGcsConnector.Ok("<Other/>")), Emulator).ExecuteAsync(Context("gs://bucket", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.StartsWith("Cloud Storage sent a body curl cannot read: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_PutsTheObjectInOneSignedRequest()
    {
        QueuedGcsConnector connector = new(QueuedGcsConnector.Ok(string.Empty));

        TransferResult result = await Handler(connector, Emulator).ExecuteAsync(Context("gs://bucket/k", new MemoryStream(), new NetworkCredential("GOOGID", "s"), upload: "abc"));

        Assert.AreEqual(3, result.BytesTransferred);
        string sent = connector.Connections[0].Written;
        Assert.StartsWith("PUT /bucket/k HTTP/1.1\r\n", sent);
        Assert.Contains("x-goog-content-sha256: ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\r\n", sent);
        Assert.EndsWith("Content-Length: 3\r\n\r\nabc", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToAPrefix_FailsWithExit3()
    {
        TransferResult result = await Handler(new QueuedGcsConnector(), Emulator).ExecuteAsync(Context("gs://bucket/dir/", new MemoryStream(), upload: "abc"));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("A Cloud Storage upload needs an object name in the URL", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new QueuedGcsConnector(), null).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_IsGs() =>
        CollectionAssert.AreEqual(new[] { "gs" }, Handler(new QueuedGcsConnector(), null).SupportedSchemes.ToArray());

    private static GcsProtocolHandler Handler(QueuedGcsConnector connector, string? emulatorHost) =>
        new(connector, name => name == "STORAGE_EMULATOR_HOST" ? emulatorHost : null);

    private static TransferContext Context(string url, Stream output, NetworkCredential? credentials = null, string? bearerToken = null, string? upload = null, bool? withHttp = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Credentials = credentials,
        Http = withHttp ?? bearerToken is not null ? new Curl.Protocol.Abstractions.HttpRequestOptions { BearerToken = bearerToken } : null,
        Upload = upload is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(upload)),
        TimeProvider = new FixedTimeProvider(),
    };

    private static string Entry(string key) =>
        $"<Contents><Key>{key}</Key><LastModified>2020-01-01T00:00:00.000Z</LastModified><ETag>\"e\"</ETag><Size>1</Size></Contents>";

    private static string Listing(string entries, string truncated, string token) =>
        $"<ListBucketResult xmlns=\"http://doc.s3.amazonaws.com/2006-03-01\"><Name>bucket</Name><Prefix></Prefix>{entries}<IsTruncated>{truncated}</IsTruncated>{token}</ListBucketResult>";

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
