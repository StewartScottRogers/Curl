using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--ssl-auto-client-cert</c>
/// (<see cref="TlsClientOptions.AutoClientCertificate" />): without <c>--cert</c>, the first
/// qualifying certificate in the user's personal store, which a
/// <see cref="FakeClientCertificateStore" /> stands in for, is presented when the server asks
/// for one, in either build (ADR-0191). curl 8.21.0's Schannel build presented one from the
/// store against <c>openssl s_server -Verify 1</c> (measured, BL-610).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAutoClientCertificate_PresentsTheCertificateFromThePersonalStore(bool matchesSchannelBuild)
    {
        var store = StoreHoldingTheClientCertificate();

        var handshake = await HandshakeWithClientCertificateRequestAsync(
            new TlsClientOptions(Insecure: true, AutoClientCertificate: true), matchesSchannelBuild, store);

        AssertPresented(handshake);
        CollectionAssert.AreEqual(new[] { (ClientCertificateStoreLocation.CurrentUser, "MY") }, store.Opened);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAutoClientCertificateAndAStoreThatCannotOpen_PresentsNone()
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(
            new TlsClientOptions(Insecure: true, AutoClientCertificate: true), SchannelBuild, new FakeClientCertificateStore { Certificates = null });

        Diagnostics.Assert("received certificate", null, handshake.Received);
        Assert.AreEqual(CurlExitCode.Ok, handshake.Result.ExitCode);
        Assert.IsNull(handshake.Received);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutAutoClientCertificate_OpensNoStore()
    {
        var store = StoreHoldingTheClientCertificate();

        var handshake = await HandshakeWithClientCertificateRequestAsync(new TlsClientOptions(Insecure: true), SchannelBuild, store);

        Diagnostics.Assert("stores opened", 0, store.Opened.Count);
        Assert.IsNull(handshake.Received);
        Assert.IsEmpty(store.Opened);
    }

    // --cert names the certificate; the automatic choice is only for a transfer without one.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAutoClientCertificateAndCert_PresentsTheCertCertificate()
    {
        var store = StoreHoldingTheClientCertificate();
        var options = ClientCertificate($@"CurrentUser\MY\{s_clientCertificate.Thumbprint}") with { AutoClientCertificate = true };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild, store);

        AssertPresented(handshake);
        Assert.HasCount(1, store.Opened);
    }
}
