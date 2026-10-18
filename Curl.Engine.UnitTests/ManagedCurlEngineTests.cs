using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedCurlEngineTests
{
    [TestMethod]
    public async Task CreateEasy_ProductionEngine_GivesAHandleWithDefaultOptions()
    {
        await using ManagedCurlEngine engine = new();

        await using IEasyTransfer easy = engine.CreateEasy();

        Assert.AreEqual(new EasyOptions(), easy.Options);
        Assert.IsInstanceOfType<ManagedEasyTransfer>(easy);
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

        Assert.IsNotNull(dispatcher);
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
