using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.RequestResponse;

/// <summary>RFC 7252 section 3.1 (option format) in the requests curl sends: options in order, with the 13 and 14 extended delta and length encodings.</summary>
[TestClass]
public sealed class CoapOptionEncodingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-3.1-5")]
    [SpecRequirement("Coap:RFC7252-3.1-6")]
    [SpecRequirement("Coap:RFC7252-3.1-7")]
    public Task LongUriPathSegments_AreSentInOptionOrderWithTheExtendedLengthEncodings() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-3.1-5", "Coap:RFC7252-3.1-6", "Coap:RFC7252-3.1-7"], async () =>
        {
            string shortSegment = new('a', 20);
            string longSegment = new('b', 300);

            CoapReceivedDatagram sent = await SentRequestAsync($"coap://localhost/{shortSegment}/{longSegment}?q=1");

            byte[] expected =
            [
                0x39, .. "localhost"u8,
                0x8D, 20 - 13, .. Encoding.ASCII.GetBytes(shortSegment),
                0x0E, 0, 300 - 269, .. Encoding.ASCII.GetBytes(longSegment),
                0x43, .. "q=1"u8,
            ];
            CollectionAssert.AreEqual(expected, sent.Bytes[(4 + sent.Message.Token.Length)..]);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-3.1-3")]
    public Task UriQueryAsTheFirstOption_IsSentWithTheExtended13Delta() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-3.1-3"], async () =>
        {
            CoapReceivedDatagram sent = await SentRequestAsync("coap://127.0.0.1/?q=1");

            CollectionAssert.AreEqual(new byte[] { 0xD3, 15 - 13, (byte)'q', (byte)'=', (byte)'1' }, sent.Bytes[(4 + sent.Message.Token.Length)..]);
        });

    private async Task<CoapReceivedDatagram> SentRequestAsync(string url)
    {
        TimerJumpingClock clock = new();
        CoapReferenceServer server = CoapMessagingRun.Server(clock, (request, _) => [CoapResponses.Piggybacked(request, CoapResponses.Content, "ok"u8.ToArray())]);

        CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, url);

        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual("ok", Encoding.UTF8.GetString(result.StandardOutput));
        Assert.IsEmpty(result.StandardError);
        return server.Received.Single();
    }
}
