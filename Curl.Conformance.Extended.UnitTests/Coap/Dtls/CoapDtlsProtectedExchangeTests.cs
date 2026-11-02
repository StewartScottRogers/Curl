using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>RFC 7252 section 9.1: the CoAP exchange of <c>coaps://</c> runs one message per protected application data record.</summary>
[TestClass]
public sealed class CoapDtlsProtectedExchangeTests
{
    [TestMethod]
    [SpecRequirement("Coap:RFC7252-9.1-4")]
    public Task Get_TheRequestAndResponseAreEachOneProtectedRecordAndNeverCrossInTheClear() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-9.1-4"], async () =>
        {
            using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
            CoapReferenceServer coap = CoapDtlsRun.CoapServer((request, _) => [CoapResponses.Piggybacked(request, 0x45, "secret payload"u8.ToArray())]);
            using DtlsReferenceServer server = new(coap, certificate);

            CurlRunResult result = await CoapDtlsRun.RunAsync(server, "-k");

            Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
            Assert.AreEqual("secret payload", Encoding.UTF8.GetString(result.StandardOutput));
            Assert.IsEmpty(result.StandardError);
            CoapReferenceMessage request = coap.Received.Single().Message;
            Assert.AreEqual(0x01, request.Code);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(request.Options.Single(option => option.Number == 11).Value));
            DtlsReferenceRecord applicationRecord = server.Received.Single(record => record.ContentType == DtlsReferenceRecord.ApplicationData);
            Assert.AreEqual((ushort)1, applicationRecord.Epoch);
            WireExchange exchange = result.Exchanges.Single();
            Assert.AreEqual(-1, IndexOf(exchange.ClientBytes, "hello"u8.ToArray()));
            Assert.AreEqual(-1, IndexOf(exchange.ServerBytes, "secret payload"u8.ToArray()));
            Assert.AreEqual(DtlsReferenceRecord.ApplicationData, exchange.ServerSent[^1][0]);
        });

    private static int IndexOf(byte[] bytes, byte[] value) => bytes.AsSpan().IndexOf(value);
}
