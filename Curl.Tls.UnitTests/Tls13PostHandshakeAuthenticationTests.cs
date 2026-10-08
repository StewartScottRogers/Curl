using System.Security.Cryptography;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ARequestAfterTheHandshakeIsAnsweredWithTheClientCertificateBetweenApplicationData()
    {
        TestServerCredential clientCredential = TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, TlsSignatureScheme.EcdsaSecp256r1Sha256);
        Tls13TestServer testServer = new(TestServerCredential.Ed25519());
        Diagnostics.Arrange("client certificate", "ECDSA P-256, post_handshake_auth offered");
        Diagnostics.Arrange("server", "Ed25519, request context 01 02 03 04 between \"before\" and \"after\"");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(testServer, OfferingSettings with
            {
                ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
            });
        }

        await using Tls13ClientStream stream = client;
        byte[] context = [1, 2, 3, 4];

        byte[] before;
        byte[] after;
        byte[] reply;
        using (Diagnostics.Phase("post-handshake authentication"))
        {
            await server.SendAsync(TlsContentType.ApplicationData, "before"u8.ToArray());
            byte[] request = await server.SendCertificateRequestAsync(context);
            await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
            before = await ReadAsync(stream, 6);
            after = await ReadAsync(stream, 5);
            await server.ReceiveCertificateRequestAnswerAsync(request, context);
            await stream.WriteAsync("reply"u8.ToArray());
            reply = await server.ReceiveApplicationDataAsync(5);
        }

        Diagnostics.Act("client certificates the server received", testServer.ClientCertificates.Count);
        Diagnostics.Act("certificate requested and sent", $"{stream.Handshake.ClientCertificateRequested}, {stream.Handshake.ClientCertificateSent}");

        Diagnostics.Diff("before", "before"u8.ToArray(), before);
        Diagnostics.Diff("after", "after"u8.ToArray(), after);
        Diagnostics.Diff("reply", "reply"u8.ToArray(), reply);
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
        Diagnostics.Arrange("client", "no certificate, post_handshake_auth offered; request context 09");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(testServer, OfferingSettings);
        }

        await using Tls13ClientStream stream = client;
        byte[] context = [9];

        byte[] after;
        byte[] reply;
        using (Diagnostics.Phase("post-handshake authentication"))
        {
            byte[] request = await server.SendCertificateRequestAsync(context);
            await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
            after = await ReadAsync(stream, 5);
            await server.ReceiveCertificateRequestAnswerAsync(request, context);
            await stream.WriteAsync("reply"u8.ToArray());
            reply = await server.ReceiveApplicationDataAsync(5);
        }

        Diagnostics.Act("client certificates the server received", testServer.ClientCertificates.Count);
        Diagnostics.Act("certificate requested and sent", $"{stream.Handshake.ClientCertificateRequested}, {stream.Handshake.ClientCertificateSent}");

        Diagnostics.Diff("after", "after"u8.ToArray(), after);
        Diagnostics.Diff("reply", "reply"u8.ToArray(), reply);
        CollectionAssert.AreEqual("after"u8.ToArray(), after);
        CollectionAssert.AreEqual("reply"u8.ToArray(), reply);
        Assert.IsEmpty(testServer.ClientCertificates);
        Assert.IsTrue(stream.Handshake.ClientCertificateRequested);
        Assert.IsFalse(stream.Handshake.ClientCertificateSent);
    }

    [TestMethod]
    public async Task EachOfTwoRequestsIsAnsweredOverTheHandshakeTranscriptAndItsOwnContext()
    {
        Diagnostics.Arrange("requests", "contexts 01 and 02 02, both before \"after\"");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(settings: OfferingSettings);
        }

        await using Tls13ClientStream stream = client;

        byte[] after;
        using (Diagnostics.Phase("post-handshake authentication"))
        {
            byte[] first = await server.SendCertificateRequestAsync([1]);
            byte[] second = await server.SendCertificateRequestAsync([2, 2]);
            await server.SendAsync(TlsContentType.ApplicationData, "after"u8.ToArray());
            after = await ReadAsync(stream, 5);
            await server.ReceiveCertificateRequestAnswerAsync(first, [1]);
            await server.ReceiveCertificateRequestAnswerAsync(second, [2, 2]);
        }

        Diagnostics.Bytes("after", after);
        Diagnostics.Act("both answers verified", true);

        Diagnostics.Diff("after", "after"u8.ToArray(), after);
        CollectionAssert.AreEqual("after"u8.ToArray(), after);
    }

    [TestMethod]
    public async Task TheFinishedIsKeyedFromTheClientSecretInForceAfterAKeyUpdate()
    {
        TestServerCredential clientCredential = TestServerCredential.Ed25519();
        Diagnostics.Arrange("client certificate", "Ed25519; key update before request context 07");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(settings: OfferingSettings with
            {
                ClientCertificate = new TlsClientCertificate([clientCredential.Certificate], clientCredential.SigningKey),
            });
        }

        await using Tls13ClientStream stream = client;

        Tls13RecordContent keyUpdate;
        using (Diagnostics.Phase("key update and post-handshake authentication"))
        {
            await stream.UpdateKeysAsync(requestServerUpdate: false);
            keyUpdate = (await server.ReceiveAsync())!;
            server.UpdateReadKeys();
            byte[] request = await server.SendCertificateRequestAsync([7]);
            await server.SendAsync(TlsContentType.ApplicationData, RandomNumberGenerator.GetBytes(3));
            await ReadAsync(stream, 3);
            await server.ReceiveCertificateRequestAnswerAsync(request, [7]);
        }

        Diagnostics.Act("key update record type", keyUpdate.Type);
        Diagnostics.Act("certificate sent", stream.Handshake.ClientCertificateSent);

        Diagnostics.Assert("key update record type", TlsContentType.Handshake, keyUpdate.Type);
        Assert.AreEqual(TlsContentType.Handshake, keyUpdate.Type);
        Assert.IsTrue(stream.Handshake.ClientCertificateSent);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithoutTheExtensionOfferedIsAnUnexpectedMessage()
    {
        Diagnostics.Arrange("client", "post_handshake_auth not offered; request context 01");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync();
        }

        await using Tls13ClientStream stream = client;

        await server.SendCertificateRequestAsync([1]);
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription serverAlert = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert", failure.Alert);
        Diagnostics.Act("alert the server received", serverAlert);

        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, failure.Alert);
        Assert.IsFalse(stream.Handshake.SentClientHello!.Extensions.Any(extension => extension.Type == TlsExtensionType.PostHandshakeAuth));
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, failure.Alert);
        Assert.IsFalse(failure.IsFromServer);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, serverAlert);
        Assert.IsFalse(stream.Handshake.ClientCertificateRequested);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithAnEmptyContextIsAnIllegalParameter()
    {
        Diagnostics.Arrange("request context", "empty");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(settings: OfferingSettings);
        }

        await using Tls13ClientStream stream = client;

        await server.SendCertificateRequestAsync([]);
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription serverAlert = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert", failure.Alert);
        Diagnostics.Act("alert the server received", serverAlert);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, serverAlert);
    }

    [TestMethod]
    public async Task ARequestAfterTheHandshakeWithoutSignatureAlgorithmsIsAMissingExtension()
    {
        byte[] request = new CertificateRequest([1], []).Encode();
        Diagnostics.Bytes("certificate request", request);
        Diagnostics.Arrange("request extensions", "none");
        Tls13ClientStream client;
        Tls13RecordTestServer server;
        using (Diagnostics.Phase("handshake"))
        {
            (client, server, _) = await ConnectAsync(settings: OfferingSettings);
        }

        await using Tls13ClientStream stream = client;

        await server.SendAsync(TlsContentType.Handshake, request);
        TlsAlertException failure = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => ReadOnceAsync(stream));
        TlsAlertDescription serverAlert = await ReceiveFatalAlertAsync(server);
        Diagnostics.Act("alert", failure.Alert);
        Diagnostics.Act("alert the server received", serverAlert);

        Diagnostics.Assert("alert", TlsAlertDescription.MissingExtension, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.MissingExtension, failure.Alert);
        Assert.AreEqual(TlsAlertDescription.MissingExtension, serverAlert);
    }

    private static async Task<int> ReadOnceAsync(Stream stream) => await stream.ReadAsync(new byte[100]);
}
