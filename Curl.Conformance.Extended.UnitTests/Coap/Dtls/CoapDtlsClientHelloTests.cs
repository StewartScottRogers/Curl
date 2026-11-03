using System.Buffers.Binary;
using System.Text;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 6347 section 4.2.1 and RFC 5246 section 7.4.1.2: what curl's ClientHello offers, and what the second one repeats.</summary>
[TestClass]
public sealed class CoapDtlsClientHelloTests
{
    private const int HandshakeHeaderLength = 12;
    private const int RandomLength = 32;

    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.1-2")]
    public Task SecondClientHello_RepeatsTheFirstsVersionRandomSessionCipherSuitesAndCompressionAndAddsOnlyTheCookie() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.1-2"], async () =>
        {
            using CoapDtlsFaultRun run = new(DtlsReferenceFault.None);

            CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            CollectionAssert.AreEqual(WithoutCookie(run.Server.Received[0].Fragment), WithoutCookie(run.Server.Received[1].Fragment));
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.1-3")]
    [SpecRequirement("Coap:RFC5246-7.4.1.2-1")]
    public Task ClientHello_OffersDtls12TheServersCipherSuiteAndTheNullCompressionMethod() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.1-3", "Coap:RFC5246-7.4.1.2-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(DtlsReferenceFault.None);

            CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            ReadOnlySpan<byte> body = run.Server.Received[1].Fragment.AsSpan(HandshakeHeaderLength);
            Assert.AreEqual(DtlsReferenceRecord.Dtls12Version, BinaryPrimitives.ReadUInt16BigEndian(body));
            int at = SkipVector8(body, SkipVector8(body, 2 + RandomLength));
            int cipherSuitesLength = BinaryPrimitives.ReadUInt16BigEndian(body[at..]);
            List<ushort> cipherSuites = [];
            for (int offset = at + 2; offset < at + 2 + cipherSuitesLength; offset += 2)
            {
                cipherSuites.Add(BinaryPrimitives.ReadUInt16BigEndian(body[offset..]));
            }

            CollectionAssert.Contains(cipherSuites, DtlsReferenceServer.CipherSuite);
            at += 2 + cipherSuitesLength;
            CollectionAssert.Contains(body.Slice(at + 1, body[at]).ToArray(), (byte)0);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC6347-4.2.2-2")]
    [SpecRequirement("Coap:RFC6347-4.2.5-1")]
    public Task PlainHandshakeMessages_AreNumberedFrom0AndChangeCipherSpecIsTheUnnumberedByte1() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC6347-4.2.2-2", "Coap:RFC6347-4.2.5-1"], async () =>
        {
            using CoapDtlsFaultRun run = new(DtlsReferenceFault.None);

            CurlRunResult result = await CoapDtlsRun.RunAsync(run.Server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            IReadOnlyList<DtlsReferenceRecord> received = run.Server.Received;
            CollectionAssert.AreEqual(
                new ushort[] { 0, 1, 2 },
                received.Take(3).Select(record => BinaryPrimitives.ReadUInt16BigEndian(record.Fragment.AsSpan(4))).ToArray());
            Assert.AreEqual(DtlsReferenceRecord.ChangeCipherSpec, received[3].ContentType);
            CollectionAssert.AreEqual(new byte[] { 1 }, received[3].Fragment);
        });

    /// <summary>The ClientHello's body with its cookie vector removed.</summary>
    private static byte[] WithoutCookie(byte[] message)
    {
        byte[] body = message[HandshakeHeaderLength..];
        int cookieAt = SkipVector8(body, 2 + RandomLength);
        return [.. body[..cookieAt], .. body[SkipVector8(body, cookieAt)..]];
    }

    private static int SkipVector8(ReadOnlySpan<byte> body, int at) => at + 1 + body[at];

}
