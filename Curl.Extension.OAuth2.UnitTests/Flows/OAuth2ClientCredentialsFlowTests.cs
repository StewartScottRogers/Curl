using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// Pins the client credentials grant (RFC 6749 §4.4) and how every flow reads the token endpoint's answer: the token
/// (§5.1), an error (§5.2), and answers that are neither (ADR-1045).
/// </summary>
[TestClass]
public sealed class OAuth2ClientCredentialsFlowTests
{
    private static readonly OAuth2FlowRequest Request = new(
        new Uri("https://server.example.com/token"), new OAuth2Client("s6BhdRkqt3", "7Fjfp0ZBr1KtDRbnfVdmIw", OAuth2ClientAuthentication.Basic), "read");

    [TestMethod]
    public async Task RunAsync_TokenAnswer_SendsTheClientCredentialsRequestAndGivesTheToken()
    {
        var connection = new RecordedConnection(FlowResponses.Token());
        var connector = new RecordedConnector(connection);

        var result = await new OAuth2ClientCredentialsFlow(new OAuth2HttpClient(connector), Request).RunAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Message);
        Assert.AreEqual(new OAuth2TokenResponse("2YotnFZFEjr1zCsicMWpAA", "Bearer", 3600, null, null), result.Token);
        Assert.AreEqual(new ConnectTarget("server.example.com", 443, true), connector.Target);
        StringAssert.StartsWith(connection.Written, "POST /token HTTP/1.1\r\n");
        StringAssert.Contains(connection.Written, "Authorization: Basic czZCaGRSa3F0Mzo3RmpmcDBaQnIxS3REUmJuZlZkbUl3\r\n");
        StringAssert.EndsWith(connection.Written, "\r\n\r\ngrant_type=client_credentials&scope=read");
    }

    [TestMethod]
    public async Task RunAsync_ErrorAnswer_IsRefusedWithLoginDenied()
    {
        var connector = new RecordedConnector(new RecordedConnection(
            FlowResponses.Json("""{"error":"invalid_client","error_description":"Unknown client"}""", 401, "Unauthorized")));

        var result = await new OAuth2ClientCredentialsFlow(new OAuth2HttpClient(connector), Request).RunAsync(CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(OAuth2ErrorCode.InvalidClient, result.Error?.Code);
        Assert.AreEqual("The token endpoint refused the request: invalid_client (Unknown client).", result.Message);
    }

    [TestMethod]
    public async Task RunAsync_ConnectFails_GivesTheConnectorsExitCode()
    {
        var connector = new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Connection refused"));

        var result = await new OAuth2ClientCredentialsFlow(new OAuth2HttpClient(connector), Request).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server.example.com: Connection refused", result.Message);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public async Task RunAsync_SuccessWithAnUnreadableBody_IsAWeirdServerReply()
    {
        var connector = new RecordedConnector(new RecordedConnection(FlowResponses.Json("[]")));

        var result = await new OAuth2ClientCredentialsFlow(new OAuth2HttpClient(connector), Request).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The token endpoint gave an answer curl cannot use: The body is not a JSON object.", result.Message);
    }

    [TestMethod]
    public async Task RunAsync_FailureStatusWithoutAnErrorBody_IsAWeirdServerReplyNamingTheStatus()
    {
        var connector = new RecordedConnector(new RecordedConnection(FlowResponses.Json("oops", 500, "Internal Server Error")));

        var result = await new OAuth2ClientCredentialsFlow(new OAuth2HttpClient(connector), Request).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The token endpoint gave an answer curl cannot use: 500 Internal Server Error", result.Message);
    }

    [TestMethod]
    public void Refused_ExpiredTokenWithoutADescription_IsOperationTimedOut()
    {
        var result = OAuth2FlowResult.Refused(new OAuth2ErrorResponse("expired_token", OAuth2ErrorCode.ExpiredToken, null, null), "token endpoint");

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("The token endpoint refused the request: expired_token.", result.Message);
    }
}
