using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;
using Curl.Conformance.Extended.Mqtt5.Transports;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams.TransferSurfaceCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// curl's generic transfer options for <c>mqtt5://</c> (port 1883), <c>mqtt5s://</c> (TLS on port 8883, curl run with
/// <c>-k</c>) and <c>mqtt5+ws://</c> (WebSocket on port 80, ADR-1054): each check subscribes to <c>t</c>
/// (<c>mqtt5://localhost/t</c>) on the MQTT 5 reference broker, which answers SUBSCRIBE with a SUBACK granting QoS 0, a
/// PUBLISH of <c>hello</c> and a DISCONNECT, or with a SUBACK refusing the filter (reason code 0x80), and pins exit
/// code, standard output and standard error. Expected values come from curl 8.22.0's manual page and, for a refusing
/// SUBACK, from ADR-1049 (exit 8).
/// </summary>
[TestClass]
public sealed class Mqtt5TransferSurfaceCases
{
    private const string ServerErrorMessage = "MQTT 5 SUBACK reason code 0x80 UnspecifiedError";

    private const string Reply = "hello";

    private static readonly byte[] NormalDisconnect = [0xE0, 0x00];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Mqtt5-1")]
    [SpecRequirement("CurlSurface:Verbose-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:Verbose-Mqtt5Ws-1")]
    [DataRow("mqtt5", "CurlSurface:Verbose-Mqtt5-1")]
    [DataRow("mqtt5s", "CurlSurface:Verbose-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "CurlSurface:Verbose-Mqtt5Ws-1")]
    public Task Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Granted), "-sS", "-v", Url(scheme));

