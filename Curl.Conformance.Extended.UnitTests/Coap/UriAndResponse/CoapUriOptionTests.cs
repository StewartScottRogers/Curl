using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.UriAndResponse;

/// <summary>
/// RFC 7252 section 6 (CoAP URIs) and the option numbers of section 12.2: the Uri-Host, Uri-Port, Uri-Path and Uri-Query
/// options curl decomposes a <c>coap</c> URI into (section 6.4), byte for byte.
/// </summary>
[TestClass]
public sealed class CoapUriOptionTests
{
    private static readonly byte[] HostAndHelloPath = [0x39, .. "localhost"u8, 0x85, .. "hello"u8];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-6.3-1")]
    [SpecRequirement("Coap:RFC7252-6.4-1")]
    [SpecRequirement("Coap:RFC7252-6.4-3")]
    [SpecRequirement("Coap:RFC7252-6.4-4")]
    [SpecRequirement("Coap:RFC7252-12.2-1")]
    public Task HostPathSegmentsAndQueryArguments_AreSentAsUriOptionsPercentDecoded() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-6.3-1", "Coap:RFC7252-6.4-1", "Coap:RFC7252-6.4-3", "Coap:RFC7252-6.4-4", "Coap:RFC7252-12.2-1"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await OkRunAsync(5683, "COAP://LocalHost/a%20b/c%2Fd/?x=1&y=%26z");

            byte[] expected =
            [
                0x39, .. "localhost"u8,
                0x83, .. "a b"u8,
                0x03, .. "c/d"u8,
                0x00,
                0x43, .. "x=1"u8,
                0x04, .. "y=&z"u8,
            ];
            CollectionAssert.AreEqual(expected, outcome.OptionBytes);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-6.4-2")]
    [SpecRequirement("Coap:RFC7252-6.1-1")]
    [SpecRequirement("Coap:RFC7252-12.6-1")]
    [DataRow(5683, "coap://localhost/hello")]
    [DataRow(5683, "coap://localhost:5683/hello")]
    [DataRow(5684, "coap://localhost:5684/hello")]
    public Task Port_IsTheDestinationAndNoUriPortIsSent(int port, string url) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-6.4-2", "Coap:RFC7252-6.1-1", "Coap:RFC7252-12.6-1"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await OkRunAsync(port, url);

            CollectionAssert.AreEqual(HostAndHelloPath, outcome.OptionBytes);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-6.4-5")]
    [DataRow("coap://127.0.0.1/hello", new byte[] { 0xB5, (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o' })]
    [DataRow("coap://127.0.0.1", new byte[0])]
    [DataRow("coap://127.0.0.1/", new byte[0])]
    public Task IpLiteralHostAndEmptyPath_SendNoUriHostAndNoUriPath(string url, byte[] expected) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-6.4-5"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await OkRunAsync(5683, url);

            CollectionAssert.AreEqual(expected, outcome.OptionBytes);
        });

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-6.4-6")]
    public Task Fragment_IsNotSent() =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-6.4-6"], async () =>
        {
            CoapUriAndResponseRun.Outcome outcome = await OkRunAsync(5683, "coap://localhost/hello#part");

            CollectionAssert.AreEqual(HostAndHelloPath, outcome.OptionBytes);
        });

    private async Task<CoapUriAndResponseRun.Outcome> OkRunAsync(int port, string url)
    {
        CoapUriAndResponseRun.Outcome outcome = await CoapUriAndResponseRun.RunAsync(port, CoapResponses.Content, "ok", TestContext.CancellationToken, url);

        Assert.AreEqual(0, outcome.Result.ExitCode, outcome.StandardError);
        Assert.AreEqual("ok", outcome.StandardOutput);
        Assert.AreEqual(string.Empty, outcome.StandardError);
        Assert.AreEqual(CoapReferenceMessageType.Confirmable, outcome.Sent.Single().Message.Type);
        return outcome;
    }
}
