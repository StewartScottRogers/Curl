using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins what <see cref="HttpProtocolHandler" /> gives its authenticator for <c>--aws-sigv4</c>
/// (<see cref="HttpAuthRequest.AwsSigV4" />), that a value of several lines goes in the
/// <c>Authorization</c> slot, and that a refusal to sign fails the transfer once connected
/// with nothing sent, as curl 8.21.0 does (BL-629 Notes).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SignedValue = "AWS4-HMAC-SHA256 Credential=u/x\r\nX-Amz-Date: 20260929T165340Z";

    [TestMethod]
    public async Task ExecuteAsync_AwsSigV4Get_GivesTheInputsAndSendsEveryLineInTheAuthorizationSlot()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignedValue, null);
        string[] headers = ["X-A: 1"];
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            PathAsIs = true,
            Http = new HttpRequestOptions { AwsSigV4 = "aws:amz:us-east-1:s3", Headers = headers },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + SignedValue + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nX-A: 1\r\n\r\n",
            connection.Written);
        AwsSigV4Inputs inputs = authenticator.Calls.Single().Request.AwsSigV4!;
        Assert.AreEqual(new AwsSigV4Inputs("aws:amz:us-east-1:s3", "127.0.0.1:18183", headers) { IsGetOrHead = true, PathAsIs = true }, inputs);
    }

    [TestMethod]
    public async Task ExecuteAsync_AwsSigV4PostData_GivesTheBodyAsPostFields()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignedValue, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AwsSigV4 = "aws", Body = new BytesBody("hello"u8.ToArray(), "application/x-www-form-urlencoded") },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        AwsSigV4Inputs inputs = authenticator.Calls.Single().Request.AwsSigV4!;
        Assert.AreEqual("hello", Encoding.Latin1.GetString(inputs.PostFields!.Value.Span));
        Assert.AreEqual(-1, inputs.UploadSize);
        Assert.IsFalse(inputs.IsGetOrHead);
    }

    [TestMethod]
    [DataRow(false, 5L, DisplayName = "Known size")]
    [DataRow(true, -1L, DisplayName = "Unknown size")]
    public async Task ExecuteAsync_AwsSigV4Upload_GivesTheUploadSize(bool unseekable, long expectedSize)
    {
        byte[] content = "abcde"u8.ToArray();
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignedValue, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Upload = unseekable ? new UnseekableStream(content) : new MemoryStream(content),
            Http = new HttpRequestOptions { AwsSigV4 = "aws" },
        };

        await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        AwsSigV4Inputs inputs = authenticator.Calls.Single().Request.AwsSigV4!;
        Assert.AreEqual(expectedSize, inputs.UploadSize);
        Assert.IsNull(inputs.PostFields);
        Assert.IsFalse(inputs.IsGetOrHead);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAwsSigV4_GivesNoInputs()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, null);

        await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(AuthContext(new MemoryStream(), new MemoryStream()));

        Assert.IsNull(authenticator.Calls.Single().Request.AwsSigV4);
    }

    [TestMethod]
    public async Task ExecuteAsync_AwsSigV4ThroughAForwardProxy_GivesTheProxyNoInputs()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignedValue, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AwsSigV4 = "aws", ForwardProxy = ChallengingProxy },
        };

        await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        Assert.IsNotNull(authenticator.Calls.Single(call => !call.Request.IsProxy).Request.AwsSigV4);
        Assert.IsNull(authenticator.Calls.Single(call => call.Request.IsProxy).Request.AwsSigV4);
    }

    /// <summary>
    /// Measured: <c>curl -sS -u AKID:SECRET --aws-sigv4 aws http://localhost:18631/x</c>
    /// connects, sends nothing and exits 3 with the signer's message.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_AuthenticatorRefusesTheFirstRequest_FailsOnceConnectedAndSendsNothing()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        QueueConnector connector = QueueConnector.For(connection);
        RefusingAuthenticator authenticator = new(CurlExitCode.UrlMalformat, "aws-sigv4: service missing in parameters and hostname");

        TransferResult result = await new HttpProtocolHandler(connector, authenticator).ExecuteAsync(AuthContext(new MemoryStream(), new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("aws-sigv4: service missing in parameters and hostname", result.ErrorMessage);
        Assert.HasCount(1, connector.Targets);
        Assert.AreEqual(string.Empty, connection.Written);
        Assert.AreEqual(1, authenticator.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_AwsSigV4ConnectRefused_KeepsTheConnectFailureAsTheErrorMessage()
    {
        const string connectFailure = "Failed to connect to 127.0.0.1 port 18183 after 0 ms: Could not connect to server";
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, connectFailure));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AwsSigV4 = "aws:amz:us-east-1:s3" },
        };

        TransferResult result = await new HttpProtocolHandler(connector, SigV4LineReporter()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(connectFailure, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AwsSigV4FailOn401_KeepsTheReturnedErrorMessage()
    {
        TurnTakingConnection connection = new(65536, NegotiateDenied);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AwsSigV4 = "aws:amz:us-east-1:s3", Fail = HttpFailMode.Fail },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), SigV4LineReporter()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
    }

    /// <summary>
    /// An authenticator that signs as <c>--aws-sigv4</c> does, reporting the signer's <c>-v</c>
    /// lines, the string to sign among them, before it gives its value.
    /// </summary>
    private static LineReportingAuthenticator SigV4LineReporter() => new(
        SignedValue,
        "aws_sigv4: picked service s3 from host",
        "aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256\n20260929T165340Z]",
        "aws_sigv4: Signature - 0123",
        "Server auth using AWS_SIGV4 with user 'u'");
}
