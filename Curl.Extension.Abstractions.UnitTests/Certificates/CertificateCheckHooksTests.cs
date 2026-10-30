using System.Net;

namespace Curl.Extension.Abstractions.Certificates;

[TestClass]
public sealed class CertificateCheckHooksTests
{
    private static readonly ExtensionOption Dane = new("example-dane", ExtensionOptionArgument.None, "Check the chain against TLSA records");

    private static readonly ExtensionOption Ct = new("example-ct", ExtensionOptionArgument.None, "Require Certificate Transparency");

    private static readonly IReadOnlyList<ReadOnlyMemory<byte>> Chain = [new byte[] { 0x30, 0x01 }, new byte[] { 0x30, 0x02 }];

    private static readonly ConnectTarget Target = new("example.com", 443, UseTls: true);

    [TestMethod]
    public void ActivatedBy_WithNoExtensionOptionGiven_GivesCurlsConnectorAndTlsProviderBack()
    {
        FixedConnector connector = new(ConnectResult.Connected(new TrackedConnection()));
        FixedTlsProvider tlsProvider = new(ConnectResult.Connected(new TrackedConnection()));

        CertificateCheckHooks hooks = CertificateCheckHooks.ActivatedBy(ExtensionOptionValues.None, [ExampleHook.ActivatedBy(Dane, new ExampleHook(Dane, null))]);

        Assert.IsEmpty(hooks.Hooks);
        Assert.AreSame(connector, hooks.ConnectorOver(connector));
        Assert.AreSame(tlsProvider, hooks.TlsProviderOver(tlsProvider));
        Assert.IsEmpty(CertificateCheckHooks.None.Hooks);
    }

    [TestMethod]
    public void ActivatedBy_WithTwoOptionsGiven_KeepsBothHooksInTheActivatorsOrder()
    {
        ExampleHook dane = new(Dane, null);
        ExampleHook ct = new(Ct, null);

        CertificateCheckHooks hooks = CertificateCheckHooks.ActivatedBy(ValuesOf(Ct, Dane), [ExampleHook.ActivatedBy(Dane, dane), ExampleHook.ActivatedBy(Ct, ct)]);

        CollectionAssert.AreEqual(new ICertificateCheckHook[] { dane, ct }, hooks.Hooks.ToArray());
    }

    [TestMethod]
    public async Task ConnectorOver_WithAHookRefusingAChainCurlAccepted_FailsWithExit60AndDisposesTheConnection()
    {
        TrackedConnection connection = new();
        ExampleHook dane = new(Dane, "SSL: no TLSA record matches the server certificate");
        IConnector checking = HooksOf(dane).ConnectorOver(new FixedConnector(ConnectResult.Connected(connection, null, peerCertificates: Chain)));

        ConnectResult result = await checking.ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual("SSL: no TLSA record matches the server certificate", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(connection.IsDisposed);
        Assert.AreSame(Chain, dane.GivenChain);
        Assert.AreEqual("example.com", dane.GivenHost);
        Assert.AreEqual(Target.Port, dane.GivenPort);
    }

    [TestMethod]
    public async Task ConnectorOver_WithHooksLettingTheChainStand_GivesCurlsResultBack()
    {
        ConnectResult accepted = ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain);
        ExampleHook dane = new(Dane, null);
        ExampleHook ct = new(Ct, null);

        ConnectResult result = await HooksOf(dane, ct).ConnectorOver(new FixedConnector(accepted)).ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(accepted, result);
        Assert.AreSame(Chain, ct.GivenChain);
    }

