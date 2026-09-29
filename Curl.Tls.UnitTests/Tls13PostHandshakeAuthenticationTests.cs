using System.Security.Cryptography;
using static Curl.Tls.Tls13PipeDriver;

namespace Curl.Tls;

/// <summary>
/// Post-handshake client authentication (RFC 8446 section 4.6.2) over a connected
/// <see cref="Tls13ClientStream" />: with <c>post_handshake_auth</c> offered, a
/// CertificateRequest after the handshake is answered with Certificate, CertificateVerify
/// and Finished the server verifies, or an empty Certificate and Finished; application data
/// flows around it; without the extension offered the request is <c>unexpected_message</c>.
/// </summary>
[TestClass]
public sealed class Tls13PostHandshakeAuthenticationTests
{
    private static readonly Tls13ClientSettings OfferingSettings = DefaultSettings with
    {
        ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.PostHandshakeAuth],
    };

    [TestMethod]
    public async Task ARequestAfterTheHandshakeIsAnsweredWithTheClientCertificateBetweenApplicationData()
    {
        TestServerCredential clientCredential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519());
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(testServer, OfferingSettings with
        {
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        await using Tls13ClientStream stream = client;
        byte[] context = [1, 2, 3, 4];

        await server.SendAsync(TlsContentType.ApplicationData, "before"u8.ToArray());
        byte[] request = await server.SendCertificateRequestAsync(context);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] before = await ReadAsync(stream, 6);
        byte[] after = await ReadAsync(stream, 5);
        await server.ReceiveCertificateRequestAnswerAsync(request, context);
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        Assert.IsTrue(stream.Handshake.SentClientHello!.Extensions.Any(extension => extension.Type == TlsExtensionType.PostHandshakeAuth && extension.Data.Length == 0));
        CollectionAssert.AreEqual("before"u8.ToArray(), before);
        CollectionAssert.AreEqual("after"u8.ToArray(), after);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
        Assert.HasCount(1, testServer.ClientCertificates);
        CollectionAssert.AreEqual(clientCredential.Certificate, testServer.ClientCertificates[0]);
        Assert.IsTrue(stream.Handshake.ClientCertificateRequested);
        Assert.IsTrue(stream.Handshake.ClientCertificateSent);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeIsAnsweredWithAnEmptyCertificateWhenTheClientHasNone()
    {
        Tls13TestServer testServer = new(TestServerCredential.Ed25519());
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(testServer, OfferingSettings);
        await using Tls13ClientStream stream = client;
        byte[] context = [9];

        byte[] request = await server.SendCertificateRequestAsync(context);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] after = await ReadAsync(stream, 5);
        await server.ReceiveCertificateRequestAnswerAsync(request, context);
        await stream.WriteAsync("reply"u8.ToArray());
        byte[] reply = await server.ReceiveApplicationDataAsync(5);

        CollectionAssert.AreEqual("after"u8.ToArray(), after);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
        Assert.IsEmpty(testServer.ClientCertificates);
        Assert.IsTrue(stream.Handshake.ClientCertificateRequested);
        Assert.IsFalse(stream.Handshake.ClientCertificateSent);
    }

    [TestMethod]
    public async Task EachOfTwoRequestsIsAnsweredOverTheHandshakeTranscriptAndItsOwnContext()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(settings: OfferingSettings);
        await using Tls13ClientStream stream = client;

        byte[] first = await server.SendCertificateRequestAsync([1]);
        byte[] second = await server.SendCertificateRequestAsync([2, 2]);
        await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
        byte[] after = await ReadAsync(stream, 5);
        await server.ReceiveCertificateRequestAnswerAsync(first, [1]);
        await server.ReceiveCertificateRequestAnswerAsync(second, [2, 2]);

        CollectionAssert.AreEqual("after"u8.ToArray(), after);
    }

    [TestMethod]
    public async Task TheFinishedIsKeyedFromTheClientSecretInForceAfterAKeyUpdate()
    {
        TestServerCredential clientCredential = TestServerCredential.Ed25519();
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(settings: OfferingSettings with
        {
            ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
        });
        await using Tls13ClientStream stream = client;

        await stream.UpdateKeysAsync(requestServerUpdate: false);
        Tls13RecordContent keyUpdate = (await server.ReceiveAsync())!;
        server.UpdateReadKeys();
        byte[] request = await server.SendCertificateRequestAsync([7]);
        await server.SendAsync(TlsContentType.ApplicationData, RandomNumberGenerator.GetBytes(3));
        await ReadAsync(stream, 3);
        await server.ReceiveCertificateRequestAnswerAsync(request, [7]);

        Assert.AreEqual(TlsContentType.Handshake, keyUpdate.Type);
        Assert.IsTrue(stream.Handshake.ClientCertificateSent);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithoutTheExtensionOfferedIsAnUnexpectedMessage()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync();
        await using Tls13ClientStream stream = client;

        await server.SendCertificateRequestAsync([1]);
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.IsFalse(stream.Handshake.SentClientHello!.Extensions.Any(extension => extension.Type == TlsExtensionType.PostHandshakeAuth));
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, await ReceiveFatalAlertAsync(server));
        Assert.IsFalse(stream.Handshake.ClientCertificateRequested);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithAnEmptyContextIsAnIllegalParameter()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(settings: OfferingSettings);
        await using Tls13ClientStream stream = client;

        await server.SendCertificateRequestAsync([]);
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, await ReceiveFatalAlertAsync(server));
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithoutSignatureAlgorithmsIsAMissingExtension()
    {
        (Tls13ClientStream client, Tls13RecordTestServer server, _) = await ConnectAsync(settings: OfferingSettings);
        await using Tls13ClientStream stream = client;

        await server.SendAsync(TlsContentType.Handshake, new CertificateRequest([1], []).Encode());
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));

        Assert.AreEqual(TlsAlertDescription.MissingExtension, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.MissingExtension, await ReceiveFatalAlertAsync(server));
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);
}
