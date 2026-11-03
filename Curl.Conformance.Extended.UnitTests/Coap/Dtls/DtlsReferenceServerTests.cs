using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>
/// <see cref="DtlsReferenceServer" /> driven by a client written here from RFC 6347, RFC 5246 and RFC 8422, so every
/// state of the server is checked whatever curl's own client does.
/// </summary>
[TestClass]
public sealed class DtlsReferenceServerTests
{
    [TestMethod]
    public void Answer_ARfcClientCompletesTheHandshakeAndExchangesOneProtectedRecordEachWay()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        using DtlsReferenceServer server = new(new ScriptedDatagramServer("pong"u8.ToArray()), certificate);
        byte[] clientRandom = RandomNumberGenerator.GetBytes(32);

        DtlsReferenceRecord verifyRequest = Single(server.Answer(Record(DtlsReferenceRecord.Handshake, 0, ClientHello(0, clientRandom, []))));
        byte[] cookie = verifyRequest.Fragment[15..];
        byte[] secondHello = ClientHello(1, clientRandom, cookie);
        List<DtlsReferenceRecord> flight = [.. server.Answer(Record(DtlsReferenceRecord.Handshake, 1, secondHello)).Select(answer => Single([answer]))];
        List<byte> transcript = [.. secondHello, .. flight.SelectMany(record => record.Fragment)];
        byte[] serverRandom = flight[0].Fragment[14..46];
        byte[] serverKeyExchange = flight[2].Fragment[12..];
        using ECDiffieHellman clientKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using ECDiffieHellman serverKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = serverKeyExchange[5..37], Y = serverKeyExchange[37..69] } });
        ECPoint clientPoint = clientKey.ExportParameters(false).Q;
        byte[] clientKeyExchange = Handshake(16, 2, [65, 4, .. clientPoint.X!, .. clientPoint.Y!]);
        transcript.AddRange(clientKeyExchange);
        byte[] masterSecret = DtlsReferencePrf.Compute(clientKey.DeriveRawSecretAgreement(serverKey.PublicKey), "master secret", [.. clientRandom, .. serverRandom], 48);
        byte[] keyBlock = DtlsReferencePrf.Compute(masterSecret, "key expansion", [.. serverRandom, .. clientRandom], 40);
        DtlsReferenceRecordCipher clientWrite = new(keyBlock[..16], keyBlock[32..36]);
        DtlsReferenceRecordCipher serverWrite = new(keyBlock[16..32], keyBlock[36..40]);
        byte[] clientFinished = Handshake(20, 3, DtlsReferencePrf.Compute(masterSecret, "client finished", SHA256.HashData([.. transcript]), 12));
        transcript.AddRange(clientFinished);

        IReadOnlyList<ReadOnlyMemory<byte>> finish = server.Answer((byte[])[
            .. Record(DtlsReferenceRecord.Handshake, 2, clientKeyExchange),
            .. Record(DtlsReferenceRecord.ChangeCipherSpec, 3, [1]),
            .. clientWrite.Seal(DtlsReferenceRecord.Handshake, 1, 0, clientFinished).Encode()]);
        DtlsReferenceRecord pong = Single(server.Answer(clientWrite.Seal(DtlsReferenceRecord.ApplicationData, 1, 1, "ping"u8.ToArray()).Encode()));

        Assert.AreEqual((byte)3, verifyRequest.Fragment[0]);
        CollectionAssert.AreEqual(server.Cookie, cookie);
        CollectionAssert.AreEqual(new byte[] { 2, 11, 12, 14 }, flight.Select(record => record.Fragment[0]).ToArray());
        Assert.AreEqual(DtlsReferenceServer.CipherSuite, BinaryPrimitives.ReadUInt16BigEndian(flight[0].Fragment.AsSpan(47)));
        Assert.IsTrue(server.ClientFinishedVerified);
        Assert.AreEqual(DtlsReferenceRecord.ChangeCipherSpec, Single([finish[0]]).ContentType);
        byte[] serverFinished = serverWrite.Open(Single([finish[1]]));
        CollectionAssert.AreEqual(DtlsReferencePrf.Compute(masterSecret, "server finished", SHA256.HashData([.. transcript]), 12), serverFinished[12..]);
        Assert.AreEqual((ulong)1, pong.SequenceNumber);
        CollectionAssert.AreEqual("pong"u8.ToArray(), serverWrite.Open(pong));
        Assert.HasCount(2, server.ClientHelloCookies);
        Assert.HasCount(6, server.Received);
    }

    [TestMethod]
    public void Prf_MatchesTheTls12Sha256TestVector()
    {
        byte[] secret = Convert.FromHexString("9bbe436ba940f017b17652849a71db35");
        byte[] seed = Convert.FromHexString("a0ba9f936cda311827a6f796ffd5198c");

        byte[] output = DtlsReferencePrf.Compute(secret, "test label", seed, 100);

        Assert.AreEqual(
            "E3F229BA727BE17B8D122620557CD453C2AAB21D07C3D495329B52D4E61EDB5A6B301791E90D35C9C9A46B4E14BAF9AF0FA022F7077DEF17ABFD3797C0564BAB4FBC91666E9DEF9B97FCE34F796789BAA48082D122EE42C5A72E5A5110FFF70187347B66",
            Convert.ToHexString(output));
    }

    private static DtlsReferenceRecord Single(IReadOnlyList<ReadOnlyMemory<byte>> answers) => DtlsReferenceRecord.Split(answers.Single().Span).Single();

    private static byte[] Record(byte contentType, ulong sequenceNumber, byte[] fragment) => new DtlsReferenceRecord(contentType, 0, sequenceNumber, fragment).Encode();

    private static byte[] ClientHello(ushort sequence, byte[] random, byte[] cookie) =>
        Handshake(1, sequence, [0xFE, 0xFD, .. random, 0, (byte)cookie.Length, .. cookie, 0, 2, 0xC0, 0x2F, 1, 0]);

    private static byte[] Handshake(byte type, ushort sequence, byte[] body)
    {
        byte[] length = [(byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length];
        return [type, .. length, (byte)(sequence >> 8), (byte)sequence, 0, 0, 0, .. length, .. body];
    }
}
