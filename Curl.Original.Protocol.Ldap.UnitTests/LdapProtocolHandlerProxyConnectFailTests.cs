using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins curl 8.22.0 on an <c>ldap</c> URL given a proxy (BL-6087): curl's own connection asks the
/// proxy for a CONNECT tunnel, and when it is refused <c>-f</c> ends exit 22 while without it the
/// connector's exit 7 stands. Recorded with
/// <c>Record-CurlExchange.ps1 -CurlArgs -sS,-v,-p,-f,-x,http://127.0.0.1:P,ldap://example.test/</c>
/// and a <c>403</c> or <c>407</c> reply.
/// </summary>
[TestClass]
public sealed class LdapProtocolHandlerProxyConnectFailTests
{
    private const string Refused = "CONNECT tunnel failed, response 403";

    private static readonly ProxyEndpoint Proxy = new(ProxyKind.Http, "127.0.0.1", 3128, null);

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.Fail, 403, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(LdapDialect.OpenLdap, HttpFailMode.Fail, 403, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 403")]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.Fail, 400, false, CurlExitCode.HttpReturnedError, "The requested URL returned error: 400")]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.Fail, 407, true, CurlExitCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.Fail, 302, false, CurlExitCode.CouldntConnect, "CONNECT tunnel failed, response 302")]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.FailWithBody, 403, false, CurlExitCode.CouldntConnect, Refused)]
    [DataRow(LdapDialect.WinLdap, HttpFailMode.None, 403, false, CurlExitCode.CouldntConnect, Refused)]
    [DataRow(LdapDialect.OpenLdap, HttpFailMode.None, 403, false, CurlExitCode.CouldntConnect, Refused)]
    public async Task ExecuteAsync_ProxyRefusesTheConnect_EndsAsCurlDoesAndClosesTheConnection(
        LdapDialect dialect, HttpFailMode fail, int status, bool authenticationProblem, CurlExitCode expectedExitCode, string expectedMessage)
    {
        var events = new RecordingTransferEvents();
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, $"CONNECT tunnel failed, response {status}", null, 0, status)
            .WithProxyAuthenticationProblem(authenticationProblem));

        TransferResult result = await new LdapProtocolHandler(connector, dialect).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = fail }, events));

        Assert.AreEqual(TransferResult.Failure(expectedExitCode, expectedMessage), result);
        CollectionAssert.AreEqual(new[] { "closing connection #0" }, events.Lines);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreSame(Proxy, target.Proxy);
        Assert.AreEqual(fail == HttpFailMode.Fail, target.FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyRefusesTheConnectWithoutHttpOptions_KeepsTheConnectorsFailure()
    {
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, Refused, null, 0, 403));

        TransferResult result = await new LdapProtocolHandler(connector, LdapDialect.WinLdap).ExecuteAsync(ContextOf(null, new RecordingTransferEvents()));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.CouldntConnect, Refused), result);
        Assert.IsFalse(connector.Targets.Single().FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_WinLdapThroughAProxy_OpensItsOwnConnectionWithoutTheProxy()
    {
        var connector = new FirstConnectOnlyConnector();

        await new LdapProtocolHandler(connector, LdapDialect.WinLdap).ExecuteAsync(ContextOf(new HttpRequestOptions { Fail = HttpFailMode.Fail }, new RecordingTransferEvents()));

        Assert.HasCount(2, connector.Targets);
        Assert.AreSame(Proxy, connector.Targets[0].Proxy);
        Assert.IsNull(connector.Targets[1].Proxy);
        Assert.IsFalse(connector.Targets[1].FailsOnHttpErrorStatus);
    }

    private static TransferContext ContextOf(HttpRequestOptions? http, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse("ldap://example.test/"),
            Output = new MemoryStream(),
            Events = events,
            Http = http,
            Proxy = Proxy,
        };

    /// <summary>An <see cref="IConnector" /> that opens its first connection, fails every later one and records every target.</summary>
    private sealed class FirstConnectOnlyConnector : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(Targets.Count == 1
                ? ConnectResult.Connected(new ScriptedConnection())
                : ConnectResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: example.test"));
        }
    }
}
