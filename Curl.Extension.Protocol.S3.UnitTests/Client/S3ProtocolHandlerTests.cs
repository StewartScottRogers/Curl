using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.S3.Client;

/// <summary>
/// Drives <see cref="S3ProtocolHandler"/>, and through it <see cref="S3Client"/> and <see cref="S3Operations"/>, over recorded
/// responses, checking the requests against the Amazon S3 API reference's GetObject, PutObject, ListObjectsV2,
/// CreateMultipartUpload, UploadPart, CompleteMultipartUpload and "Error responses" sections.
/// </summary>
[TestClass]
public sealed class S3ProtocolHandlerTests
{
    private const string Minio = "http://minio:9000";

    [TestMethod]
    public async Task ExecuteAsync_Key_GetsTheObjectSignedWithSigV4()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok("hello"));
        MemoryStream output = new();

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/dir/a%20b.txt", output, new NetworkCredential("AKID", "secret")));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(("minio", 9000, false), (connector.Targets[0].Host, connector.Targets[0].Port, connector.Targets[0].UseTls));
        string sent = connector.Connections[0].Written;
        Assert.StartsWith("GET /bucket/dir/a%20b.txt HTTP/1.1\r\nhost: minio:9000\r\nx-amz-date: 20130524T000000Z\r\n", sent);
        Assert.Contains("x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\r\n", sent);
        Assert.Contains("Authorization: AWS4-HMAC-SHA256 Credential=AKID/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;x-amz-content-sha256;x-amz-date,Signature=", sent);
        Assert.DoesNotContain("Content-Length", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_SendsAnUnsignedRequestToTheVirtualHost()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok(string.Empty));

        TransferResult result = await Handler(connector, null).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("GET /k HTTP/1.1\r\nhost: bucket.s3.us-east-1.amazonaws.com\r\n\r\n", connector.Connections[0].Written);
        Assert.IsTrue(connector.Targets[0].UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_SessionToken_IsSentAndSigned()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok(string.Empty));
        Dictionary<string, string> variables = new() { ["AWS_ACCESS_KEY_ID"] = "AKID", ["AWS_SECRET_ACCESS_KEY"] = "s", ["AWS_SESSION_TOKEN"] = "tok", ["AWS_ENDPOINT_URL"] = Minio };

        await new S3ProtocolHandler(connector, variables.GetValueOrDefault, 8).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.Contains("x-amz-security-token: tok\r\n", connector.Connections[0].Written);
        Assert.Contains("SignedHeaders=host;x-amz-content-sha256;x-amz-date;x-amz-security-token,", connector.Connections[0].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorResponse_FailsWithExit22NamingTheS3Code()
    {
        QueuedS3Connector connector = new("HTTP/1.1 404 Not Found\r\nContent-Length: 78\r\n\r\n<Error><Code>NoSuchKey</Code><Message>The key does not exist</Message></Error>");

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("S3 returned error 404: NoSuchKey: The key does not exist", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorResponseWithoutAnErrorBody_NamesOnlyTheStatus()
    {
        QueuedS3Connector connector = new("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual("S3 returned error 403", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionRefused_FailsWithTheConnectorsExitCode()
    {
        TransferResult result = await Handler(new QueuedS3Connector(QueuedS3Connector.Refuse), Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to minio port 9000", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MalformedResponse_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedS3Connector("nonsense\r\n\r\n"), Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_EndpointThatIsNotHttp_FailsWithExit3()
    {
        TransferResult result = await Handler(new QueuedS3Connector(), "ftp://minio").ExecuteAsync(Context("s3://bucket/k", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("The S3 endpoint \"ftp://minio\" is not an http or https URL", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TrailingSlash_ListsEveryPageWithTheDelimiter()
    {
        QueuedS3Connector connector = new(
            QueuedS3Connector.Ok(Listing("<Contents><Key>dir/a</Key><Size>1</Size><LastModified>2009-10-12T17:50:30.000Z</LastModified><ETag>\"e\"</ETag></Contents><CommonPrefixes><Prefix>dir/sub/</Prefix></CommonPrefixes>", "true", "<NextContinuationToken>t/1=</NextContinuationToken>")),
            QueuedS3Connector.Ok(Listing("<Contents><Key>dir/b</Key><Size>2</Size><LastModified>2009-10-12T17:50:30.000Z</LastModified><ETag>\"f\"</ETag></Contents>", "false", string.Empty)));
        MemoryStream output = new();

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/dir/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("dir/sub/\ndir/a\ndir/b\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.StartsWith("GET /bucket?list-type=2&delimiter=%2F&prefix=dir%2F HTTP/1.1\r\n", connector.Connections[0].Written);
        Assert.StartsWith("GET /bucket?list-type=2&delimiter=%2F&prefix=dir%2F&continuation-token=t%2F1%3D HTTP/1.1\r\n", connector.Connections[1].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingThatIsNotAListBucketResult_FailsWithExit8()
    {
        TransferResult result = await Handler(new QueuedS3Connector(QueuedS3Connector.Ok("<Other/>")), Minio).ExecuteAsync(Context("s3://bucket", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.StartsWith("S3 sent a body curl cannot read: ", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadShorterThanAPart_PutsItInOneRequest()
    {
        QueuedS3Connector connector = new(QueuedS3Connector.Ok(string.Empty, "ETag: \"x\"\r\n"));

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream(), new NetworkCredential("AKID", "s"), "abc"));

        Assert.AreEqual(3, result.BytesTransferred);
        string sent = connector.Connections[0].Written;
        Assert.StartsWith("PUT /bucket/k HTTP/1.1\r\n", sent);
        Assert.Contains("x-amz-content-sha256: ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\r\n", sent);
        Assert.EndsWith("Content-Length: 3\r\n\r\nabc", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOfSeveralParts_SendsAMultipartUpload()
    {
        QueuedS3Connector connector = new(
            QueuedS3Connector.Ok("<InitiateMultipartUploadResult><Bucket>bucket</Bucket><Key>k</Key><UploadId>u1</UploadId></InitiateMultipartUploadResult>"),
            QueuedS3Connector.Ok(string.Empty, "ETag: \"p1\"\r\n"),
            QueuedS3Connector.Ok(string.Empty, "ETag: \"p2\"\r\n"),
            QueuedS3Connector.Ok("<CompleteMultipartUploadResult><Key>k</Key></CompleteMultipartUploadResult>"));

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream(), null, "0123456789"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10, result.BytesTransferred);
        Assert.StartsWith("POST /bucket/k?uploads= HTTP/1.1\r\nhost: minio:9000\r\nContent-Length: 0\r\n", connector.Connections[0].Written);
        Assert.StartsWith("PUT /bucket/k?partNumber=1&uploadId=u1 HTTP/1.1\r\n", connector.Connections[1].Written);
        Assert.EndsWith("Content-Length: 8\r\n\r\n01234567", connector.Connections[1].Written);
        Assert.EndsWith("Content-Length: 2\r\n\r\n89", connector.Connections[2].Written);
        Assert.StartsWith("POST /bucket/k?uploadId=u1 HTTP/1.1\r\n", connector.Connections[3].Written);
        Assert.Contains("<PartNumber>2</PartNumber><ETag>\"p2\"</ETag>", connector.Connections[3].Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_CompletionAnsweredWithAnErrorBody_FailsWithExit22()
    {
        QueuedS3Connector connector = new(
            QueuedS3Connector.Ok("<InitiateMultipartUploadResult><Bucket>b</Bucket><Key>k</Key><UploadId>u1</UploadId></InitiateMultipartUploadResult>"),
            QueuedS3Connector.Ok(string.Empty, "ETag: \"p1\"\r\n"),
            QueuedS3Connector.Ok("<Error><Code>InternalError</Code><Message>retry</Message></Error>"));

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream(), null, "01234567"));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("S3 returned error 200: InternalError: retry", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_PartWithoutAnETag_FailsWithExit8()
    {
        QueuedS3Connector connector = new(
            QueuedS3Connector.Ok("<InitiateMultipartUploadResult><Bucket>b</Bucket><Key>k</Key><UploadId>u1</UploadId></InitiateMultipartUploadResult>"),
            QueuedS3Connector.Ok(string.Empty));

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/k", new MemoryStream(), null, "01234567"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("S3 sent no ETag for an uploaded part", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithoutAKey_FailsWithExit3()
    {
        QueuedS3Connector connector = new();

        TransferResult result = await Handler(connector, Minio).ExecuteAsync(Context("s3://bucket/dir/", new MemoryStream(), null, "abc"));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new QueuedS3Connector(), null).ExecuteAsync(null!));

    private static S3ProtocolHandler Handler(QueuedS3Connector connector, string? endpointUrl) =>
        new(connector, name => name == "AWS_ENDPOINT_URL" ? endpointUrl : null, 8);

    private static TransferContext Context(string url, Stream output, NetworkCredential? credentials = null, string? upload = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Credentials = credentials,
        Upload = upload is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(upload)),
        TimeProvider = new FixedTimeProvider(),
    };

    private static string Listing(string entries, string truncated, string token) =>
        $"<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\"><Name>bucket</Name>{entries}<IsTruncated>{truncated}</IsTruncated>{token}</ListBucketResult>";

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);
    }
}