    [TestMethod]
    public async Task ConnectorOver_WithTheFirstHookRefusing_RunsNoLaterHook()
    {
        ExampleHook ct = new(Ct, null);

        ConnectResult result = await HooksOf(new ExampleHook(Dane, "refused"), ct).ConnectorOver(new FixedConnector(ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain))).ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsNull(ct.GivenChain);
    }

    [TestMethod]
    public async Task ConnectorOver_WithAChainCurlRefused_GivesCurlsRefusalBackWithoutAskingTheHooks()
    {
        ConnectResult refused = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "SSL certificate problem: unable to get local issuer certificate");
        ExampleHook accepting = new(Dane, null);

        ConnectResult result = await HooksOf(accepting).ConnectorOver(new FixedConnector(refused)).ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(refused, result);
        Assert.IsNull(accepting.GivenChain);
    }

    [TestMethod]
    public async Task ConnectorOver_WithAConnectionWithoutTls_ChecksNothing()
    {
        ConnectResult plain = ConnectResult.Connected(new TrackedConnection());
        ExampleHook refusing = new(Dane, "refused");

        ConnectResult result = await HooksOf(refusing).ConnectorOver(new FixedConnector(plain)).ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(plain, result);
        Assert.IsNull(refusing.GivenChain);
    }

    [TestMethod]
    public async Task ConnectorOver_WithAReusedConnection_ChecksNothingAgain()
    {
        ConnectResult reused = ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain, isReused: true);

        ConnectResult result = await HooksOf(new ExampleHook(Dane, "refused")).ConnectorOver(new FixedConnector(reused)).ConnectAsync(Target, CancellationToken.None);

        Assert.AreSame(reused, result);
    }

    [TestMethod]
    public async Task ConnectorOver_ForAMultiplexedSession_ChecksItsChain()
    {
        IConnector checking = HooksOf(new ExampleHook(Dane, "refused")).ConnectorOver(new FixedConnector(ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain)));

        ConnectResult result = await checking.ConnectMultiplexedSessionAsync(Target, _ => new TrackedConnection(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectorOver_ForAMultiplexedConnect_GivesCurlsResultBack()
    {
        MultiplexedConnectResult failed = MultiplexedConnectResult.Failed(CurlExitCode.CouldntConnect, "no QUIC");

        MultiplexedConnectResult result = await HooksOf(new ExampleHook(Dane, "refused")).ConnectorOver(new FixedConnector(ConnectResult.Connected(new TrackedConnection()), failed)).ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreSame(failed, result);
    }

    [TestMethod]
    public void TlsProviderOver_VerifyServerCertificateChain_ReturnsTheInnerProvidersVerdict()
    {
        ITlsProvider checking = HooksOf(new ExampleHook(Dane, null)).TlsProviderOver(new FixedTlsProvider(ConnectResult.Connected(new TrackedConnection())));

        ServerCertificateChainVerdict verdict = checking.VerifyServerCertificateChain([new byte[] { 1 }], "mail.example.com");

        Assert.AreSame(FixedTlsProvider.ChainVerdict, verdict);
    }

    [TestMethod]
    public async Task TlsProviderOver_WithAHookRefusingAnUpgradeCurlAccepted_FailsWithExit60()
    {
        TrackedConnection connection = new();
        ExampleHook dane = new(Dane, "refused");
        ITlsProvider checking = HooksOf(dane).TlsProviderOver(new FixedTlsProvider(ConnectResult.Connected(connection)));

        ConnectResult result = await checking.AuthenticateAsClientAsync(new TrackedConnection(), "mail.example.com", CancellationToken.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsTrue(connection.IsDisposed);
        Assert.IsEmpty(dane.GivenChain!);
        Assert.AreEqual("mail.example.com", dane.GivenHost);
        Assert.IsNull(dane.GivenPort);
    }

    [TestMethod]
    public async Task TlsProviderOver_UpgradingAConnectionTheCheckingConnectorOpened_GivesTheCheckThatConnectsPort()
    {
        TrackedConnection plaintext = new();
        ExampleHook dane = new(Dane, null);
        CertificateCheckHooks hooks = HooksOf(dane);
        await hooks.ConnectorOver(new FixedConnector(ConnectResult.Connected(plaintext))).ConnectAsync(new ConnectTarget("mail.example.com", 587, UseTls: false), CancellationToken.None);

        await hooks.TlsProviderOver(new FixedTlsProvider(ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain))).AuthenticateAsClientAsync(plaintext, "mail.example.com", NoTransferEvents.Instance, CancellationToken.None);

        Assert.AreEqual(587, dane.GivenPort);
    }

    [TestMethod]
    public async Task TlsProviderOver_WithEvents_ChecksTheUpgradeAndKeepsAnAcceptedOne()
    {
        ConnectResult accepted = ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain);
        FixedTlsProvider curls = new(accepted);

        ConnectResult result = await HooksOf(new ExampleHook(Dane, null)).TlsProviderOver(curls).AuthenticateAsClientAsync(new TrackedConnection(), "mail.example.com", NoTransferEvents.Instance, CancellationToken.None);

        Assert.AreSame(accepted, result);
        Assert.IsTrue(curls.GivenEvents);
    }

    [TestMethod]
    public async Task TlsProviderOver_WithApplicationProtocols_OffersThemThroughCurlsProviderAndChecksTheUpgrade()
    {
        string[] h2ThenHttp11 = ["h2", "http/1.1"];
        FixedTlsProvider curls = new(ConnectResult.Connected(new TrackedConnection(), null, peerCertificates: Chain));

        ConnectResult result = await HooksOf(new ExampleHook(Dane, "DANE: no TLSA record matches")).TlsProviderOver(curls).AuthenticateAsClientAsync(new TrackedConnection(), "mail.example.com", NoTransferEvents.Instance, h2ThenHttp11, CancellationToken.None);

        Assert.AreSame(h2ThenHttp11, curls.GivenApplicationProtocols);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
    }

    [TestMethod]
    public async Task TlsProviderOver_WithAnUpgradeCurlRefused_GivesCurlsRefusalBack()
    {
        ConnectResult refused = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "SSL certificate problem: self-signed certificate");

        ConnectResult result = await HooksOf(new ExampleHook(Dane, null)).TlsProviderOver(new FixedTlsProvider(refused)).AuthenticateAsClientAsync(new TrackedConnection(), "mail.example.com", CancellationToken.None);

        Assert.AreSame(refused, result);
    }

    private static CertificateCheckHooks HooksOf(params ExampleHook[] hooks) =>
        CertificateCheckHooks.ActivatedBy(ValuesOf([.. hooks.Select(hook => hook.Option)]), [.. hooks.Select(hook => ExampleHook.ActivatedBy(hook.Option, hook))]);

    private static ExtensionOptionValues ValuesOf(params ExtensionOption[] options) =>
        new(options.ToDictionary(option => option.Name, _ => ""));

    private sealed class ExampleHook(ExtensionOption option, string? refusal) : ICertificateCheckHook
    {
        public ExtensionOption Option => option;

        public IReadOnlyList<ReadOnlyMemory<byte>>? GivenChain { get; private set; }

        public string? GivenHost { get; private set; }

        public int? GivenPort { get; private set; }

        public static Func<ExtensionOptionValues, ICertificateCheckHook?> ActivatedBy(ExtensionOption option, ExampleHook hook) =>
            values => values.IsGiven(option) ? hook : null;

        public ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, int? port, CancellationToken cancellationToken)
        {
            GivenChain = peerCertificates;
            GivenHost = host;
            GivenPort = port;
            return ValueTask.FromResult(refusal);
        }
    }

    private sealed class FixedConnector(ConnectResult result, MultiplexedConnectResult? multiplexed = null) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(result);

        public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(multiplexed!);

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) => ValueTask.FromResult(result);
    }

    private sealed class FixedTlsProvider(ConnectResult result) : ITlsProvider
    {
        public static ServerCertificateChainVerdict ChainVerdict { get; } = ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, "inner verdict");

        public ServerCertificateChainVerdict VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost) => ChainVerdict;

        public bool GivenEvents { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) => ValueTask.FromResult(result);

        public IReadOnlyList<string>? GivenApplicationProtocols { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, CancellationToken cancellationToken)
        {
            GivenEvents = true;
            return ValueTask.FromResult(result);
        }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken)
        {
            GivenApplicationProtocols = applicationProtocols;
            return AuthenticateAsClientAsync(plaintext, targetHost, events, cancellationToken);
        }
    }

    private sealed class TrackedConnection : IConnection
    {
        public bool IsDisposed { get; private set; }

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
