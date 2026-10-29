using System.Net;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins BL-4026 (ADR-0979): a <see cref="ConnectTarget" /> can name the host its TLS handshake sends
/// as SNI and checks the certificate against (<see cref="ConnectTarget.TlsServerName" />), and ask for
/// a handshake trusting only the system store (<see cref="ConnectTarget.TrustsSystemStoreOnly" />), as
/// the second connection curl's WinLDAP opens to an <c>ldaps://</c> server needs (CGR-289).
/// </summary>
[TestClass]
public sealed class ConnectTargetTlsOverrideTests
{
    private static readonly TlsClientOptions TrustBesideTheSystemStore = new(
        Insecure: true,
        CaCertificateFile: "ca.pem",
        CaCertificateDirectory: "certs",
        CaCertificateBlob: new byte[] { 1 },
        PinnedPublicKey: "sha256//pin");

    [TestMethod]
    public void ConnectTarget_ByDefault_NamesNoServerNameAndTrustsAsTheOptionsSay()
    {
        var target = new ConnectTarget("localhost", 636, UseTls: true);

        Assert.IsNull(target.TlsServerName);
        Assert.IsFalse(target.TrustsSystemStoreOnly);
    }

    [TestMethod]
    public async Task ConnectAsync_WithATlsServerName_HandsItToTheProviderInPlaceOfTheHost()
    {
        var tls = new FakeTlsProvider();
        var connector = ConnectorOver(tls);

        _ = await connector.ConnectAsync(new ConnectTarget("localhost", 636, UseTls: true) { TlsServerName = "MACHINE" }, CancellationToken.None);

        Assert.AreEqual("MACHINE", tls.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutATlsServerName_HandsTheProviderTheHost()
    {
        var tls = new FakeTlsProvider();
        var connector = ConnectorOver(tls);

        _ = await connector.ConnectAsync(new ConnectTarget("localhost", 636, UseTls: true), CancellationToken.None);

        Assert.AreEqual("localhost", tls.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_TrustingTheSystemStoreOnly_RunsTheHandshakeThroughTheSystemStoreProvider()
    {
        var tls = new SystemStoreSwitchingTlsProvider();
        var connector = ConnectorOver(tls);

        _ = await connector.ConnectAsync(new ConnectTarget("localhost", 636, UseTls: true) { TrustsSystemStoreOnly = true }, CancellationToken.None);

        Assert.AreEqual(0, tls.Default.HandshakeCount);
        Assert.AreEqual(1, tls.SystemStoreOnly.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectAsync_TrustingAsTheOptionsSay_RunsTheHandshakeThroughTheGivenProvider()
    {
        var tls = new SystemStoreSwitchingTlsProvider();
        var connector = ConnectorOver(tls);

        _ = await connector.ConnectAsync(new ConnectTarget("localhost", 636, UseTls: true), CancellationToken.None);

        Assert.AreEqual(1, tls.Default.HandshakeCount);
        Assert.AreEqual(0, tls.SystemStoreOnly.HandshakeCount);
    }

    [TestMethod]
    public void TrustedAs_AProviderThatCannotTrustOnlyTheSystemStore_IsKept()
    {
        var tls = new FakeTlsProvider();

        var chosen = TcpConnector.TrustedAs(new ConnectTarget("localhost", 636, UseTls: true) { TrustsSystemStoreOnly = true }, tls);

        Assert.AreSame(tls, chosen);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SslStreamTlsProvider_WithSystemStoreOnlyTrust_DropsEveryCaBesideTheSystemStoreAndKeepsInsecure(bool matchesSchannelBuild)
    {
        var provider = new SslStreamTlsProvider(TrustBesideTheSystemStore, matchesSchannelBuild);

        var systemStoreOnly = (SslStreamTlsProvider)provider.WithSystemStoreOnlyTrust();

        AssertTrustsOnlyTheSystemStoreUnlessInsecure(systemStoreOnly.Options);
        Assert.AreEqual(TrustBesideTheSystemStore, provider.Options);
        Assert.IsEmpty(systemStoreOnly.Warnings);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void HandBuiltTlsProvider_WithSystemStoreOnlyTrust_DropsEveryCaBesideTheSystemStoreAndKeepsInsecure(bool matchesSchannelBuild)
    {
        var provider = new HandBuiltTlsProvider(TrustBesideTheSystemStore, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), Curl.Tls.SystemTlsRandomSource.Instance);

        var systemStoreOnly = (HandBuiltTlsProvider)provider.WithSystemStoreOnlyTrust();

        AssertTrustsOnlyTheSystemStoreUnlessInsecure(systemStoreOnly.Options);
        Assert.AreEqual(TrustBesideTheSystemStore, provider.Options);
    }

    private static void AssertTrustsOnlyTheSystemStoreUnlessInsecure(TlsClientOptions options)
    {
        Assert.IsTrue(options.Insecure);
        Assert.IsNull(options.CaCertificateFile);
        Assert.IsNull(options.CaCertificateDirectory);
        Assert.IsNull(options.CaCertificateBlob);
        Assert.AreEqual("sha256//pin", options.PinnedPublicKey);
    }

    private static TcpConnector ConnectorOver(ITlsProvider tls) =>
        new(new FakeDnsResolver(IPAddress.Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, tls, new ManualTimeProvider());

    private sealed class SystemStoreSwitchingTlsProvider : IHandshakeReportingTlsProvider, ISystemStoreTrustTlsProvider
    {
        public FakeTlsProvider Default { get; } = new();

        public FakeTlsProvider SystemStoreOnly { get; } = new();

        public TlsClientRoute Route => TlsClientRoute.SslStream;

        public string? RouteReason => null;

        public ITlsProvider WithSystemStoreOnlyTrust() => SystemStoreOnly;

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
            Default.AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(
            IConnection plaintext,
            string targetHost,
            ITransferEvents events,
            bool isProxy,
            IReadOnlyList<string> applicationProtocols,
            CancellationToken cancellationToken) =>
            Default.AuthenticateAsClientAsync(plaintext, targetHost, events, isProxy, applicationProtocols, cancellationToken);
    }
}
