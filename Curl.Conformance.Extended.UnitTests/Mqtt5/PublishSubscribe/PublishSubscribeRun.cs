using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.PublishSubscribe;

/// <summary>Runs curl's <c>mqtt5://</c> against the reference broker for the PUBLISH, QoS and SUBSCRIBE cases (BL-4485).</summary>
internal static class PublishSubscribeRun
{
    /// <summary>The PUBACK packet type.</summary>
    internal const int PubAck = 4;

    /// <summary>The PUBREC packet type.</summary>
    internal const int PubRec = 5;

    /// <summary>The PUBCOMP packet type.</summary>
    internal const int PubComp = 7;

    /// <summary>The PINGREQ packet type.</summary>
    internal const int PingReq = 12;

    /// <summary>A DISCONNECT with Reason Code 0x00 left out (section 3.14.2.1).</summary>
    internal static readonly byte[] NormalDisconnect = [0xE0, 0x00];

    private const int Port = 1883;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    /// <summary>Gets the known failures the cases run under.</summary>
    internal static KnownFailures Known { get; } = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>Runs curl with <paramref name="arguments"/> against a broker answering with <paramref name="responders"/>.</summary>
    internal static async Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> RunAsync(
        IReadOnlyDictionary<int, Func<Mqtt5WirePacket, byte[]>> responders, byte[]? connAck, params string[] arguments)
    {
        Mqtt5ReferenceBroker broker = new(connAck, responders);
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, broker)], ["-sS", .. arguments]);
        return (result, broker);
    }

    /// <summary>Runs <c>curl -sS -m 3 mqtt5://localhost/t</c> against a broker answering SUBSCRIBE with a SUBACK granting QoS 0 and then <paramref name="packets"/>.</summary>
    internal static Task<(CurlRunResult Result, Mqtt5ReferenceBroker Broker)> SubscribeAsync(
        IReadOnlyDictionary<int, Func<Mqtt5WirePacket, byte[]>>? moreResponders, params byte[][] packets)
    {
        Dictionary<int, Func<Mqtt5WirePacket, byte[]>> responders = new(moreResponders ?? new Dictionary<int, Func<Mqtt5WirePacket, byte[]>>())
        {
            [Mqtt5WirePacket.Subscribe] = _ => [.. Mqtt5PublishSubscribeBytes.SubAck(1, 0x00), .. packets.SelectMany(packet => packet)],
        };
        return RunAsync(responders, null, "-m", "3", "mqtt5://localhost/t");
    }

    /// <summary>Gets the packet types the broker read, in order.</summary>
    internal static int[] TypesOf(Mqtt5ReferenceBroker broker) => [.. broker.ReceivedPackets.Select(packet => packet.PacketType)];

    /// <summary>Gets the frame of each packet the broker read after the SUBSCRIBE.</summary>
    internal static byte[][] FramesAfterSubscribe(Mqtt5ReferenceBroker broker) =>
        [.. broker.ReceivedPackets.Skip(2).Select(packet => Mqtt5WirePacket.Frame(packet.FirstByte, packet.Body))];

    /// <summary>Asserts exit 0, <paramref name="standardOutput"/> and nothing on standard error.</summary>
    internal static void AssertSucceeded(CurlRunResult result, string standardOutput)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(standardOutput, Encoding.UTF8.GetString(result.StandardOutput));
        Assert.AreEqual(0, result.StandardError.Length);
    }

    /// <summary>Asserts the exit code, no output and exactly <paramref name="message"/> on standard error.</summary>
    internal static void AssertFailed(CurlRunResult result, int exitCode, string message)
    {
        Assert.AreEqual(exitCode, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual($"curl: ({exitCode}) {message}{Environment.NewLine}", Encoding.UTF8.GetString(result.StandardError));
    }

    /// <summary>Asserts curl's last packet is a DISCONNECT carrying <paramref name="reasonCode"/>.</summary>
    internal static void AssertEndedWithDisconnect(Mqtt5ReferenceBroker broker, byte reasonCode)
    {
        Mqtt5WirePacket last = broker.ReceivedPackets[^1];
        Assert.AreEqual(Mqtt5WirePacket.Disconnect, last.PacketType, "curl closed the connection without a DISCONNECT carrying the reason code.");
        Assert.AreEqual(reasonCode, last.Body[0]);
    }
}
