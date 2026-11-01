using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Core;
using Curl.Networking;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed partial class ManagedEasyTransferTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Type: text/plain\r\n\r\nhello";

    private const string Continue = "HTTP/1.1 100 Continue\r\n\r\n";

    private const string ConnectFailure = "Failed to connect to h port 80 after 0 ms: Could not connect to server";

    [TestMethod]
    public async Task PerformAsync_HttpGetOnFakeConnector_GivesTheBodyToTheWriteCallbackAndTheHeadersToTheHeaderCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        MemoryStream body = new();
        List<string> headers = [];
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new()
            {
                Write = data =>
                {
                    body.Write(data);
                    return data.Length;
                },
                Header = header =>
                {
                    headers.Add(Encoding.ASCII.GetString(header));
                    return header.Length;
                },
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(body.ToArray()));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Type: text/plain\r\n\r\n", string.Concat(headers));
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), "GET /x HTTP/1.1\r\nHost: h\r\n");
        Assert.AreEqual(200, easy.Info.ResponseCode);
        Assert.AreEqual("http://h/x", easy.Info.EffectiveUrl);
        Assert.AreEqual(string.Empty, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_HttpGetOnFakeConnector_FillsInfoFromTheTransferReport()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual("text/plain", easy.Info.ContentType);
    }

    [TestMethod]
    public async Task PerformAsync_HttpGetWithNoUserAgent_SendsNoUserAgentLine()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("GET /x HTTP/1.1\r\nHost: h\r\nAccept: */*\r\n\r\n", Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task PerformAsync_HttpGetWithUserAgent_SendsIt()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { UserAgent = "x" },
        };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("GET /x HTTP/1.1\r\nHost: h\r\nUser-Agent: x\r\nAccept: */*\r\n\r\n", Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task PerformAsync_HttpGetWithNoUserAgentUnderToolSettings_SendsTheCurlToolsUserAgent()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, ManagedEasyTransfer.TransferSettingsOf(options), Response);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(Encoding.ASCII.GetString(written), "\r\nUser-Agent: curl/8.22.0\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_PostFieldsThenHttpGet_SendsAGetWithNoBody()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { PostFields = "a=b"u8.ToArray(), HttpGet = true, Post = false },
        };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("GET /x HTTP/1.1\r\nHost: h\r\nAccept: */*\r\n\r\n", Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task PerformAsync_PostFieldsPostThenNoBody_SendsAHeadWithNoBody()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/514" },
            Http = new EasyHttpOptions { PostFields = "moo"u8.ToArray(), PostFieldSize = 3, Post = true },
            Transfer = new EasyTransferOptions { NoBody = true },
        };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("HEAD /514 HTTP/1.1\r\nHost: h\r\nAccept: */*\r\n\r\n", Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public async Task PerformAsync_PostFieldsWithoutHttpGet_SendsAPost()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { PostFields = "a=b"u8.ToArray(), Post = true },
        };

        (byte[] written, CurlCode code) = await PerformThroughEasyAsync(options, toolSettings: null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(
            "POST /x HTTP/1.1\r\nHost: h\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\na=b",
            Encoding.ASCII.GetString(written));
    }

    [TestMethod]
    public void WithoutPostFieldsUnderHttpGet_HttpGetWithPost_KeepsThePostFields()
    {
        EasyOptions options = new() { Http = new EasyHttpOptions { PostFields = "a=b"u8.ToArray(), HttpGet = true, Post = true } };

        EasyOptions result = ManagedEasyTransfer.WithoutPostFieldsUnderHttpGet(options);

        Assert.AreSame(options, result);
    }

    [TestMethod]
    public async Task PerformAsync_NoCallbacksSet_DiscardsTheBodyAndHeadersAndSucceeds()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
    }

    /// <summary>
    /// Upstream case 694 (BL-2709): the scheme a perform's server picked stays on the handle, so the next perform
    /// reports it as <c>CURLINFO_HTTPAUTH_USED</c> and, as libcurl 8.22.0's kept <c>authhost.picked</c>, sends Basic
    /// before any challenge.
    /// </summary>
    [TestMethod]
    public async Task PerformAsync_EarlierPerformPickedBasic_NextPerformReportsBasicAsHttpAuthUsed()
    {
        ScriptedConnector connector = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        Api.Options.Authentication.EasyAuthenticationOptions anyAuth = new() { UserPassword = "alice:secret", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Any };
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/first" }, Authentication = anyAuth };
        await easy.PerformAsync(CancellationToken.None);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/second" }, Authentication = anyAuth };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string second = Encoding.ASCII.GetString(connector.Written)[Encoding.ASCII.GetString(connector.Written).IndexOf("GET /second", StringComparison.Ordinal)..];
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(1, easy.Info.HttpAuthUsed);
        Assert.Contains("Authorization: Basic YWxpY2U6c2VjcmV0", second);
    }

    [TestMethod]
    public void AuthSchemeKeptAfter_NoResultOrNoReport_KeepsTheEarlierSchemeElseTakesTheReports()
    {
        TransferResult picked = TransferResult.Success(0) with { Report = new TransferReport { AuthSchemePicked = HttpAuthSchemes.Digest } };

        Assert.AreEqual(HttpAuthSchemes.Basic, ManagedEasyTransfer.AuthSchemeKeptAfter(null, HttpAuthSchemes.Basic));
        Assert.AreEqual(HttpAuthSchemes.Basic, ManagedEasyTransfer.AuthSchemeKeptAfter(TransferResult.Success(0) with { Report = null }, HttpAuthSchemes.Basic));
        Assert.AreEqual(HttpAuthSchemes.Digest, ManagedEasyTransfer.AuthSchemeKeptAfter(picked, HttpAuthSchemes.Basic));
    }

    /// <summary><c>curl_easy_reset</c> forgets the scheme the last perform picked (BL-2709).</summary>
    [TestMethod]
    public async Task Reset_AfterAPerformThatPickedBasic_NextPerformReportsNoHttpAuthUsed()
    {
        ScriptedConnector connector = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/first" },
            Authentication = new() { UserPassword = "alice:secret", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Any },
        };
        await easy.PerformAsync(CancellationToken.None);
        easy.Reset();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/second" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(0, easy.Info.HttpAuthUsed);
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackTakesFewerBytes_ReturnsWriteError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = _ => 0 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectionRefused_ReturnsCouldntConnectWithTheConnectorsText()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual(ConnectFailure, easy.ErrorText);
        Assert.AreEqual(0, easy.Info.ResponseCode);
    }

    [TestMethod]
    public async Task PerformAsync_NoUrlSet_ReturnsUrlMalformatWithNoUrlSet()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual(ManagedEasyTransfer.NoUrlSetText, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_UnparsableUrl_ReturnsUrlMalformatAndKeepsTheUrlAsEffective()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://[::1/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.StartsWith("URL rejected: ", easy.ErrorText);
        Assert.AreEqual("http://[::1/", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_PortPastTheRange_NamesTheBadPortAsLibcurlDoes()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:99999/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual("URL rejected: Port number was not a decimal number between 0 and 65535", easy.ErrorText);
    }

    [TestMethod]
    [DataRow(CurlCode.TooLarge, "A value or data field grew larger than allowed")]
    [DataRow(CurlCode.WriteError, "Failed writing received data to disk/application")]
    public void ErrorTextOf_StockTextOfCodeLibcurlLeavesSilent_IsEmpty(CurlCode code, string stock)
    {
        Assert.AreEqual(string.Empty, ManagedEasyTransfer.ErrorTextOf(code, stock));
    }

    [TestMethod]
    public void ErrorTextOf_OtherTextOrCode_KeepsTheMessage()
    {
        Assert.AreEqual("Failure writing output to destination, passed 5 returned 3", ManagedEasyTransfer.ErrorTextOf(CurlCode.WriteError, "Failure writing output to destination, passed 5 returned 3"));
        Assert.AreEqual("A value or data field grew larger than allowed", ManagedEasyTransfer.ErrorTextOf(CurlCode.RecvError, "A value or data field grew larger than allowed"));
        Assert.AreEqual(string.Empty, ManagedEasyTransfer.ErrorTextOf(CurlCode.TooLarge, null));
    }

    [TestMethod]
    public async Task PerformAsync_ResumeFromGiven_SendsTheRange()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new() { ResumeFrom = 2 },
        };

        await easy.PerformAsync(CancellationToken.None);

        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Range: bytes=2-\r\n");
    }

    [TestMethod]
    public async Task Reset_AfterOptionsSet_RestoresDefaultOptions()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };

        easy.Reset();

        Assert.AreEqual(new EasyOptions(), easy.Options);
    }

    [TestMethod]
    public async Task Duplicate_AfterAFailedTransfer_CopiesTheOptionsAndNotTheResults()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };
        await easy.PerformAsync(CancellationToken.None);

        await using IEasyTransfer copy = easy.Duplicate();

        Assert.AreSame(easy.Options, copy.Options);
        Assert.AreEqual(string.Empty, copy.ErrorText);
        Assert.AreEqual(new Api.Info.TransferInfo(), copy.Info);
        Assert.AreEqual(CurlCode.CouldntConnect, await copy.PerformAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Pause_NoPerformRunning_IsBadFunctionArgument()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        CurlCode code = easy.Pause(CurlPause.Receive);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
    }

    [TestMethod]
    public async Task UpkeepAsync_IdleHandle_ReturnsOk()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        Assert.AreEqual(CurlCode.Ok, await easy.UpkeepAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WebSocket_BeforeAnyPerform_AnswersUnsupportedProtocolWithNoFrame()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        CurlWebSocketReceiveResult received = await easy.WebSocket.ReceiveAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, received.Result);
        Assert.IsNull(easy.WebSocket.CurrentFrame);
    }

    [TestMethod]
    public void TransferSettingsOf_EasyGroups_CarriesVerifyPeerKeyPasswordEngineTunnelAndUnixSocket()
    {
        EasyOptions options = new()
        {
            Tls = new EasyTlsOptions { Origin = new TlsPeerOptions { VerifyPeer = false, KeyPassword = "pw" }, Engine = "e" },
            Proxy = new EasyProxyOptions { HttpProxyTunnel = true },
            Network = new EasyNetworkOptions { UnixSocketPath = "/s" },
        };

        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);

        Assert.IsTrue(settings.Insecure);
        Assert.AreEqual("pw", settings.Passphrase);
        Assert.AreEqual("e", settings.Engine);
        Assert.IsTrue(settings.ProxyTunnel);
        Assert.IsTrue(settings.OverUnixSocket);
        Assert.AreEqual("http://h/?a", settings.AppendUrlQuery("http://h/?a"));
    }

    [TestMethod]
    public void TransferSettingsOf_DefaultOptions_VerifiesPeerWithNoTunnelOrUnixSocket()
    {
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions());

        Assert.IsFalse(settings.Insecure);
        Assert.IsFalse(settings.ProxyTunnel);
        Assert.IsFalse(settings.OverUnixSocket);
        Assert.IsTrue(settings.Silent);
    }

    [TestMethod]
    public void TransferSettingsOf_ProtocolsListOnlyAndProxyHeadersSet_CarriesThem()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Protocols = " HTTPS ,ftp", RedirectProtocols = "https" },
            Transfer = new EasyTransferOptions { DirListOnly = true },
            Proxy = new EasyProxyOptions { ProxyHeaders = ["X: y"] },
        };

        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);

        CollectionAssert.AreEquivalent(new[] { "https", "ftp" }, settings.AllowedProtocols!.ToArray());
        CollectionAssert.AreEquivalent(new[] { "https" }, settings.AllowedRedirectProtocols!.ToArray());
        Assert.IsTrue(settings.ListOnly);
        CollectionAssert.AreEqual(new[] { "X: y" }, settings.ProxyHeaders.ToArray());
    }

    [TestMethod]
    public void ProtocolSetOf_NotSet_GivesLibcurlsDefault()
    {
        Assert.IsNull(ManagedEasyTransfer.ProtocolSetOf(null));
    }

    [TestMethod]
    public async Task PerformAsync_FailOnErrorAgainst404_MatchesTheToolsFailTransfer()
    {
        const string NotFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\n\r\ngone";
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { FailOnError = true },
        };
        EngineTransferSettings toolSettings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions()) with { FailMode = HttpFailMode.Fail };

        (byte[] easyBytes, CurlCode easyCode) = await PerformThroughEasyAsync(options, null, NotFound);
        (byte[] toolBytes, CurlCode toolCode) = await PerformThroughToolAsync(options with { Behaviour = new() }, toolSettings, "http://h/x", NotFound);

        Assert.AreEqual(CurlCode.HttpReturnedError, easyCode);
        Assert.AreEqual(toolCode, easyCode);
        CollectionAssert.AreEqual(toolBytes, easyBytes);
    }

    [TestMethod]
    public async Task PerformAsync_ToolSettingsFailWithBody_UsesThemInPlaceOfTheOptions()
    {
        const string NotFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\n\r\ngone";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(NotFound)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        MemoryStream body = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = data => WriteTo(body, data) },
        };
        easy.ToolSettings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions()) with { FailMode = HttpFailMode.FailWithBody };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.HttpReturnedError, code);
        Assert.AreEqual("gone", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public async Task PerformAsync_ProtocolsExcludeTheUrlsScheme_MatchesTheToolsProtoRefusal()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x", Protocols = "https" } };
        EngineTransferSettings toolSettings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions()) with { AllowedProtocols = new HashSet<string> { "https" } };

        (byte[] easyBytes, CurlCode easyCode) = await PerformThroughEasyAsync(options, null, Response);
        (byte[] toolBytes, CurlCode toolCode) = await PerformThroughToolAsync(options with { Network = new EasyNetworkOptions() }, toolSettings, "http://h/x", Response);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, easyCode);
        Assert.AreEqual(toolCode, easyCode);
        Assert.IsEmpty(easyBytes);
        Assert.IsEmpty(toolBytes);
    }

    [TestMethod]
    public async Task PerformAsync_UrlWithTheGetQuery_MatchesTheToolsGetTransfer()
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x?a=b" } };
        EngineTransferSettings toolSettings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions()) with { AppendUrlQuery = url => url + "?a=b" };

        (byte[] easyBytes, CurlCode easyCode) = await PerformThroughEasyAsync(options, null, Response);
        (byte[] toolBytes, CurlCode toolCode) = await PerformThroughToolAsync(new EasyOptions(), toolSettings, "http://h/x", Response);

        Assert.AreEqual(CurlCode.Ok, easyCode);
        Assert.AreEqual(toolCode, easyCode);
        StringAssert.StartsWith(Encoding.ASCII.GetString(easyBytes), "GET /x?a=b HTTP/1.1\r\n");
        CollectionAssert.AreEqual(toolBytes, easyBytes);
    }

    [TestMethod]
    public async Task PerformAsync_UploadThroughTheReadCallback_MatchesTheToolsStandardInputUpload()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true },
        };

        (byte[] easyBytes, CurlCode easyCode) = await PerformThroughEasyAsync(options with { Callbacks = new() { Read = ReadOf("abc") } }, null, Continue + Response);
        (byte[] toolBytes, CurlCode toolCode) = await PerformThroughToolAsync(
            options, ManagedEasyTransfer.TransferSettingsOf(options), "http://h/up", Continue + Response, new CallbackReadStream(ReadOf("abc")));

        Assert.AreEqual(CurlCode.Ok, easyCode);
        Assert.AreEqual(toolCode, easyCode);
        StringAssert.StartsWith(Encoding.ASCII.GetString(easyBytes), "PUT /up HTTP/1.1\r\n");
        StringAssert.EndsWith(Encoding.ASCII.GetString(easyBytes), "\r\n\r\n3\r\nabc\r\n0\r\n\r\n");
        CollectionAssert.AreEqual(toolBytes, easyBytes);
    }

    [TestMethod]
    public async Task PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody()
    {
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true },
        };

        (byte[] easyBytes, CurlCode easyCode) = await PerformThroughEasyAsync(options, null, Response);

        string request = Encoding.ASCII.GetString(easyBytes);
        Assert.AreEqual(CurlCode.Ok, easyCode);
        StringAssert.StartsWith(request, "PUT /up HTTP/1.1\r\n");
        StringAssert.Contains(request, "\r\nContent-Length: 0\r\n");
        Assert.AreEqual(request.Length, request.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4, "Bytes followed the request head, so the body was not empty.");
    }

    [TestMethod]
    public async Task PerformAsync_CookieEngineOn_ListsTheStoredCookiesAsNetscapeLines()
    {
        const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(SetsCookie)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { CookieFiles = [string.Empty] },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual("h\tFALSE\t/\tFALSE\t0\ta\t1", easy.Info.CookieList.Single());
    }

    [TestMethod]
    public async Task PerformAsync_OnlyCookieStrings_ListsNoCookies()
    {
        const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(SetsCookie)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { Cookie = "b=2" },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsEmpty(easy.Info.CookieList);
    }

    [TestMethod]
    public async Task PerformAsync_UploadWithInFileSize_ReportsItAsTheUploadContentLength()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Response)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true, InFileSize = 3 },
            Callbacks = new() { Read = ReadOf("abc") },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(3, easy.Info.ContentLengthUpload);
    }

    [TestMethod]
    [DataRow(false, 7L, null, -1L, false, -1L)]
    [DataRow(false, -1L, "abc", -1L, false, 3L)]
    [DataRow(false, -1L, "abc", 2L, false, 2L)]
    [DataRow(false, -1L, null, 9L, true, 9L)]
    [DataRow(false, -1L, null, -1L, true, -1L)]
    [DataRow(false, -1L, null, 9L, false, -1L)]
    [DataRow(true, 7L, "abc", 2L, true, 7L)]
    public void ContentLengthUploadOf_Options_AnswersLibcurlsKnownUploadSize(bool upload, long inFileSize, string? postFields, long postFieldSize, bool post, long expected) =>
        Assert.AreEqual(expected, ManagedEasyTransfer.ContentLengthUploadOf(new EasyOptions
        {
            Transfer = new EasyTransferOptions { Upload = upload, InFileSize = inFileSize },
            Http = new EasyHttpOptions
            {
                PostFields = postFields is null ? (ReadOnlyMemory<byte>?)null : Encoding.ASCII.GetBytes(postFields),
                PostFieldSize = postFieldSize,
                Post = post,
            },
        }));

    [TestMethod]
    public async Task PerformAsync_TwoHandlesOfOneEngine_ShareNeitherTheCookiesNorThePooledConnection()
    {
        const string SetsCookie = "HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SetsCookie), Encoding.ASCII.GetBytes(Response)]);
        await using ManagedCurlEngine engine = new(TimeProvider.System, (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            new PoolingConnector(connector, shared.Connections, configuration: null),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies)));
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { CookieFiles = [string.Empty] },
        };
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = options;
        second.Options = options;

        await first.PerformAsync(CancellationToken.None);
        int written = connector.Written.Length;
        CurlCode code = await second.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(2, connector.Targets);
        Assert.DoesNotContain("Cookie:", Encoding.ASCII.GetString(connector.Written[written..]));
        Assert.AreEqual(0, first.Info.TransferId);
        Assert.AreEqual(0, second.Info.TransferId);
    }

    [TestMethod]
    public async Task ResetAndDuplicate_ToolSettingsSet_DuplicateKeepsThemAndResetDropsThem()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions());
        easy.ToolSettings = settings;

        await using ManagedEasyTransfer copy = (ManagedEasyTransfer)easy.Duplicate();
        easy.Reset();

        Assert.AreSame(settings, copy.ToolSettings);
        Assert.IsNull(easy.ToolSettings);
    }

    [TestMethod]
    public async Task PerformAsync_VerboseWithADebugCallback_GivesItTheEventsTheToolsEventsReceiveInTheirOrder()
    {
        List<(CurlInfoType Type, string Data)> easyCalls = [];
        List<(CurlInfoType Type, string Data)> toolCalls = [];
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { Verbose = true },
        };

        (_, CurlCode easyCode) = await PerformThroughEasyAsync(options with { Callbacks = new() { Debug = RecordingDebug(easyCalls) } }, null, Response);
        await PerformThroughToolAsync(
            options,
            ManagedEasyTransfer.TransferSettingsOf(options),
            "http://h/x",
            Response,
            events: new DebugCallbackTransferEvents(RecordingDebug(toolCalls), PlatformTlsBackend.ForProcess));

        Assert.AreEqual(CurlCode.Ok, easyCode);
        CollectionAssert.AreEqual(toolCalls, easyCalls);
        List<CurlInfoType> types = [.. easyCalls.Select(call => call.Type).Distinct()];
        CollectionAssert.IsSubsetOf(new[] { CurlInfoType.Text, CurlInfoType.HeaderOut, CurlInfoType.HeaderIn, CurlInfoType.DataIn }, types);
        Assert.IsTrue(types.IndexOf(CurlInfoType.HeaderOut) < types.IndexOf(CurlInfoType.HeaderIn));
        StringAssert.StartsWith(easyCalls.First(call => call.Type == CurlInfoType.HeaderOut).Data, "GET /x HTTP/1.1\r\n");
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", easyCalls.First(call => call.Type == CurlInfoType.HeaderIn).Data);
        Assert.AreEqual("hello", easyCalls.Single(call => call.Type == CurlInfoType.DataIn).Data);
        Assert.IsTrue(easyCalls.Where(call => call.Type == CurlInfoType.Text).All(call => call.Data.EndsWith('\n')));
    }

    [TestMethod]
    public async Task PerformAsync_DebugCallbackReadsTheHandlesIds_GetsNoConnectionBeforeItOpensAndItsNumberAfter()
    {
        List<(string Data, long TransferId, long ConnectionId)> calls = [];
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]) { ReportsTheConnectionOpened = true };
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { Verbose = true },
            Callbacks = new()
            {
                Debug = (_, data) =>
                {
                    calls.Add((Encoding.Latin1.GetString(data), easy.Info.TransferId, easy.Info.ConnectionId));
                    return 0;
                },
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        int connected = calls.FindIndex(call => call.ConnectionId != ManagedEasyTransfer.NoConnectionId);
        Assert.IsGreaterThan(0, connected);
        Assert.IsTrue(calls.Take(connected).All(call => call.ConnectionId == ManagedEasyTransfer.NoConnectionId));
        Assert.IsTrue(calls.Skip(connected).All(call => call.ConnectionId == 0));
        Assert.IsTrue(calls.All(call => call.TransferId == 0));
    }

    [TestMethod]
    public async Task PerformAsync_DebugCallbackWithoutVerbose_IsNotCalled()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Debug = RecordingDebug(calls) },
        };

        (_, CurlCode code) = await PerformThroughEasyAsync(options, null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsEmpty(calls);
    }

    [TestMethod]
    public async Task PerformAsync_ReadAndWriteTraceComponents_GiveTheDebugCallbackTheReadAndWriteLinesTheToolsEventsReceive()
    {
        List<(CurlInfoType Type, string Data)> easyCalls = [];
        List<(CurlInfoType Type, string Data)> toolCalls = [];
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { Verbose = true },
        };

        (_, CurlCode easyCode) = await PerformThroughEasyAsync(
            options with { Callbacks = new() { Debug = RecordingDebug(easyCalls) } }, null, Response, TracesOf("read", "write"));
        await PerformThroughToolAsync(
            options,
            ManagedEasyTransfer.TransferSettingsOf(options),
            "http://h/x",
            Response,
            events: new ClientWriterTraceEvents(new ClientReaderResetTraceEvents(new DebugCallbackTransferEvents(RecordingDebug(toolCalls), PlatformTlsBackend.ForProcess))));

        Assert.AreEqual(CurlCode.Ok, easyCode);
        CollectionAssert.AreEqual(toolCalls, easyCalls);
        List<string> texts = [.. easyCalls.Where(call => call.Type == CurlInfoType.Text).Select(call => call.Data)];
        CollectionAssert.Contains(texts, "[READ] client_reset, clear readers\n");
        CollectionAssert.Contains(texts, ClientWriterTraceEvents.DoneLine + "\n");
        Assert.IsTrue(texts.IndexOf(ClientWriterTraceEvents.DoneLine + "\n") < texts.IndexOf("[READ] client_reset, clear readers\n"));
    }

    [TestMethod]
    public async Task PerformAsync_TimerTraceComponent_GivesTheDebugCallbackTheWaitTimerLinesAfterTheRequestIsSent()
    {
        List<(CurlInfoType Type, string Data)> calls = [];
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { Verbose = true },
            Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromSeconds(5) },
            Callbacks = new() { Debug = RecordingDebug(calls) },
        };

        (_, CurlCode code) = await PerformThroughEasyAsync(options, null, Response, TracesOf("timer"));

        Assert.AreEqual(CurlCode.Ok, code);
        List<string> texts = [.. calls.Where(call => call.Type == CurlInfoType.Text).Select(call => call.Data)];
        int sent = texts.IndexOf(ResponseWaitTimerTraceEvents.RequestSentLine + "\n");
        Assert.IsGreaterThanOrEqualTo(0, sent);
        Assert.AreEqual("[TIMER] [TIMEOUT] gives multi timeout in 5000ms\n", texts[sent + 1]);
        Assert.IsFalse(texts.Any(text => text.StartsWith("[READ]", StringComparison.Ordinal) || text.StartsWith("[WRITE]", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ResetAndDuplicate_TraceComponentsSet_BothKeepThem()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        EngineTraceComponents traces = TracesOf("read");
        easy.TraceComponents = traces;

        await using ManagedEasyTransfer copy = (ManagedEasyTransfer)easy.Duplicate();
        easy.Reset();

        Assert.AreSame(traces, copy.TraceComponents);
        Assert.AreSame(traces, easy.TraceComponents);
    }

    [TestMethod]
    public async Task PerformAsync_TransferInfoCallback_GetsTheCountsTheToolsProgressSinkReceives()
    {
        List<string> easyCalls = [];
        List<string> toolCalls = [];
        using CancellationTokenSource unused = new();
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
        };

        (_, CurlCode easyCode) = await PerformThroughEasyAsync(options with { Callbacks = new() { TransferInfo = RecordingTransferInfo(easyCalls, abort: false) } }, null, Response);
        await PerformThroughToolAsync(
            options,
            ManagedEasyTransfer.TransferSettingsOf(options),
            "http://h/x",
            Response,
            progress: new CallbackTransferProgress((dlTotal, dlNow, ulTotal, ulNow) => RecordingTransferInfo(toolCalls, abort: false)(dlTotal, dlNow, ulTotal, ulNow), unused));

        Assert.AreEqual(CurlCode.Ok, easyCode);
        CollectionAssert.AreEqual(toolCalls, easyCalls);
        Assert.AreEqual("0 0 0 0", easyCalls[0]);
        CollectionAssert.Contains(easyCalls, "5 5 0 0");
    }

    [TestMethod]
    public async Task PerformAsync_TransferInfoCallbackUnderNoProgress_IsNotCalled()
    {
        List<string> calls = [];
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { TransferInfo = RecordingTransferInfo(calls, abort: false) },
        };

        (_, CurlCode code) = await PerformThroughEasyAsync(options, null, Response);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsEmpty(calls);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PerformAsync_TransferInfoCallbackReturnsNonZero_AbortsWithAbortedByCallback(bool readsObserveCancellation)
    {
        List<string> calls = [];
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response[..^3]), Encoding.ASCII.GetBytes(Response[^3..])]) { ObservesCancellation = readsObserveCancellation };
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        MemoryStream body = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
            Callbacks = new() { TransferInfo = RecordingTransferInfo(calls, abort: true), Write = data => WriteTo(body, data) },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(CallbackTransferProgress.CallbackAbortedText, easy.ErrorText);
        Assert.AreEqual("http://h/x", easy.Info.EffectiveUrl);
        Assert.AreEqual(1, calls.Count(call => call.Split(' ')[1] != "0"));
        Assert.AreNotEqual("0", calls[^1].Split(' ')[1]);
        Assert.AreEqual(readsObserveCancellation, body.Length < "hello".Length);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task PerformAsync_CallerCancels_ThrowsOperationCanceled(bool withTransferInfo)
    {
        List<string> calls = [];
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(Response)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Behaviour = new EasyBehaviourOptions { NoProgress = false },
            Callbacks = new() { TransferInfo = withTransferInfo ? RecordingTransferInfo(calls, abort: false) : null },
        };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await easy.PerformAsync(new CancellationToken(canceled: true)));
    }

    private static Api.Callbacks.DebugCallback RecordingDebug(List<(CurlInfoType Type, string Data)> calls) =>
        (type, data) =>
        {
            calls.Add((type, Encoding.Latin1.GetString(data)));
            return 0;
        };

    private static Api.Callbacks.TransferInfoCallback RecordingTransferInfo(List<string> calls, bool abort) =>
        (dlTotal, dlNow, ulTotal, ulNow) =>
        {
            calls.Add($"{dlTotal} {dlNow} {ulTotal} {ulNow}");
            return abort && dlNow > 0 ? 1 : 0;
        };

    private static long WriteTo(MemoryStream stream, ReadOnlySpan<byte> data)
    {
        stream.Write(data);
        return data.Length;
    }

    private static Api.Callbacks.ReadCallback ReadOf(string text)
    {
        MemoryStream source = new(Encoding.ASCII.GetBytes(text));
        return buffer => source.Read(buffer);
    }

    private static EngineTraceComponents TracesOf(params string[] components) =>
        new(new HashSet<string>(components, StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

    private static async Task<(byte[] Written, CurlCode Code)> PerformThroughEasyAsync(
        EasyOptions options,
        EngineTransferSettings? toolSettings,
        string response,
        EngineTraceComponents? traces = null)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        easy.Options = options;
        easy.ToolSettings = toolSettings;
        easy.TraceComponents = traces ?? EngineTraceComponents.None;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        return (connector.Written, code);
    }

    // The tool's serial attempt today: the runner's context from TransferContextFactory and the
    // attempt TransferPerformer.FollowWatchingSpeedAsync performs (CurlCommandRunner.TransferWithBodyAsync), over the
    // options a handle with no CURLOPT_USERAGENT performs with, as the tool sets its own (BL-2559).
    private static async Task<(byte[] Written, CurlCode Code)> PerformThroughToolAsync(
        EasyOptions handleOptions,
        EngineTransferSettings settings,
        string urlText,
        string response,
        Stream? upload = null,
        ITransferEvents? events = null,
        ITransferProgress? progress = null)
    {
        EasyOptions options = ManagedEasyTransfer.WithoutDefaultUserAgent(handleOptions);
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(response)]);
        Assert.IsTrue(CurlUrl.TryParse(settings.AppendUrlQuery(urlText), pathAsIs: false, out CurlUrl? url));
        TransferContext context = new TransferContextFactory(Stream.Null, TimeProvider.System).Create(
            settings,
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            url,
            Stream.Null,
            range: null,
            resumeFrom: null,
            headerOutput: null,
            upload: upload,
            progress: progress,
            events: events);

        TransferResult result = await TransferPerformer.FollowWatchingSpeedAsync(
            new EngineTransferState(),
            new RedirectFollower(CreateDispatcher(connector, cookies: null)),
            context,
            RedirectPolicyMapping.Of(settings, options.Http, options.Authentication));

        return (connector.Written, result.ExitCode.ToCurlCode());
    }

    private static ProtocolDispatcher CreateDispatcher(IConnector connector, ICookieStore? cookies) =>
        new(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies));

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(TimeProvider.System, (_, _, cookies) => CreateDispatcher(connector, cookies), readEnvironmentVariable: _ => null);
}
