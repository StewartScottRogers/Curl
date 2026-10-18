using System.Net;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the proxy a perform from the options of a handle with no <see cref="ManagedEasyTransfer.ToolInputs" />
/// chooses (BL-2519, ADR-0608): <c>CURLOPT_PROXY</c> and <c>CURLOPT_NOPROXY</c>, and the proxy environment
/// variables the engine's injected reader gives, connect-only performs included, as libcurl reads them for every handle.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private static ManagedEasyTransfer EasyOver(RecordingProtocolHandler handler, Dictionary<string, string> environment) =>
        (ManagedEasyTransfer)new ManagedCurlEngine(
            TimeProvider.System,
            (_, _, _) => new ProtocolDispatcher([handler]),
            readEnvironmentVariable: name => environment.GetValueOrDefault(name)).CreateEasy();

    [TestMethod]
    public async Task PerformAsync_ProxyOptionSet_PutsThatProxyOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128" },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Http, "proxy", 3128, null), handler.Contexts.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_NoProxyNamesTheHost_PutsNoProxyOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, new Dictionary<string, string> { ["http_proxy"] = "http://envproxy:8080" });
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128", NoProxy = "h" },
        };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(handler.Contexts.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_NoProxyOptionAndHttpProxyInTheEnvironment_PutsTheEnvironmentsProxyOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, new Dictionary<string, string> { ["http_proxy"] = "http://envproxy:8080" });
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Http, "envproxy", 8080, null), handler.Contexts.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_NoProxyOptionAndNoProxyEnvironment_PutsNoProxyOnTheContext()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.IsNull(handler.Contexts.Single().Proxy);
    }

    [TestMethod]
    public async Task PerformAsync_UnusableProxyText_AnswersTheSelectorsFailureAndRunsNoTransfer()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Proxy = new EasyProxyOptions { Proxy = "foo://proxy:1" },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual("Unsupported proxy scheme for 'foo://proxy:1'", easy.ErrorText);
        Assert.IsEmpty(handler.Contexts);
    }

    [TestMethod]
    public async Task PerformAsync_ProxyUserPasswordSet_PutsItsCredentialsOnTheContextsProxy()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath("http");
        await using ManagedEasyTransfer easy = EasyOver(handler, []);
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Proxy = new EasyProxyOptions { Proxy = "http://u:p@proxy:3128" },
            Authentication = new EasyAuthenticationOptions { ProxyUserPassword = "silly:person", ProxyAuthentication = HttpAuthenticationMethods.Digest },
        };

        await easy.PerformAsync(CancellationToken.None);

        NetworkCredential? credential = handler.Contexts.Single().Proxy?.Credential;
        Assert.AreEqual("silly", credential?.UserName);
        Assert.AreEqual("person", credential?.Password);
    }

    [TestMethod]
    public void ProxyCredentialsOf_NoProxyCredentialOption_IsNull()
    {
        Assert.IsNull(ManagedEasyTransfer.ProxyCredentialsOf(new EasyAuthenticationOptions()));
    }

    [TestMethod]
    [DataRow("silly", null, null, "silly", "")]
    [DataRow("silly:per:son", null, null, "silly", "per:son")]
    [DataRow(":secret", null, null, "", "secret")]
    [DataRow("silly:person", "name", null, "name", "person")]
    [DataRow("silly:person", null, "word", "silly", "word")]
    [DataRow(null, "name", null, "name", "")]
    [DataRow(null, null, "word", "", "word")]
    public void ProxyCredentialsOf_ProxyCredentialOptions_SplitsUserPasswordAtTheFirstColonAndLetsTheOthersWin(string? userPassword, string? userName, string? password, string expectedUser, string expectedPassword)
    {
        EasyAuthenticationOptions authentication = new() { ProxyUserPassword = userPassword, ProxyUserName = userName, ProxyPassword = password };

        NetworkCredential? credential = ManagedEasyTransfer.ProxyCredentialsOf(authentication);

        Assert.AreEqual(expectedUser, credential?.UserName);
        Assert.AreEqual(expectedPassword, credential?.Password);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectOnlyWithHttpProxyInTheEnvironment_DialsTheTargetThroughTheEnvironmentsProxy()
    {
        TargetRecordingConnector connector = new(ConnectResult.Connected(new ControlledConnection()));
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"), new Dictionary<string, string> { ["http_proxy"] = "http://envproxy:8080" });
        easy.CreateConnectOnlyConnector = (_, _) => connector;
        easy.Options = ConnectOnlyTo("http://h:8080/x");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(new ConnectTarget("h", 8080, false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "envproxy", 8080, null) }, connector.Targets.Single());
    }
}
