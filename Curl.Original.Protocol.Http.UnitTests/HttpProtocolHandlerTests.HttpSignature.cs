using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins what <see cref="HttpProtocolHandler" /> gives its authenticator for the
/// <c>--httpsig-*</c> options (<see cref="HttpAuthRequest.HttpSignature" />), that the value's
/// <c>Signature-Input</c> and <c>Signature</c> lines go in the <c>Authorization</c> slot whatever
/// the <c>-H</c> values, and that the handler writes no <c>Server auth using</c> line of its own
/// for them, as curl 8.22.0 does (BL-2182; upstream test 5001).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SignatureLines = "Signature-Input: sig1=(\"@method\");created=0;keyid=\"k\";alg=\"ed25519\"\r\nSignature: sig1=:AA==:";

    private static readonly HttpSignatureSettings SignatureSettings = new("ed25519", "00", "k", null);

    [TestMethod]
    public async Task ExecuteAsync_HttpSignatureWithHostAndAuthorizationHeaders_SignsForTheHostHeaderAndSendsTheLinesInTheAuthorizationSlot()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignatureLines, null);
        string[] headers = ["Host: example.com:8000", "Authorization: Bearer t"];
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { HttpSignature = SignatureSettings, Headers = headers },
        };
        Diagnostics.Arrange("url, headers", $"{AuthUrl}, {string.Join("|", headers)}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string expected = "GET /a HTTP/1.1\r\nHost: example.com:8000\r\n" + SignatureLines + "\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nAuthorization: Bearer t\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
        HttpSignatureInputs inputs = authenticator.Calls.Single().Request.HttpSignature!;
        Diagnostics.Assert("inputs", new HttpSignatureInputs(SignatureSettings, "example.com:8000", headers), inputs);
        Assert.AreEqual(new HttpSignatureInputs(SignatureSettings, "example.com:8000", headers), inputs);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpSignatureWithoutHostHeader_SignsForTheUrlsHostAndPort()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignatureLines, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { HttpSignature = SignatureSettings },
        };
        Diagnostics.Arrange("url", AuthUrl);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        WriteResult(result);
        string authority = authenticator.Calls.Single().Request.HttpSignature!.Authority;
        Diagnostics.Assert("authority", "127.0.0.1:18183", authority);
        Assert.AreEqual("127.0.0.1:18183", authority);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpSignatureNothingSigned_SendsNoSignatureLines()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { HttpSignature = SignatureSettings, Headers = ["Signature: mine"] },
        };
        Diagnostics.Arrange("url, headers", $"{AuthUrl}, Signature: mine");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        WriteResult(result);
        const string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nSignature: mine\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpSignatureThroughAForwardProxy_GivesTheProxyNoInputs()
    {
        TurnTakingConnection connection = new(int.MaxValue, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(SignatureLines, null);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { HttpSignature = SignatureSettings, ForwardProxy = ChallengingProxy },
        };
        Diagnostics.Arrange("url, forward proxy", $"{AuthUrl}, challenging");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("proxy call has inputs", false, authenticator.Calls.Single(call => call.Request.IsProxy).Request.HttpSignature is not null);
        Assert.IsNotNull(authenticator.Calls.Single(call => !call.Request.IsProxy).Request.HttpSignature);
        Assert.IsNull(authenticator.Calls.Single(call => call.Request.IsProxy).Request.HttpSignature);
    }

    [TestMethod]
    public async Task ExecuteAsync_HttpSignatureFailOn401_KeepsTheReturnedErrorMessageAndWritesNoAuthUsingLine()
    {
        TurnTakingConnection connection = new(65536, NegotiateDenied);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { HttpSignature = SignatureSettings, Fail = HttpFailMode.Fail },
        };
        Diagnostics.Arrange("url, fail mode", $"{AuthUrl}, Fail");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new LineReportingAuthenticator(SignatureLines, "httpsig: Signature: sig1=:AA==:")).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("error message", "The requested URL returned error: 401", result.ErrorMessage ?? "(none)");
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
    }
}
