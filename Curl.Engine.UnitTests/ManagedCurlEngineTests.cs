using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;

namespace Curl.Engine;

[TestClass]
public sealed partial class ManagedCurlEngineTests
{
    [TestMethod]
    public async Task CreateEasy_ProductionEngine_GivesAHandleWithDefaultOptions()
    {
        await using ManagedCurlEngine engine = new();

        await using IEasyTransfer easy = engine.CreateEasy();

        Assert.AreEqual(new EasyOptions(), easy.Options);
        Assert.IsInstanceOfType<ManagedEasyTransfer>(easy);
        Assert.AreSame(EasyTransferHost.Library, ((ManagedEasyTransfer)easy).Host);
        Assert.IsFalse(((ManagedEasyTransfer)easy).Host.AltSvcUsesOverHttp);
    }

    [TestMethod]
    public void Library_Host_IgnoresAltSvcReceivedOverPlainHttp()
    {
        Assert.IsFalse(EasyTransferHost.Library.AltSvcUsesOverHttp);
    }

    [TestMethod]
    public async Task CreateEasy_EngineGivenAStandardInput_GivesAHandleReadingIt()
    {
        using MemoryStream standardInput = new();
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, standardInput: standardInput);

        await using IEasyTransfer easy = engine.CreateEasy();

