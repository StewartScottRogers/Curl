using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.UriAndResponse;

/// <summary>
/// RFC 7252 section 5.9 (response codes) and the code registry of section 12.1 as curl reports them: a 4.xx or 5.xx
/// response is written with exit 0 as an HTTP error status is, ends with exit 22 under <c>-f</c>, and <c>-i</c> writes the
/// response code as a status line.
/// </summary>
[TestClass]
public sealed class CoapResponseCodeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.9-1")]
    [SpecRequirement("Coap:RFC7252-12.1-1")]
    [DataRow((byte)0x84, "not here")]
    [DataRow((byte)0x80, "bad")]
    [DataRow((byte)0xA3, "unavailable")]
    [DataRow((byte)0xA0, "broken")]
    public Task ErrorResponse_WithoutFail_IsWrittenWithExit0(byte code, string payload) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.9-1", "Coap:RFC7252-12.1-1"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await CoapUriAndResponseRun.RunAsync(5683, code, payload, TestContext.CancellationToken, CoapMessagingRun.Url);

            Assert.AreEqual(0, outcome.Result.ExitCode, outcome.StandardError);
            Assert.AreEqual(payload, outcome.StandardOutput);
            Assert.AreEqual(string.Empty, outcome.StandardError);
            Assert.HasCount(1, outcome.Sent);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.9-1")]
    [SpecRequirement("Coap:RFC7252-5.9-2")]
    [DataRow((byte)0x84, "4.04")]
    [DataRow((byte)0x91, "4.17")]
    [DataRow((byte)0xA3, "5.03")]
    [DataRow((byte)0xBE, "5.30")]
    public Task ErrorResponse_WithFail_EndsWithExit22AndWritesNothing(byte code, string reported) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.9-1", "Coap:RFC7252-5.9-2"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await CoapUriAndResponseRun.RunAsync(5683, code, "body", TestContext.CancellationToken, "-f", CoapMessagingRun.Url);

            Assert.AreEqual(22, outcome.Result.ExitCode);
            Assert.AreEqual(string.Empty, outcome.StandardOutput);
            Assert.AreEqual($"curl: (22) The requested URL returned error: {reported}\n", outcome.StandardError.ReplaceLineEndings("\n"));
            Assert.HasCount(1, outcome.Sent);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.9-1")]
    [DataRow((byte)0x45, "2.05")]
    [DataRow((byte)0x44, "2.04")]
    public Task SuccessResponse_WithFail_IsWrittenWithExit0(byte code, string reported) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.9-1"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await CoapUriAndResponseRun.RunAsync(5683, code, "fine", TestContext.CancellationToken, "-f", "-i", CoapMessagingRun.Url);

            Assert.AreEqual(0, outcome.Result.ExitCode, outcome.StandardError);
            Assert.AreEqual($"CoAP/1.0 {reported}\r\n\r\nfine", outcome.StandardOutput);
            Assert.AreEqual(string.Empty, outcome.StandardError);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-12.1-2")]
    [DataRow((byte)0x45, "2.05", "content")]
    [DataRow((byte)0x84, "4.04", "missing")]
    [DataRow((byte)0xA0, "5.00", "fault")]
    public Task IncludeHeaders_WritesTheResponseCodeAsAStatusLineBeforeThePayload(byte code, string reported, string payload) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-12.1-2"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await CoapUriAndResponseRun.RunAsync(5683, code, payload, TestContext.CancellationToken, "-i", CoapMessagingRun.Url);

            Assert.AreEqual(0, outcome.Result.ExitCode, outcome.StandardError);
            Assert.AreEqual($"CoAP/1.0 {reported}\r\n\r\n{payload}", outcome.StandardOutput);
            Assert.AreEqual(string.Empty, outcome.StandardError);
        });
}
