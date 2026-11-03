using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using Curl.Conformance.Extended.SocketIo;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.SocketIo.SocketIoRun;

namespace Curl.Conformance.Extended.UnitTests.SocketIo;

/// <summary>Engine.IO v4 over the WebSocket transport, and the RFC 6455 client rules it relies on.</summary>
[TestClass]
public sealed class SocketIoEngineIoCases
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Transport-1")]
    [SpecRequirement("SocketIo:RFC6455-4.1-1")]
    [SpecRequirement("SocketIo:RFC6455-5.1-1")]
    [SpecRequirement("SocketIo:RFC6455-5.6-1")]
    public async Task Upgrade_IsAWebSocketOnlyConnectionWithMaskedTextFrames()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        SignalRReferenceRequest upgrade = server.Requests.Single();
        Assert.AreEqual("GET", upgrade.Method);
        Assert.AreEqual("/socket.io/?EIO=4&transport=websocket", upgrade.Target);
        Assert.AreEqual("localhost:5000", upgrade.Header("Host"));
        Assert.AreEqual("websocket", upgrade.Header("Upgrade"), ignoreCase: true);
        StringAssert.Contains(upgrade.Header("Connection"), "Upgrade");
        Assert.AreEqual("13", upgrade.Header("Sec-WebSocket-Version"));
        Assert.HasCount(16, Convert.FromBase64String(upgrade.Header("Sec-WebSocket-Key")!));
        Assert.IsTrue(server.ClientFrames.All(frame => frame.IsMasked), "Every client frame is masked.");
        Assert.IsTrue(server.ClientFrames.Take(1).All(frame => frame.IsFinal && frame.Opcode == SignalRReferenceFrame.Text));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Open-1")]
    [SpecRequirement("SocketIo:EIO-Open-2")]
    public async Task OpenPacket_IsReadBeforeTheClientSendsItsConnect()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("""42["news",{"hello":"world"}]"""), SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
        Assert.AreEqual("[\"news\",{\"hello\":\"world\"}]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Heartbeat-1")]
    public async Task ServerPing_IsAnsweredWithAPong()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("2"), SocketIoStep.Receive, SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40", "3" }, server.ClientPackets.ToArray());
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Heartbeat-2")]
    public async Task MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock()
    {
        SocketIoManualClock clock = new();
        SocketIoReferenceServer server = new(
            SocketIoStep.Open(pingInterval: 1000, pingTimeout: 1000), SocketIoStep.Receive, SocketIoStep.Packet("40{\"sid\":\"s\"}"));

        await SocketIoRun.KnownFailures.RunCaseAsync(["SocketIo:EIO-Heartbeat-2"], async () =>
        {
            Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
                InProcessCurl.RunAsync, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "100", Url], timeProvider: clock);
            try
            {
                await WaitForAsync(() => server.ClientPackets.Count > 0);
                for (int tenth = 0; tenth < 25 && !run.IsCompleted; tenth++)
                {
                    clock.Advance(TimeSpan.FromMilliseconds(100));
                    await Task.Delay(10, TestContext.CancellationToken);
                }

                Assert.IsTrue(run.IsCompleted, "The client ends the connection once pingInterval + pingTimeout pass with no ping.");
                Assert.AreNotEqual(28, (await run).ExitCode, "The run ends on the ping timeout, not on -m.");
            }
            finally
            {
                for (int second = 0; second <= 100 && !run.IsCompleted; second++)
                {
                    clock.Advance(TimeSpan.FromSeconds(1));
                    await Task.Yield();
                }

                await run.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            }
        });
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Close-1")]
    [SpecRequirement("SocketIo:EIO-Noop-1")]
    public async Task NoopIsIgnoredAndCloseEndsTheSession()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("6"), SocketIoStep.Packet("""42["after noop"]"""), SocketIoStep.Packet("1"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"after noop\"]\n", Text(result.StandardOutput));
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-MaxPayload-1")]
    public async Task EventOverMaxPayload_IsNotSent()
    {
        SocketIoReferenceServer server = new(
            SocketIoStep.Open(maxPayload: 16), SocketIoStep.Receive, SocketIoStep.Packet("40{\"sid\":\"s\"}"), SocketIoStep.Receive, SocketIoStep.Packet("41"));

        await SocketIoRun.KnownFailures.RunCaseAsync(["SocketIo:EIO-MaxPayload-1"], async () =>
        {
            CurlRunResult result = await RunAsync(server, "-d", """["big","0123456789abcdef0123456789"]""", Url);

            Assert.IsTrue(server.ClientPackets.All(packet => Encoding.UTF8.GetByteCount(packet) <= 16), "No packet over maxPayload is sent.");
            Assert.AreNotEqual(0, result.ExitCode, "An event that cannot be sent fails the transfer.");
        });
    }

    [TestMethod]
    [SpecRequirement("SocketIo:RFC6455-4.1-2")]
    public async Task UpgradeAnsweredWithTheWrongAccept_FailsWithExit8AndSendsNoFrame()
    {
        SocketIoReferenceServer server = new(SocketIoStep.Open()) { AcceptOverride = "d3JvbmcgYWNjZXB0IHZhbHVlIQ==" };

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.IsEmpty(server.ClientFrames);
        Assert.AreEqual("curl: (8) Socket.IO: the server's Sec-WebSocket-Accept does not match the key sent\n", Error(result));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:RFC6455-5.5.2-1")]
    public async Task WebSocketPing_IsAnsweredWithAPongCarryingItsPayload()
    {
        SocketIoReferenceServer server = Connected(
            SocketIoStep.Send(0x89, Encoding.ASCII.GetBytes("are you there")), SocketIoStep.Receive, SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        SignalRReferenceFrame pong = server.ClientFrames[1];
        Assert.AreEqual(SignalRReferenceFrame.Pong, pong.Opcode);
        Assert.AreEqual("are you there", Encoding.ASCII.GetString(pong.Payload));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:RFC6455-5.4-1")]
    public async Task FragmentedTextMessage_IsReassembled()
    {
        SocketIoReferenceServer server = Connected(
            SocketIoStep.Send(0x01, Encoding.UTF8.GetBytes("42[\"split\",")),
            SocketIoStep.Send(0x80, Encoding.UTF8.GetBytes("\"" + new string('x', 200) + "\"]")),
            SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"split\",\"" + new string('x', 200) + "\"]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:RFC6455-5.5.1-1")]
    public async Task WebSocketClose_IsAnsweredWithAClose()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Send(0x88, [0x03, 0xE8]));

        await SocketIoRun.KnownFailures.RunCaseAsync(["SocketIo:RFC6455-5.5.1-1"], async () =>
        {
            await RunAsync(server, Url);

            SignalRReferenceFrame close = server.ClientFrames[^1];
            Assert.AreEqual(SignalRReferenceFrame.Close, close.Opcode);
            CollectionAssert.AreEqual(new byte[] { 0x03, 0xE8 }, close.Payload);
        });
    }

    [TestMethod]
    [SpecRequirement("SocketIo:EIO-Transport-2")]
    public async Task SecureScheme_UpgradesOverTls()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("""42["secure"]"""), SocketIoStep.Packet("41"));
        using System.Security.Cryptography.X509Certificates.X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [ReferenceRoute.Tls(Port, server, certificate)], ["-sS", "-k", "-m", "30", "socketios://localhost:5000/"]);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"secure\"]\n", Text(result.StandardOutput));
        Assert.IsTrue(result.Exchanges.All(exchange => exchange.Transport == ReferenceTransport.Tls));
        Assert.AreEqual("/socket.io/?EIO=4&transport=websocket", server.Requests.Single().Target);
    }

    private async Task WaitForAsync(Func<bool> condition)
    {
        while (!condition())
        {
            await Task.Delay(10, TestContext.CancellationToken);
        }
    }
}
