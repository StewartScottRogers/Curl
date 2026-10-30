using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.PrivacyPass.Http;

/// <summary>Pins the issuer exchange over recorded bytes against RFC 9112, the HTTP/1.1 the issuance protocol (RFC 9578 §5, §6) runs over.</summary>
[TestClass]
public sealed class PrivacyPassHttpClientTests
{
    private static readonly Uri ManifestUrl = new("https://issuer.example.com/v2/library/alpine/manifests/latest");

    private static readonly PrivacyPassHttpRequest GetManifest = new(
        "GET", ManifestUrl, [new("Accept", "application/vnd.oci.image.manifest.v1+json")], null);

    private static Task<PrivacyPassHttpResult> SendAsync(RecordedConnection connection, PrivacyPassHttpRequest? request = null) =>
        new PrivacyPassHttpClient(new RecordedConnector(connection)).SendAsync(request ?? GetManifest, CancellationToken.None);

    private static string BodyText(PrivacyPassHttpResult result) => Encoding.UTF8.GetString(result.Response!.Body);

    [TestMethod]
    public async Task SendAsync_GetManifest_SendsTheRequestLineHostAndFieldsAndClosesTheConnection()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}");
        var connector = new RecordedConnector(connection);

        await new PrivacyPassHttpClient(connector).SendAsync(GetManifest, CancellationToken.None);

        Assert.AreEqual(
            "GET /v2/library/alpine/manifests/latest HTTP/1.1\r\nHost: issuer.example.com\r\n"
            + "Accept: application/vnd.oci.image.manifest.v1+json\r\nConnection: close\r\n\r\n",
            connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreEqual(new ConnectTarget("issuer.example.com", 443, true), connector.Target);
    }

    [TestMethod]
    public async Task SendAsync_PutWithBodyOnIPv6WithPort_SendsContentLengthBodyAndABracketedHost()
    {
        var connection = new RecordedConnection("HTTP/1.1 201 Created\r\nLocation: /v2/a/blobs/sha256:x\r\nContent-Length: 0\r\n\r\n");
        var connector = new RecordedConnector(connection);
        var request = new PrivacyPassHttpRequest(
            "PUT", new Uri("http://[::1]:5000/v2/a/blobs/uploads/1?digest=sha256:x"), [new("Content-Type", "application/octet-stream")], "abc"u8.ToArray());

        var result = await new PrivacyPassHttpClient(connector).SendAsync(request, CancellationToken.None);

        Assert.AreEqual(
            "PUT /v2/a/blobs/uploads/1?digest=sha256:x HTTP/1.1\r\nHost: [::1]:5000\r\nContent-Type: application/octet-stream\r\n"
            + "Content-Length: 3\r\nConnection: close\r\n\r\nabc",
            connection.Written);
        Assert.AreEqual(new ConnectTarget("::1", 5000, false), connector.Target);
        Assert.AreEqual(201, result.Response?.StatusCode);
        Assert.AreEqual("/v2/a/blobs/sha256:x", result.Response?.FindHeader("location"));
    }

    [TestMethod]
    public async Task SendAsync_PostWithEmptyBody_SendsContentLengthZero()
    {
        var connection = new RecordedConnection("HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\n\r\n");

        await SendAsync(connection, new PrivacyPassHttpRequest("POST", new Uri("https://r.example/v2/a/blobs/uploads/"), [], []));

        Assert.AreEqual("POST /v2/a/blobs/uploads/ HTTP/1.1\r\nHost: r.example\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", connection.Written);
    }

    [TestMethod]
    public async Task SendAsync_InternationalHost_ConnectsAndSendsHostInPunycode()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        var connector = new RecordedConnector(connection);

        await new PrivacyPassHttpClient(connector).SendAsync(new PrivacyPassHttpRequest("GET", new Uri("https://bücher.example/v2/"), [], null), CancellationToken.None);

        StringAssert.Contains(connection.Written, "\r\nHost: xn--bcher-kva.example\r\n");
        Assert.AreEqual("xn--bcher-kva.example", connector.Target?.Host);
    }

    [TestMethod]
    public async Task SendAsync_HeadWithContentLength_ReadsNoBody()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nDocker-Content-Digest: sha256:x\r\nContent-Length: 1234\r\n\r\n");

        var result = await SendAsync(connection, new PrivacyPassHttpRequest("HEAD", ManifestUrl, [], null));

        Assert.AreEqual(200, result.Response?.StatusCode);
        Assert.AreEqual("1234", result.Response?.FindHeader("Content-Length"));
        Assert.IsEmpty(result.Response!.Body);
    }

    [TestMethod]
    public async Task SendAsync_ContentLengthBody_ReturnsTheStatusHeadersAndBody()
    {
        const string Json = """{"schemaVersion":2}""";
        var connection = new RecordedConnection($"HTTP/1.1 200 OK\r\nContent-Type: application/vnd.oci.image.manifest.v1+json\r\nDocker-Content-Digest:  sha256:x \r\nContent-Length: {Json.Length}\r\n\r\n{Json}", readSize: 7);

        var result = await SendAsync(connection);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Failure);
        Assert.IsNull(result.Message);
        Assert.AreEqual(200, result.Response!.StatusCode);
        Assert.AreEqual("OK", result.Response.ReasonPhrase);
        Assert.AreEqual("sha256:x", result.Response.FindHeader("docker-content-digest"));
        Assert.IsNull(result.Response.FindHeader("Pragma"));
        Assert.AreEqual(Json, BodyText(result));
    }

    [TestMethod]
    public async Task SendAsync_ChunkedBodyWithExtensionAndTrailer_ReturnsTheJoinedChunks()
    {
        var connection = new RecordedConnection(
            "HTTP/1.1 401 Unauthorized\r\nTransfer-Encoding: chunked\r\n\r\n5;name=value\r\n{\"err\r\nA \r\nors\":[12]}\r\n0\r\nExpires: 0\r\n\r\n", readSize: 3);

        var result = await SendAsync(connection);

        Assert.AreEqual(401, result.Response?.StatusCode);
        Assert.AreEqual("{\"errors\":[12]}", BodyText(result));
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\n\r\n{}", DisplayName = "No framing: the close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n{}", DisplayName = "Not chunked last: the close")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 103 Early Hints\r\nLink: </s>\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}", DisplayName = "Interim 1xx skipped")]
    public async Task SendAsync_FramedResponse_ReturnsTheBody(string response)
    {
        var result = await SendAsync(new RecordedConnection(response));

        Assert.AreEqual(200, result.Response?.StatusCode);
        Assert.AreEqual("{}", BodyText(result));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 204\r\nContent-Length: 5\r\n\r\n", 204)]
    [DataRow("HTTP/1.1 304 Not Modified\r\nTransfer-Encoding: chunked\r\n\r\n", 304)]
    public async Task SendAsync_StatusWithoutBody_ReturnsAnEmptyBody(string response, int statusCode)
    {
        var result = await SendAsync(new RecordedConnection(response));

        Assert.AreEqual(statusCode, result.Response?.StatusCode);
        Assert.IsEmpty(result.Response!.Body);
    }

    [TestMethod]
    [DataRow("", PrivacyPassHttpFailure.NoResponse, CurlExitCode.GotNothing)]
    [DataRow("HTTP/1.1 200", PrivacyPassHttpFailure.NoResponse, CurlExitCode.GotNothing)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n", PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n{}", PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\n{}", PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n", PrivacyPassHttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\n{}x\r\n0\r\n\r\n", PrivacyPassHttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", PrivacyPassHttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFF\r\n", PrivacyPassHttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n7FFFFFFF\r\n", PrivacyPassHttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 1073741825\r\n\r\n", PrivacyPassHttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 1, 1\r\n\r\n", PrivacyPassHttpFailure.InvalidContentLength, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nX: a\r\n folded\r\n\r\n", PrivacyPassHttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nNo colon\r\n\r\n", PrivacyPassHttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\n: empty name\r\n\r\n", PrivacyPassHttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nBad name: x\r\n\r\n", PrivacyPassHttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/2 200 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("ICY 200 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.x 200 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1-200 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 2x0 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n", PrivacyPassHttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    public async Task SendAsync_BrokenResponse_FailsWithItsCause(string response, PrivacyPassHttpFailure failure, CurlExitCode exitCode)
    {
        var connection = new RecordedConnection(response);

        var result = await SendAsync(connection);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Response);
        Assert.AreEqual(failure, result.Failure);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_HeadLineOver64KiB_FailsAsLineTooLong()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nX: " + new string('a', PrivacyPassHttpConnectionReader.MaxLineLength) + "\r\n\r\n");

        var result = await SendAsync(connection);

        Assert.AreEqual(PrivacyPassHttpFailure.LineTooLong, result.Failure);
    }

    [TestMethod]
    public async Task SendAsync_ConnectionResetWhileReading_FailsAsARecvError()
    {
        var connection = new RecordedConnection((string?)null);

        var result = await SendAsync(connection);

        Assert.AreEqual(PrivacyPassHttpFailure.ConnectionFailed, result.Failure);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_ConnectorFails_ReturnsTheConnectorsExitCode()
    {
        var connector = new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Connection refused"));

        var result = await new PrivacyPassHttpClient(connector).SendAsync(GetManifest, CancellationToken.None);

        Assert.AreEqual(PrivacyPassHttpFailure.ConnectFailed, result.Failure);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to issuer.example.com: Connection refused", result.Message);
    }

    [TestMethod]
    [DataRow("ftp://issuer.example.com/v2/")]
    [DataRow("v2/")]
    public async Task SendAsync_UrlNotHttp_FailsWithoutConnecting(string url)
    {
        var connector = new RecordedConnector(new RecordedConnection(string.Empty));

        var result = await new PrivacyPassHttpClient(connector).SendAsync(
            new PrivacyPassHttpRequest("GET", new Uri(url, UriKind.RelativeOrAbsolute), [], null), CancellationToken.None);

        Assert.AreEqual(PrivacyPassHttpFailure.UnsupportedEndpoint, result.Failure);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    [DataRow("GET", "Authorization", "Bearer a\r\nX-Injected: 1", DisplayName = "CR LF in a value")]
    [DataRow("GET", "Authorization", "Bearer a\nb", DisplayName = "LF in a value")]
    [DataRow("GET", "Bad Name", "x", DisplayName = "Space in a name")]
    [DataRow("GET", "X:Y", "x", DisplayName = "Colon in a name")]
    [DataRow("GET", "", "x", DisplayName = "Empty name")]
    [DataRow("GET /x", "Accept", "x", DisplayName = "Space in the method")]
    public async Task SendAsync_FieldThatWouldBreakTheHead_FailsWithoutWriting(string method, string name, string value)
    {
        var connection = new RecordedConnection(string.Empty);

        var result = await SendAsync(connection, new PrivacyPassHttpRequest(method, ManifestUrl, [new(name, value)], null));

        Assert.AreEqual(PrivacyPassHttpFailure.InvalidRequestField, result.Failure);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(string.Empty, connection.Written);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task SendAsync_NullRequest_Throws()
    {
        var client = new PrivacyPassHttpClient(new RecordedConnector(new RecordedConnection(string.Empty)));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync(null!, CancellationToken.None));
    }
}
