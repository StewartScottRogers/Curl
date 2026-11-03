using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;

namespace Curl.Conformance.Extended.UnitTests.Coap.RequestResponse;

/// <summary>RFC 7252 sections 5.5 and 5.8 (payloads and methods): each method curl sends, with its request payload, and the response payload it writes.</summary>
[TestClass]
public sealed class CoapRequestMethodTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("Coap:RFC7252-5.8-1")]
    [SpecRequirement("Coap:RFC7252-5.5-1")]
    [DataRow("GET", (byte)0x01, (byte)0x45, "")]
    [DataRow("POST", (byte)0x02, (byte)0x41, "posted")]
    [DataRow("PUT", (byte)0x03, (byte)0x44, "put body")]
    [DataRow("DELETE", (byte)0x04, (byte)0x42, "")]
    public Task Method_IsSentWithItsCodeAndPayloadAndTheResponsePayloadIsWritten(string method, byte code, byte responseCode, string payload) =>
        CoapMessagingRun.CaseAsync(["Coap:RFC7252-5.8-1", "Coap:RFC7252-5.5-1"], async () =>
        {
            string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(file, payload, TestContext.CancellationToken);
            try
            {
                TimerJumpingClock clock = new();
                CoapReferenceServer server = CoapMessagingRun.Server(clock, (request, _) => [CoapResponses.Piggybacked(request, responseCode, Encoding.UTF8.GetBytes("done " + method))]);

                CurlRunResult result = await CoapMessagingRun.RunAsync(server, clock, TestContext.CancellationToken, [.. ArgumentsOf(method, payload, file), CoapMessagingRun.Url]);

                Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
                Assert.AreEqual("done " + method, Encoding.UTF8.GetString(result.StandardOutput));
                Assert.IsEmpty(result.StandardError);
                CoapReferenceMessage sent = server.Received.Single().Message;
                Assert.AreEqual(CoapReferenceMessageType.Confirmable, sent.Type);
                Assert.AreEqual(code, sent.Code);
                CollectionAssert.AreEqual(
                    new[] { "3=localhost", "11=hello" },
                    sent.Options.Select(option => $"{option.Number}={Encoding.UTF8.GetString(option.Value)}").ToArray());
                Assert.AreEqual(payload, Encoding.UTF8.GetString(sent.Payload));
            }
            finally
            {
                File.Delete(file);
            }
        });

    private static string[] ArgumentsOf(string method, string payload, string file) => method switch
    {
        "POST" => ["-d", payload],
        "PUT" => ["-T", file],
        "DELETE" => ["-X", "DELETE"],
        _ => [],
    };
}