                AssertVerboseRun(result, requirement, "manual: -v", Reply, VerboseLines(scheme));
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Mqtt5-1")]
    [SpecRequirement("CurlSurface:Output-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:Output-Mqtt5Ws-1")]
    [DataRow("mqtt5", "CurlSurface:Output-Mqtt5-1")]
    [DataRow("mqtt5s", "CurlSurface:Output-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "CurlSurface:Output-Mqtt5Ws-1")]
    public Task Output_File_WritesTheMessageToTheFileAndNothingToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    string file = Path.Combine(directory, "message.txt");

                    CurlRunResult result = await RunAsync(Route(scheme, Granted), "-sS", "-o", file, Url(scheme));

                    AssertRun(result, requirement, "manual: -o", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(file), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Mqtt5-1")]
    [SpecRequirement("CurlSurface:RemoteName-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:RemoteName-Mqtt5Ws-1")]
    [DataRow("mqtt5", "CurlSurface:RemoteName-Mqtt5-1")]
    [DataRow("mqtt5s", "CurlSurface:RemoteName-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "CurlSurface:RemoteName-Mqtt5Ws-1")]
    public Task RemoteName_WritesTheMessageToAFileNamedForTheUrlsLastSegment(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    CurlRunResult result = await RunAsync(Route(scheme, Granted), "-sS", "--output-dir", directory, "-O", Url(scheme));

                    AssertRun(result, requirement, "manual: -O", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(Path.Combine(directory, "t")), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5-1")]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5Ws-1")]
    [DataRow("mqtt5", "CurlSurface:WriteOut-Mqtt5-1")]
    [DataRow("mqtt5s", "CurlSurface:WriteOut-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "CurlSurface:WriteOut-Mqtt5Ws-1")]
    public Task WriteOut_SuccessfulSubscribe_WritesEveryVariableAfterTheMessage(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Granted), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(result, requirement, "manual: -w", 0, $"{Reply}0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|{Reply.Length}|0.000000", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5-2")]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5s-2")]
    [SpecRequirement("CurlSurface:WriteOut-Mqtt5Ws-2")]
    [DataRow("mqtt5", "CurlSurface:WriteOut-Mqtt5-2")]
    [DataRow("mqtt5s", "CurlSurface:WriteOut-Mqtt5s-2")]
    [DataRow("mqtt5+ws", "CurlSurface:WriteOut-Mqtt5Ws-2")]
    public Task WriteOut_RefusingSubAck_WritesExitCode8AndTheErrorMessage(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Refused), "-sS", "-w", WriteOutFormat, Url(scheme));

                AssertRun(
                    result,
                    requirement,
                    "manual: -w; ADR-1049: a refusing reason code is exit 8",
                    8,
                    $"8|{ServerErrorMessage}|{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|0|0.000000",
                    $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Mqtt5-1")]
    [SpecRequirement("CurlSurface:Fail-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:Fail-Mqtt5Ws-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Mqtt5-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Mqtt5Ws-1")]
    [DataRow("mqtt5", "--fail", "CurlSurface:Fail-Mqtt5-1")]
    [DataRow("mqtt5s", "--fail", "CurlSurface:Fail-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "--fail", "CurlSurface:Fail-Mqtt5Ws-1")]
    [DataRow("mqtt5", "--fail-with-body", "CurlSurface:FailWithBody-Mqtt5-1")]
    [DataRow("mqtt5s", "--fail-with-body", "CurlSurface:FailWithBody-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "--fail-with-body", "CurlSurface:FailWithBody-Mqtt5Ws-1")]
    public Task Fail_RefusingSubAck_StaysExit8AsMqtt5MapsNoServerErrorTo22(string scheme, string option, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Refused), "-sS", option, Url(scheme));

                AssertRun(result, requirement, "manual: --fail is for HTTP; ADR-1049: a refusing reason code is exit 8", 8, string.Empty, $"curl: (8) {ServerErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Mqtt5-2")]
    [SpecRequirement("CurlSurface:Fail-Mqtt5s-2")]
    [SpecRequirement("CurlSurface:Fail-Mqtt5Ws-2")]
    [DataRow("mqtt5", "CurlSurface:Fail-Mqtt5-2")]
    [DataRow("mqtt5s", "CurlSurface:Fail-Mqtt5s-2")]
    [DataRow("mqtt5+ws", "CurlSurface:Fail-Mqtt5Ws-2")]
    public Task Fail_SuccessfulSubscribe_WritesTheMessageAndExits0(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(Route(scheme, Granted), "-sS", "--fail", Url(scheme));

                AssertRun(result, requirement, "manual: --fail", 0, Reply, string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Mqtt5-1")]
    [SpecRequirement("CurlSurface:MaxTime-Mqtt5s-1")]
    [SpecRequirement("CurlSurface:MaxTime-Mqtt5Ws-1")]
    [DataRow("mqtt5", "CurlSurface:MaxTime-Mqtt5-1")]
    [DataRow("mqtt5s", "CurlSurface:MaxTime-Mqtt5s-1")]
    [DataRow("mqtt5+ws", "CurlSurface:MaxTime-Mqtt5Ws-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunPastMaxTimeAsync(Route(scheme, Granted), 5, Url(scheme), TestContext.CancellationToken);

                AssertRun(result, requirement, "manual: -m, EXIT CODES 28", 28, string.Empty, "curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received\n");
            });

    private static byte[] Granted(Mqtt5WirePacket subscribe) =>
        [.. Mqtt5PublishSubscribeBytes.SubAck(PacketIdentifierOf(subscribe), 0x00), .. Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", "hello"u8.ToArray()), .. NormalDisconnect];

    private static byte[] Refused(Mqtt5WirePacket subscribe) =>
        Mqtt5PublishSubscribeBytes.SubAck(PacketIdentifierOf(subscribe), 0x80);

    private static ushort PacketIdentifierOf(Mqtt5WirePacket subscribe) => (ushort)((subscribe.Body[0] << 8) | subscribe.Body[1]);

    private static string Url(string scheme) => $"{scheme}://localhost/t";

    private static int Port(string scheme) => scheme switch
    {
        "mqtt5s" => 8883,
        "mqtt5+ws" => 80,
        _ => 1883,
    };

    // The connection line every transfer's -v writes (curl 8.22.0's "* Established connection to localhost (127.0.0.1
    // port N) from ...", as the xmpp cases measured), and for mqtt5s a TLS line: the Schannel build's "* schannel: ..."
    // after it on Windows, the OpenSSL build's "* SSL connection using TLSv1..." before it elsewhere, as the xmpp and
    // redis cases pin (the OpenSSL build reports the handshake before the connection counts as established, BL-4666).
    private static string[] VerboseLines(string scheme)
    {
        string connectionLine = $"* Established connection to localhost (127.0.0.1 port {Port(scheme)}) from ";
        if (scheme != "mqtt5s")
        {
            return [connectionLine];
        }

        return OperatingSystem.IsWindows() ? [connectionLine, "* schannel: "] : ["* SSL connection using TLSv1.", connectionLine];
    }

    private static TransferSurfaceRoute Route(string scheme, Func<Mqtt5WirePacket, byte[]> answerSubscribe) => scheme switch
    {
        "mqtt5s" => TransferSurfaceRoute.Tls(8883, () => Broker(answerSubscribe)),
        "mqtt5+ws" => TransferSurfaceRoute.Tcp(80, () => new Mqtt5WebSocketReferenceServer(respond: packet => packet.PacketType switch
        {
            Mqtt5WirePacket.Connect => [Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, Mqtt5ReferenceBroker.SuccessConnAck)],
            Mqtt5WirePacket.Subscribe => [Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, answerSubscribe(packet))],
            _ => [],
        })),
        _ => TransferSurfaceRoute.Tcp(1883, () => Broker(answerSubscribe)),
    };

    private static Mqtt5ReferenceBroker Broker(Func<Mqtt5WirePacket, byte[]> answerSubscribe) =>
        new(null, new Dictionary<int, Func<Mqtt5WirePacket, byte[]>> { [Mqtt5WirePacket.Subscribe] = answerSubscribe });
}