        EasyTransferHost host = ((ManagedEasyTransfer)easy).Host;
        Assert.AreSame(standardInput, host.StandardInput);
        Assert.AreSame(EasyTransferHost.Library.FileSystem, host.FileSystem);
        Assert.AreSame(EasyTransferHost.Library.StandardError, host.StandardError);
    }

    [TestMethod]
    public async Task CreateEasy_EngineGivenAStandardError_GivesAHandleWritingVerboseLinesToIt()
    {
        using MemoryStream standardError = new();
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, standardError: standardError);

        await using IEasyTransfer easy = engine.CreateEasy();

        EasyTransferHost host = ((ManagedEasyTransfer)easy).Host;
        Assert.AreSame(standardError, host.StandardError.Value);
        Assert.AreSame(EasyTransferHost.Library.StandardInput, host.StandardInput);
    }

    [TestMethod]
    public async Task PerformAsync_ProductionDispatcherAndUnknownScheme_ReturnsUnsupportedProtocolWithoutConnecting()
    {
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "bogus://h/" },
            Transfer = new() { Timeout = TimeSpan.FromSeconds(5) },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, code);
    }

    [TestMethod]
    public async Task CreateDispatcher_NoTimeout_BuildsTheProductionHandlers()
    {
        await using ManagedCurlEngine engine = new();

        Core.ProtocolDispatcher dispatcher = ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null);

        foreach (string scheme in new[] { "http", "https", "ftp", "file", "dict", "tftp", "smtp", "imap", "pop3", "ws" })
        {
            Assert.IsTrue(dispatcher.Serves(scheme), scheme);
        }

        Assert.IsFalse(dispatcher.Serves("bogus"));
    }

    [TestMethod]
    public async Task Shared_ProductionEngine_HasNoEasyErrorText()
    {
        await using ManagedCurlEngine engine = new();

        Assert.AreEqual(string.Empty, engine.Shared.Performer.KnownHostsFileMissingFailure.ErrorMessage);
    }

    [TestMethod]
    public async Task Shared_EasyErrorTextGiven_ReachesThePerformer()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, code => $"text {(int)code}");

        Assert.AreEqual("text 2", engine.Shared.Performer.KnownHostsFileMissingFailure.ErrorMessage);
    }

    [TestMethod]
    public async Task SharedHsts_EnvironmentSetsCurlHstsHttp_LearnsOverPlainHttp()
    {
        Dictionary<string, string> environment = new() { ["CURL_HSTS_HTTP"] = "yes" };
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, readEnvironmentVariable: name => environment.GetValueOrDefault(name));

        bool stored = engine.Shared.Hsts.StoreFromResponse(Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com:8990/1922"), "max-age=31536000", DateTimeOffset.UtcNow);

        Assert.IsTrue(stored);
        Assert.IsTrue(engine.Shared.Hsts.TrySwitchToHttps("http://hsts.example.com/1922", Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com/1922"), out string? httpsUrl));
        Assert.AreEqual("https://hsts.example.com/1922", httpsUrl);
    }

    [TestMethod]
    public async Task SharedHsts_EnvironmentWithoutCurlHstsHttp_IgnoresPlainHttp()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, readEnvironmentVariable: _ => null);

        _ = engine.Shared.Hsts.StoreFromResponse(Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com/"), "max-age=31536000", DateTimeOffset.UtcNow);

        Assert.IsFalse(engine.Shared.Hsts.TrySwitchToHttps("http://hsts.example.com/", Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com/"), out _));
    }

    [TestMethod]
    public async Task SharedHsts_ProductionEngine_IgnoresPlainHttp()
    {
        await using ManagedCurlEngine engine = new();

        _ = engine.Shared.Hsts.StoreFromResponse(Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com/"), "max-age=31536000", DateTimeOffset.UtcNow);

        Assert.IsFalse(engine.Shared.Hsts.TrySwitchToHttps("http://hsts.example.com/", Protocol.Abstractions.CurlUrl.Parse("http://hsts.example.com/"), out _));
    }

    [TestMethod]
    public async Task VersionInfo_ProductionEngine_ListsHttpAndHttps()
    {
        await using ManagedCurlEngine engine = new();

        CollectionAssert.Contains(engine.VersionInfo.Protocols.ToArray(), "http");
        CollectionAssert.Contains(engine.VersionInfo.Protocols.ToArray(), "https");
    }

    [TestMethod]
    public async Task TwoEngines_InOneProcess_ShareNoHandleOrVersionInfo()
    {
        await using ManagedCurlEngine first = new();
        await using ManagedCurlEngine second = new();

        Assert.AreNotSame(first.VersionInfo, second.VersionInfo);
        Assert.AreNotSame(first.CreateEasy(), second.CreateEasy());
        Assert.AreNotSame(first.Shared.Cookies, second.Shared.Cookies);
        Assert.AreNotSame(first.Shared.Connections, second.Shared.Connections);
    }

    [TestMethod]
    public async Task CreateMulti_ProductionEngine_GivesANewManagedMultiHandleEachCall()
    {
        await using ManagedCurlEngine engine = new();

        await using IMultiTransfer first = engine.CreateMulti();
        await using IMultiTransfer second = engine.CreateMulti();

        Assert.IsInstanceOfType<ManagedMultiTransfer>(first);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public async Task CreateUrl_ProductionEngine_GivesAnEmptyManagedUrlHandle()
    {
        await using ManagedCurlEngine engine = new();

        using ICurlUrlHandle url = engine.CreateUrl();

        Assert.IsInstanceOfType<ManagedCurlUrlHandle>(url);
        Assert.AreEqual(CurlUrlCode.NoHost, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public async Task CreateShare_Called_GivesAManagedShareHandle()
    {
        await using ManagedCurlEngine engine = new();

        Assert.IsInstanceOfType<ManagedShareHandle>(engine.CreateShare());
    }

    [TestMethod]
    public async Task CreateMime_Called_GivesAManagedMimeBuilderWithNoParts()
    {
        await using ManagedCurlEngine engine = new();

        IMimeBuilder mime = engine.CreateMime();

        Assert.IsInstanceOfType<ManagedMimeBuilder>(mime);
        Assert.IsEmpty(mime.Build().Parts);
    }

    [TestMethod]
    public async Task CreateForm_Called_GivesAManagedFormBuilderWithNoFields()
    {
        await using ManagedCurlEngine engine = new();

        ICurlFormBuilder form = engine.CreateForm();

        Assert.IsInstanceOfType<ManagedFormBuilder>(form);
        Assert.IsEmpty(form.Build().Parts);
    }

    [TestMethod]
    public async Task PerformAsync_DialingGiven_DialsResolvesAndGetsThroughIt()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        MemoryStream body = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://bl2558.test/" },
            Callbacks = new EasyCallbackOptions { Write = data => WriteTo(body, data) },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("ok", System.Text.Encoding.ASCII.GetString(body.ToArray()));
        Assert.AreEqual("127.0.0.1", server.Targets.Single().Host);
        Assert.AreSame(dialing, engine.Shared.Dialing);
    }

    [TestMethod]
    public async Task PerformAsync_DialingGivenAndATftpUrl_OpensTheChannelThroughItsDatagramConnector()
    {
        RecordingDatagramConnector datagrams = new(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp");
        EngineDialing dialing = new(new ScriptedTcpDialer(new ScriptedConnector([])), new LoopbackDnsResolver(), datagrams);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "tftp://bl2558.test/file" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual(("bl2558.test", 69), datagrams.Opens.Single());
    }

    [TestMethod]
    public async Task PerformAsync_DialingGivenAnFtpListenerAndAnActiveFtpUrl_ListensThroughIt()
    {
        ScriptedConnector server = new(["220 hi\r\n"u8.ToArray(), "331 pass\r\n"u8.ToArray(), "230 in\r\n"u8.ToArray(), "257 \"/\"\r\n"u8.ToArray()]);
        RecordingConnectionListener listener = new();
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), listener);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "ftp://bl2597.test/file" },
            Protocols = new EasyProtocolOptions { Ftp = new EasyFtpOptions { ActiveAddress = "127.0.0.1" } },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreNotEqual(CurlCode.Ok, code);
        Assert.HasCount(1, listener.Targets);
    }

    [TestMethod]
    public async Task Shared_NoDialing_IsNull()
    {
        await using ManagedCurlEngine engine = new();

        Assert.IsNull(engine.Shared.Dialing);
        Assert.IsNull(engine.Shared.SigningClock);
        Assert.IsNull(engine.Shared.WebSocketRandomSource);
        Assert.IsNull(engine.Shared.CreateFormBoundary);
    }

    [TestMethod]
    public async Task PerformAsync_FormBoundaryGiven_SendsTheMimePostWithIt()
    {
        const string Boundary = "------------------------qrstuvwxyz0123456789AB";
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing, createFormBoundary: () => Boundary);
        await using IEasyTransfer easy = engine.CreateEasy();
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder part = mime.AddPart();
        part.SetName("foo");
        part.SetData("bar"u8.ToArray());
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/bl2695" },
            Http = new EasyHttpOptions { MimePost = mime.Build() },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("Content-Type: multipart/form-data; boundary=" + Boundary + "\r\n", request);
        Assert.Contains("--" + Boundary + "--\r\n", request);
    }

    [TestMethod]
    public async Task CreateEasy_WebSocketRandomSourceGiven_HandleDrawsFromIt()
    {
        ManagedEasyWebSocketTests.ZeroMaskSource source = new();
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, webSocketRandomSource: source);

        await using IEasyTransfer easy = engine.CreateEasy();

        Assert.AreSame(source, ((ManagedEasyTransfer)easy).WebSocketRandomSource);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketRandomSourceGiven_SendsTheKeyItGives()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing, webSocketRandomSource: new ManagedEasyWebSocketTests.ZeroMaskSource());
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "ws://127.0.0.1:9000/bl2688" } };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.Contains("Sec-WebSocket-Key: AAAAAAAAAAAAAAAAAAAAAA==\r\n", request);
    }

    [TestMethod]
    public async Task PerformAsync_SigningClockGivenAndAwsSigV4_SignsAtThatClocksTime()
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing, signingClock: new FixedClock(DateTimeOffset.UnixEpoch));
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/bl2675" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "xxx:yyy", AwsSigV4 = "xxx" },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("X-Xxx-Date: 19700101T000000Z\r\n", request);
        Assert.Contains("Credential=xxx/19700101/", request);
    }

    [TestMethod]
    public async Task PerformAsync_DialingUsesHandBuiltNtlm_SendsCurlsOwnTypeOneMessage()
    {
        ScriptedConnector server = new(["HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/bl2635" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "me:password", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Ntlm },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.Contains("Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=\r\n", request);
    }

    /// <summary>
    /// Upstream case 1686 (BL-2666): with the harness's hand-built NTLM the engine is curl's non-SSPI
    /// build, so a Digest answer joins its fields with <c>", "</c> as curl 8.22.0's <c>lib/vauth/digest.c</c>.
    /// </summary>
    [TestMethod]
    public async Task PerformAsync_DialingUsesHandBuiltNtlmAndDigest_SeparatesTheFieldsWithACommaAndASpace()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"my-backyard\", nonce=\"314156295\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/api" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "alice:secret", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Digest },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.Contains("Authorization: Digest username=\"alice\", realm=\"my-backyard\", nonce=\"314156295\", uri=\"/api\", response=\"", request);
    }

    /// <summary>
    /// Upstream case 2024 (BL-2747): a 401 offering Digest to a Basic transfer is kept on the handle, and the
    /// handle's next transfer under Digest alone answers it on its first request, as curl 8.22.0's authhost state.
    /// </summary>
    [TestMethod]
    public async Task PerformAsync_EarlierTransferGotDigestChallenge_NextDigestTransferAnswersItFirst()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"1\"\r\nWWW-Authenticate: Basic realm=\"testrealm\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/20240100" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "testuser:wrongpass", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Basic },
        };
        await easy.PerformAsync(CancellationToken.None);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/20240200" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "testuser:testpass", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Digest },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        string second = request[request.IndexOf("GET /20240200", StringComparison.Ordinal)..];
        Assert.Contains("Authorization: Digest username=\"testuser\", realm=\"testrealm\", nonce=\"1\", uri=\"/20240200\", response=\"", second);
        Assert.AreEqual(200, easy.Info.ResponseCode);
    }

    /// <summary>
    /// Upstream case 2030 (BL-2820): a transfer whose last 401 offered Digest while it already held a Digest nonce, kept
    /// from the handle's previous transfer, ends with Digest still marked available, which the handle passes to its next transfer's HTTP options
    /// as <c>DigestStaysAvailable</c>, as libcurl 8.22.0 keeps <c>authhost.avail</c>; <c>Reset</c> clears it.
    /// </summary>
    [TestMethod]
    public async Task PerformAsync_EarlierTransferEndedWithDigestStillAvailable_NextTransferCarriesItUntilReset()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"1\"\r\nWWW-Authenticate: Basic realm=\"testrealm\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"2\"\r\nWWW-Authenticate: Basic realm=\"testrealm\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        ManagedEasyTransfer handle = (ManagedEasyTransfer)easy;
        EasyOptions basic = new()
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/20300100" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "testuser:wrongpass", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Basic },
        };
        easy.Options = basic;
        await easy.PerformAsync(CancellationToken.None);
        await easy.PerformAsync(CancellationToken.None);
        bool firstCarried = handle.LastContext!.Http!.DigestStaysAvailable;

        await easy.PerformAsync(CancellationToken.None);
        bool nextCarried = handle.LastContext!.Http!.DigestStaysAvailable;
        easy.Reset();
        easy.Options = basic;
        await easy.PerformAsync(CancellationToken.None);

        Assert.IsFalse(firstCarried);
        Assert.IsTrue(nextCarried);
        Assert.IsFalse(handle.LastContext!.Http!.DigestStaysAvailable);
    }

    /// <summary>
    /// Upstream case 3101 (BL-2714): under <c>CURLAUTH_ANY</c> with credentials and redirects limited to <c>https</c>, a
    /// 401 offering an unknown scheme and Basic is answered with <c>Authorization: Basic</c>; an authentication retry is
    /// not a redirect, so the redirect-protocol list does not stop it.
    /// </summary>
    [TestMethod]
    public async Task PerformAsync_AnyAuthAndHttpsOnlyRedirectProtocols_AnswersABasicChallengeWithBasic()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Authorization Required\r\nWWW-Authenticate: Blackmagic realm=\"x\"\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/3101", RedirectProtocols = "https" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserName = "user", Password = "password", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Any },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.Contains("Authorization: Basic dXNlcjpwYXNzd29yZA==\r\n", request);
        Assert.AreEqual(200, easy.Info.ResponseCode);
    }

    /// <summary>CURLOPT_USERNAME and CURLOPT_PASSWORD each replace their half of CURLOPT_USERPWD, and either alone is a credential.</summary>
    [TestMethod]
    [DataRow(null, "u", null, "u:")]
    [DataRow(null, null, "p", ":p")]
    [DataRow("a:b", "u", null, "u:b")]
    [DataRow("a:b", null, "p", "a:p")]
    [DataRow("a", null, null, "a:")]
    public async Task PerformAsync_UserNameAndPasswordOptions_ReplaceTheirHalfOfUserPassword(string? userPassword, string? userName, string? password, string expected)
    {
        ScriptedConnector server = new(["HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/creds" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = userPassword, UserName = userName, Password = password },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        Assert.Contains("Authorization: Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(expected)) + "\r\n", request);
    }

    /// <summary>Upstream case 1985: a Digest challenge kept from one host is not answered to another.</summary>
    [TestMethod]
    public async Task PerformAsync_EarlierTransferGotDigestChallengeFromAnotherPort_NextDigestTransferSendsNoAuthorizationFirst()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"1\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/first" } };
        await easy.PerformAsync(CancellationToken.None);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9001/second" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "testuser:testpass", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Digest },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        string second = request[request.IndexOf("GET /second", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Authorization:", second);
    }

    [TestMethod]
    public void ChallengesOfLast401_ResponsesOtherThanA401OrNone_KeepNoChallenges()
    {
        Curl.Protocol.Abstractions.TransferReport ok = new() { ResponseCode = 200, ResponseHeaders = [new("WWW-Authenticate", "Digest nonce=\"1\"")] };
        Curl.Protocol.Abstractions.TransferReport unauthorized = new() { ResponseCode = 401, ResponseHeaders = [new("www-authenticate", " Digest nonce=\"1\" "), new("Content-Length", "0")] };

        Assert.IsEmpty(ManagedEasyTransfer.ChallengesOfLast401(null));
        Assert.IsEmpty(ManagedEasyTransfer.ChallengesOfLast401(Curl.Protocol.Abstractions.TransferResult.Success(0)));
        Assert.IsEmpty(ManagedEasyTransfer.ChallengesOfLast401(Curl.Protocol.Abstractions.TransferResult.Success(0) with { Report = ok }));
        CollectionAssert.AreEqual(new[] { "Digest nonce=\"1\"" }, ManagedEasyTransfer.ChallengesOfLast401(Curl.Protocol.Abstractions.TransferResult.Success(0) with { Report = unauthorized }).ToArray());
    }

    [TestMethod]
    public async Task Reset_AfterAPerformThatEndedWithADigestChallenge_NextDigestTransferSendsNoAuthorizationFirst()
    {
        ScriptedConnector server = new([
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"1\"\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(),
        ]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"), UsesHandBuiltNtlm: true);
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/first" } };
        await easy.PerformAsync(CancellationToken.None);
        easy.Reset();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://127.0.0.1:9000/second" },
            Authentication = new Api.Options.Authentication.EasyAuthenticationOptions { UserPassword = "testuser:testpass", HttpAuthentication = Api.Options.Authentication.HttpAuthenticationMethods.Digest },
        };

        await easy.PerformAsync(CancellationToken.None);

        string request = System.Text.Encoding.ASCII.GetString(server.Written);
        string second = request[request.IndexOf("GET /second", StringComparison.Ordinal)..];
        Assert.DoesNotContain("Authorization:", second);
    }

    [TestMethod]
    public async Task PerformAsync_StopsFirstFtpTransferBeforePwd_FirstFtpPerformSendsNoPwdAndTimesOut()
    {
        ScriptedConnector server = new(["220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"u8.ToArray()]);
        EngineDialing dialing = new(new ScriptedTcpDialer(server), new LoopbackDnsResolver(), new RecordingDatagramConnector(Curl.Protocol.Abstractions.CurlExitCode.CouldntConnect, "no udp"));
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, dialing: dialing, stopsFirstFtpTransferBeforePwd: true);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "ftp://127.0.0.1:9000/753", ConnectTimeout = TimeSpan.FromMilliseconds(200) } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.OperationTimedOut, code);
        Assert.AreEqual("USER anonymous\r\nPASS ftp@example.com\r\n", System.Text.Encoding.ASCII.GetString(server.Written));
    }

    [TestMethod]
    public async Task CreateDispatcher_StopsFirstFtpTransferBeforePwd_OnlyTheFirstDispatcherClaimsTheStop()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null, stopsFirstFtpTransferBeforePwd: true);

        _ = ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null);
        _ = ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null);

        Assert.IsNotNull(engine.Shared.FtpPwdStop);
        Assert.IsFalse(engine.Shared.FtpPwdStop.TryClaim());
    }

    [TestMethod]
    public async Task Constructor_NoFtpPwdStopAsked_SharesNone()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null);

        Assert.IsNull(engine.Shared.FtpPwdStop);
    }

    [TestMethod]
    public async Task DisposeAsync_HandleStillInAMultiHandle_RemovesItFromTheMultiHandle()
    {
        await using ManagedCurlEngine engine = new(TimeProvider.System, null);
        await using IMultiTransfer multi = engine.CreateMulti();
        IEasyTransfer easy = engine.CreateEasy();
        multi.Add(easy);

        await easy.DisposeAsync();

        Assert.IsEmpty(multi.GetHandles());
        Assert.IsFalse(engine.Shared.MultiOwners.ContainsKey(easy));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static long WriteTo(MemoryStream body, ReadOnlySpan<byte> data)
    {
        body.Write(data);
        return data.Length;
    }
}
