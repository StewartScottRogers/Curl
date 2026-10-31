using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Http;

/// <summary>Pins the token endpoint exchange over recorded bytes against RFC 9112 and RFC 6749 §4.4.2 and §5.2.</summary>
[TestClass]
public sealed class OAuth2HttpClientTests
{
    private static readonly OAuth2FormRequest ClientCredentials = new("grant_type=client_credentials", "Basic czZCaGRSa3F0MzpnWDFmQmF0M2JW");

    private static readonly Uri TokenEndpoint = new("https://server.example.com/token");

    [TestMethod]
    public async Task PostFormAsync_Section442Example_SendsTheFormPostAndClosesTheConnection()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}");
        var connector = new RecordedConnector(connection);

        await new OAuth2HttpClient(connector).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(
            "POST /token HTTP/1.1\r\nHost: server.example.com\r\nAccept: application/json\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\nAuthorization: Basic czZCaGRSa3F0MzpnWDFmQmF0M2JW\r\n"
            + "Content-Length: 29\r\nConnection: close\r\n\r\ngrant_type=client_credentials",
            connection.Written);
        Assert.AreEqual(1, connection.FlushCount);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreEqual(new ConnectTarget("server.example.com", 443, true), connector.Target);
    }

    [TestMethod]
    public async Task PostFormAsync_PublicClientOnIPv6WithPort_SendsNoAuthorizationAndABracketedHost()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}");
        var connector = new RecordedConnector(connection);

        await new OAuth2HttpClient(connector).PostFormAsync(
            new Uri("http://[::1]:8080/oauth/token?tenant=a"), new OAuth2FormRequest("client_id=c", null), CancellationToken.None);

        Assert.AreEqual(
            "POST /oauth/token?tenant=a HTTP/1.1\r\nHost: [::1]:8080\r\nAccept: application/json\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\nContent-Length: 11\r\nConnection: close\r\n\r\nclient_id=c",
            connection.Written);
        Assert.AreEqual(new ConnectTarget("::1", 8080, false), connector.Target);
    }

    [TestMethod]
    public async Task PostFormAsync_InternationalHost_ConnectsAndSendsHostInPunycode()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        var connector = new RecordedConnector(connection);

        await new OAuth2HttpClient(connector).PostFormAsync(new Uri("https://bücher.example/token"), ClientCredentials, CancellationToken.None);

        StringAssert.Contains(connection.Written, "\r\nHost: xn--bcher-kva.example\r\n");
        Assert.AreEqual("xn--bcher-kva.example", connector.Target?.Host);
    }

    [TestMethod]
    public async Task PostFormAsync_ContentLengthBody_ReturnsTheStatusHeadersAndBody()
    {
        const string Json = """{"access_token":"2YotnFZFEjr1zCsicMWpAA","token_type":"example","expires_in":3600}""";
        var connection = new RecordedConnection($"HTTP/1.1 200 OK\r\nContent-Type: application/json;charset=UTF-8\r\nCache-Control:  no-store \r\nContent-Length: {Json.Length}\r\n\r\n{Json}", readSize: 7);

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Failure);
        Assert.IsNull(result.Message);
        Assert.AreEqual(200, result.Response!.StatusCode);
        Assert.AreEqual("OK", result.Response.ReasonPhrase);
        Assert.AreEqual("no-store", result.Response.FindHeader("cache-control"));
        Assert.IsNull(result.Response.FindHeader("Pragma"));
        Assert.AreEqual(Json, result.Response.Body);
        Assert.AreEqual("2YotnFZFEjr1zCsicMWpAA", OAuth2TokenResponseParser.Parse(result.Response.Body).Value?.AccessToken);
    }

    [TestMethod]
    public async Task PostFormAsync_Section52ErrorWith400_ReturnsTheErrorBody()
    {
        var connection = new RecordedConnection("HTTP/1.1 400 Bad Request\r\nContent-Length: 27\r\n\r\n{\"error\":\"invalid_request\"}");

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(400, result.Response?.StatusCode);
        Assert.AreEqual(OAuth2ErrorCode.InvalidRequest, OAuth2ErrorResponseParser.Parse(result.Response!.Body).Value?.Code);
    }

    [TestMethod]
    public async Task PostFormAsync_ChunkedBodyWithExtensionAndTrailer_ReturnsTheJoinedChunks()
    {
        var connection = new RecordedConnection(
            "HTTP/1.1 401 Unauthorized\r\nTransfer-Encoding: chunked\r\n\r\n5;name=value\r\n{\"err\r\nA \r\nor\":\"x\"}\r\n\r\n0\r\nExpires: 0\r\n\r\n", readSize: 3);

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(401, result.Response?.StatusCode);
        Assert.AreEqual("{\"error\":\"x\"}\r\n", result.Response?.Body);
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK\r\n\r\n{}", DisplayName = "No framing: the close")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n{}", DisplayName = "Not chunked last: the close")]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 103 Early Hints\r\nLink: </s>\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}", DisplayName = "Interim 1xx skipped")]
    public async Task PostFormAsync_FramedResponse_ReturnsTheBody(string response)
    {
        var result = await new OAuth2HttpClient(new RecordedConnector(new RecordedConnection(response))).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(200, result.Response?.StatusCode);
        Assert.AreEqual("{}", result.Response?.Body);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 204\r\nContent-Length: 5\r\n\r\n", 204)]
    [DataRow("HTTP/1.1 304 Not Modified\r\nTransfer-Encoding: chunked\r\n\r\n", 304)]
    public async Task PostFormAsync_StatusWithoutBody_ReturnsAnEmptyBody(string response, int statusCode)
    {
        var result = await new OAuth2HttpClient(new RecordedConnector(new RecordedConnection(response))).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(statusCode, result.Response?.StatusCode);
        Assert.AreEqual(string.Empty, result.Response?.Body);
    }

    [TestMethod]
    [DataRow("", OAuth2HttpFailure.NoResponse, CurlExitCode.GotNothing)]
    [DataRow("HTTP/1.1 200", OAuth2HttpFailure.NoResponse, CurlExitCode.GotNothing)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n", OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n{}", OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\n{}", OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n", OAuth2HttpFailure.ClosedEarly, CurlExitCode.PartialFile)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2\r\n{}x\r\n0\r\n\r\n", OAuth2HttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n", OAuth2HttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFF\r\n", OAuth2HttpFailure.InvalidChunk, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n7FFFFFFF\r\n", OAuth2HttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 1048577\r\n\r\n", OAuth2HttpFailure.BodyTooLarge, CurlExitCode.FilesizeExceeded)]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 1, 1\r\n\r\n", OAuth2HttpFailure.InvalidContentLength, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nX: a\r\n folded\r\n\r\n", OAuth2HttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nNo colon\r\n\r\n", OAuth2HttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\n: empty name\r\n\r\n", OAuth2HttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 200 OK\r\nBad name: x\r\n\r\n", OAuth2HttpFailure.MalformedHeaderField, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/2 200 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("ICY 200 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.x 200 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1-200 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 2x0 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    [DataRow("HTTP/1.1 2000 OK\r\n\r\n", OAuth2HttpFailure.MalformedStatusLine, CurlExitCode.WeirdServerReply)]
    public async Task PostFormAsync_BrokenResponse_FailsWithItsCause(string response, OAuth2HttpFailure failure, CurlExitCode exitCode)
    {
        var connection = new RecordedConnection(response);

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Response);
        Assert.AreEqual(failure, result.Failure);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task PostFormAsync_HeadLineOver64KiB_FailsAsLineTooLong()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\nX: " + new string('a', OAuth2HttpConnectionReader.MaxLineLength) + "\r\n\r\n");

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.LineTooLong, result.Failure);
    }

    [TestMethod]
    public async Task PostFormAsync_CloseDelimitedBodyOver1MiB_FailsAsBodyTooLarge()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n" + new string('a', OAuth2HttpBodyReader.MaxBodyLength + 1));

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.BodyTooLarge, result.Failure);
    }

    [TestMethod]
    public async Task PostFormAsync_ConnectionResetWhileReading_FailsAsARecvError()
    {
        var connection = new RecordedConnection(null);

        var result = await new OAuth2HttpClient(new RecordedConnector(connection)).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.ConnectionFailed, result.Failure);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task PostFormAsync_ConnectorFails_ReturnsTheConnectorsExitCode()
    {
        var connector = new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Connection refused"));

        var result = await new OAuth2HttpClient(connector).PostFormAsync(TokenEndpoint, ClientCredentials, CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.ConnectFailed, result.Failure);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server.example.com: Connection refused", result.Message);
    }

    [TestMethod]
    [DataRow("ftp://server.example.com/token")]
    [DataRow("oauth/token")]
    public async Task PostFormAsync_EndpointNotHttp_FailsWithoutConnecting(string endpoint)
    {
        var connector = new RecordedConnector(new RecordedConnection(string.Empty));

        var result = await new OAuth2HttpClient(connector).PostFormAsync(new Uri(endpoint, UriKind.RelativeOrAbsolute), ClientCredentials, CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.UnsupportedEndpoint, result.Failure);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    [DataRow("Basic a\r\nX-Injected: 1")]
    [DataRow("Basic a\nb")]
    public async Task PostFormAsync_AuthorizationWithLineBreak_FailsWithoutConnecting(string authorization)
    {
        var connector = new RecordedConnector(new RecordedConnection(string.Empty));

        var result = await new OAuth2HttpClient(connector).PostFormAsync(TokenEndpoint, new OAuth2FormRequest("a=b", authorization), CancellationToken.None);

        Assert.AreEqual(OAuth2HttpFailure.InvalidRequestField, result.Failure);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task PostFormAsync_NullArguments_Throw()
    {
        var client = new OAuth2HttpClient(new RecordedConnector(new RecordedConnection(string.Empty)));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.PostFormAsync(null!, ClientCredentials, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.PostFormAsync(TokenEndpoint, null!, CancellationToken.None));
    }
}
