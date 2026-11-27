using System.Security.Authentication;
using System.Security.Cryptography;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins what the provider reports when .NET on Linux throws from the chain build
/// <see cref="System.Net.Security.SslStream" /> makes inside its handshake, for OpenSSL's error 1
/// (ADR-1452, BL-6063). The handshake step is replaced by one that throws, so every platform runs it.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    private const string UnspecifiedBuildMessage = "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'.";

    private const string UnspecifiedRefusal = "SSL certificate OpenSSL verify result: unspecified certificate verification error (1)";

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HandshakeThrowingForOpenSslError1InTheOpenSslBuild_IsExit60WithVerifyResult1()
    {
        var events = new RecordingTransferEvents();

        var result = await ThrowingHandshakeAsync(
            new TlsClientOptions(), OpenSslBuild, new CryptographicException(UnspecifiedBuildMessage), events);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(UnspecifiedRefusal, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (1L, false) }, events.VerifyResults);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsTrue(handshake.Failed);
        Assert.IsFalse(handshake.CertificateVerified);
        Assert.AreEqual(1L, handshake.CertificateVerifyResult);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HandshakeThrowingForOpenSslError1WrappedInTheOpenSslBuild_IsExit60()
    {
        var failure = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new IOException("The handshake failed.", new CryptographicException(UnspecifiedBuildMessage)));

        var result = await ThrowingHandshakeAsync(new TlsClientOptions(), OpenSslBuild, failure, new RecordingTransferEvents());

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(UnspecifiedRefusal, result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HandshakeThrowingForOpenSslError1UnderInsecure_StaysAConnectErrorWithVerifyResult1()
    {
        var failure = new CryptographicException(UnspecifiedBuildMessage);
        var events = new RecordingTransferEvents();

        // A pin no key matches: with no certificate to check, it is not what is reported.
        var result = await ThrowingHandshakeAsync(
            new TlsClientOptions(Insecure: true, PinnedPublicKey: "sha256//AAAA"), OpenSslBuild, failure, events);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.OpenSslSslConnectError(failure), result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { (1L, false) }, events.VerifyResults);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HandshakeThrowingForAnotherOpenSslErrorInTheOpenSslBuild_IsAConnectError()
    {
        var failure = new CryptographicException("OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '12'.");
        var events = new RecordingTransferEvents();

        var result = await ThrowingHandshakeAsync(new TlsClientOptions(), OpenSslBuild, failure, events);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_HandshakeThrowingForOpenSslError1InTheSchannelBuild_IsAConnectError()
    {
        var events = new RecordingTransferEvents();

        var result = await ThrowingHandshakeAsync(
            new TlsClientOptions(), SchannelBuild, new CryptographicException(UnspecifiedBuildMessage), events);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.IsEmpty(events.VerifyResults);
    }

    [TestMethod]
    public async Task AsksForRedial_AfterOpenSslError1UnderInsecure_RunsThatHostOnTheHandBuiltClientWithTheSameSettings()
    {
        var failure = new CryptographicException(UnspecifiedBuildMessage);
        var provider = ThrowingProvider(new TlsClientOptions(Insecure: true), OpenSslBuild, failure);
        var events = new RecordingTransferEvents();
        ITlsProviderWithHandBuiltRedial redialing = provider;
        Assert.AreSame(provider, redialing.ProviderFor(CertificateHost));

        var result = await AuthenticateThrowingAsync(provider, events);

        Diagnostics.Assert("asks for a redial", true, redialing.AsksForRedial(result));
        Assert.IsTrue(redialing.AsksForRedial(result));
        Assert.IsFalse(redialing.AsksForRedial(ConnectResult.Failed(CurlExitCode.SslConnectError, result.ErrorMessage!)));
        var handBuilt = Assert.IsInstanceOfType<HandBuiltTlsProvider>(redialing.ProviderFor(CertificateHost));
        Assert.IsTrue(handBuilt.Options.Insecure);
        Assert.AreSame(provider, redialing.ProviderFor("another.example"));
        var systemStoreOnly = (ITlsProviderWithHandBuiltRedial)provider.WithSystemStoreOnlyTrust();
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(systemStoreOnly.ProviderFor(CertificateHost));
        Assert.IsEmpty(events.Handshakes);
    }

    [TestMethod]
    public async Task AsksForRedial_AfterOpenSslError1WithoutInsecure_IsFalseAndKeepsTheProvider()
    {
        var failure = new CryptographicException(UnspecifiedBuildMessage);
        var provider = ThrowingProvider(new TlsClientOptions(), OpenSslBuild, failure);
        ITlsProviderWithHandBuiltRedial redialing = provider;

        var result = await AuthenticateThrowingAsync(provider, new RecordingTransferEvents());

        Diagnostics.Assert("asks for a redial", false, redialing.AsksForRedial(result));
        Assert.IsFalse(redialing.AsksForRedial(result));
        Assert.AreSame(provider, redialing.ProviderFor(CertificateHost));
    }

    private async Task<ConnectResult> ThrowingHandshakeAsync(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        Exception failure,
        RecordingTransferEvents events) =>
        await AuthenticateThrowingAsync(ThrowingProvider(options, matchesSchannelBuild, failure), events);

    private SslStreamTlsProvider ThrowingProvider(TlsClientOptions options, bool matchesSchannelBuild, Exception failure)
    {
        ArrangeOptions(options);
        Diagnostics.Arrange("handshake throws", failure.Message);
        return new SslStreamTlsProvider(options, matchesSchannelBuild)
        {
            AuthenticateSslStreamAsClientAsync = (_, _, _) => Task.FromException(failure),
        };
    }

    private async Task<ConnectResult> AuthenticateThrowingAsync(SslStreamTlsProvider provider, RecordingTransferEvents events)
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        ActResult(result);
        return result;
    }
}
