using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Amqp0;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Amqp0;

/// <summary>AMQP 0-9-1 protocol header, framing, data types and the connection class (BL-4464), against the reference broker.</summary>
[TestClass]
public sealed class Amqp0ConnectionTests
{
    private const int AmqpPort = 5672;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static async Task<CurlRunResult> RunAsync(Amqp0ReferenceBroker broker, params string[] arguments) =>
        await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(AmqpPort, broker)], ["-sS", "-m", "10", .. arguments]);

    private static Amqp0ReferenceBroker Broker(Amqp0MethodTable table) => new(new Amqp0BrokerSettings(), table);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void AssertExit(CurlRunResult result, int exitCode, string standardError)
    {
        // curl ends its message line with the platform's newline, CRLF on Windows.
        Assert.AreEqual(standardError, Text(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.2-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.3-1")]
    [SpecRequirement("Amqp0:AMQP091-2.2.4-1")]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.8-1")]
    public async Task Publish_OpensWithTheProtocolHeaderAndClosesTheConnection()
    {
        Amqp0ReferenceBroker broker = new();

        CurlRunResult result = await RunAsync(broker, "amqp://localhost/%2f/q", "-d", "hi");

        AssertExit(result, 0, string.Empty);
        CollectionAssert.AreEqual("AMQP\0\0\u0009\u0001"u8.ToArray(), broker.ClientProtocolHeader);
        Assert.IsTrue(broker.ClientFrames.All(frame => frame.FrameEnd == Amqp0BrokerFrame.FrameEndOctet));
        CollectionAssert.AreEqual(new[] { "10.11", "10.31", "10.40", "20.10", "50.10", "60.40", "20.40", "10.50" }, broker.ClientMethods.ToArray());
        Amqp0ArgumentReader close = broker.ClientMethod(10, 50).Arguments();
        Assert.AreEqual(200, close.Short());
        Assert.AreEqual(0, broker.ClientMethod(10, 50).Channel);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.1-1")]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.2-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.5.5-1")]
    [SpecRequirement("Amqp0:RFC4616-2-1")]
    public async Task StartOk_ChoosesPlainAndAnOfferedLocaleInAWellFormedTable()
    {
        Amqp0ReferenceBroker broker = new();

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 0, string.Empty);

        Amqp0ArgumentReader startOk = broker.ClientMethod(10, 11).Arguments();
        IReadOnlyList<KeyValuePair<string, object>> properties = startOk.Table();
        Assert.IsTrue(properties.All(entry => entry.Key.Length <= 128 && (char.IsAsciiLetter(entry.Key[0]) || entry.Key[0] is '$' or '#')));
        Assert.AreEqual("curl", properties.Single(entry => entry.Key == "product").Value);
        Assert.AreEqual("PLAIN", startOk.ShortString());
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), startOk.LongString());
        Assert.AreEqual("en_US", startOk.ShortString());
        Assert.IsTrue(startOk.AtEnd);
        Assert.AreEqual("vh", broker.ClientMethod(10, 40).Arguments().ShortString());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:RFC4616-2-2")]
    public async Task StartOk_CarriesTheCredentialsOfTheUserOptionAndOfTheUrl()
    {
        Amqp0ReferenceBroker fromOption = new();
        Amqp0ReferenceBroker fromUrl = new();

        AssertExit(await RunAsync(fromOption, "-u", "alice:s3cret", "amqp://localhost/vh/q"), 0, string.Empty);
        AssertExit(await RunAsync(fromUrl, "amqp://bob:pw@localhost/vh/q"), 0, string.Empty);

        static byte[] Response(Amqp0ReferenceBroker broker)
        {
            Amqp0ArgumentReader startOk = broker.ClientMethod(10, 11).Arguments();
            startOk.Table();
            startOk.ShortString();
            return startOk.LongString();
        }

        CollectionAssert.AreEqual("\0alice\0s3cret"u8.ToArray(), Response(fromOption));
        CollectionAssert.AreEqual("\0bob\0pw"u8.ToArray(), Response(fromUrl));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.2-2")]
    public async Task Start_WithoutPlain_FailsLoginWithoutAnswering()
    {
        Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings { Mechanisms = "AMQPLAIN" }, Amqp0MethodTable.Standard);

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 67, "curl: (67) AMQP server offers no PLAIN mechanism (it offers \"AMQPLAIN\")\n");
        Assert.AreEqual(0, broker.ClientFrames.Count);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.2-2")]
    public async Task ProtocolHeader_BrokerAnswersWithAnotherVersion_FailsWithoutFrames()
    {
        Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings { ProtocolHeaderReply = "AMQP\0\u0001\0\0"u8.ToArray() }, Amqp0MethodTable.Standard);

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 8, "curl: (8) Weird server reply: AMQP 501 The peer answered with protocol header 414D515000010000, not AMQP 0-9-1.\n");
        Assert.IsEmpty(broker.ClientFrames);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.6-1")]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.6-2")]
    [SpecRequirement("Amqp0:AMQP091-4.2.3-5")]
    public async Task Tune_ClientAcceptsLowerLimitsAndSplitsContentAtFrameMax()
    {
        Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings { ChannelMax = 10, FrameMax = 4096 }, Amqp0MethodTable.Standard);

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q", "-d", new string('x', 10000)), 0, string.Empty);

        Amqp0ArgumentReader tuneOk = broker.ClientMethod(10, 31).Arguments();
        Assert.AreEqual(10, tuneOk.Short());
        Assert.AreEqual(4096u, tuneOk.Long());
        Assert.IsTrue(broker.ClientFrames.All(frame => frame.Payload.Length + 8 <= 4096));
        Assert.AreEqual(10000, broker.ClientFrames.Where(frame => frame.Type == 3).Sum(frame => frame.Payload.Length));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.6-3")]
    public async Task Tune_ZeroMeansNoLimitSoTheClientProposesItsOwn()
    {
        Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings { ChannelMax = 0, FrameMax = 0 }, Amqp0MethodTable.Standard);

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 0, string.Empty);

        Amqp0ArgumentReader tuneOk = broker.ClientMethod(10, 31).Arguments();
        Assert.AreNotEqual(0, tuneOk.Short());
        Assert.AreNotEqual(0u, tuneOk.Long());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.7-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.7-2")]
    public async Task Heartbeat_ClientDeclinesAndSkipsHeartbeatFrames()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 40, async (b, _, token) =>
        {
            await b.SendAsync(Amqp0BrokerFrame.Encode(Amqp0BrokerFrame.HeartbeatType, 0, []), token);
            return await b.ReplyAsync(0, 10, 41, new Amqp0ArgumentWriter().ShortString(string.Empty), token);
        }));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 0, string.Empty);

        Amqp0ArgumentReader tuneOk = broker.ClientMethod(10, 31).Arguments();
        tuneOk.Short();
        tuneOk.Long();
        Assert.AreEqual(0, tuneOk.Short());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.7-3")]
    public async Task Heartbeat_OnAChannelOtherThanZero_IsAFrameError()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 40, (b, _, token) => b.SendAsync(Amqp0BrokerFrame.Encode(Amqp0BrokerFrame.HeartbeatType, 1, []), token)));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 8, "curl: (8) Weird server reply: AMQP 501 A heartbeat frame came on channel 1, not channel 0.\n");
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.3-2")]
    public async Task Frame_WithoutFrameEnd_IsAFrameError()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 11, (b, _, token) =>
            b.SendAsync(Amqp0BrokerFrame.Encode(1, 0, [0, 10, 0, 30, 0, 0, 0, 0, 0, 0, 0, 0], 0x00), token)));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 8, "curl: (8) Weird server reply: AMQP 501 A frame ends with 0x00, not frame-end 0xCE.\n");
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.3-3")]
    public async Task Frame_OfAnUnknownType_IsAFrameError()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 11, (b, _, token) => b.SendAsync(Amqp0BrokerFrame.Encode(9, 0, []), token)));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 8, "curl: (8) Weird server reply: AMQP 501 Frame type 9 is not one AMQP 0-9-1 defines.\n");
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.3-4")]
    public async Task Frame_Malformed_IsAnsweredWithConnectionClose501()
    {
        await Known.RunCaseAsync(["Amqp0:AMQP091-4.2.3-4"], async () =>
        {
            Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard
                .With(10, 11, (b, _, token) => b.SendAsync(Amqp0BrokerFrame.Encode(9, 0, []), token))
                .With(10, 50, async (b, _, token) => !await b.ReplyAsync(0, 10, 51, new Amqp0ArgumentWriter(), token)));

            await RunAsync(broker, "amqp://localhost/vh/q");

            Assert.AreEqual(501, broker.ClientMethod(10, 50).Arguments().Short());
        });
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.9-1")]
    public async Task Close_FromTheBroker_IsAnsweredWithCloseOk()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 40, (b, _, token) =>
            b.ReplyAsync(0, 10, 50, new Amqp0ArgumentWriter().Short(530).ShortString("NOT_ALLOWED - vhost not found").Short(10).Short(40), token)));

        AssertExit(await RunAsync(broker, "amqp://localhost/nope/q"), 8, "curl: (8) AMQP server sent connection.close: reply code 530, NOT_ALLOWED - vhost not found\n");
        Assert.AreEqual("10.51", broker.ClientMethods[^1]);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.4.2.9-2")]
    public async Task Close_AccessRefused_FailsLogin()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.With(10, 11, (b, _, token) =>
            b.ReplyAsync(0, 10, 50, new Amqp0ArgumentWriter().Short(403).ShortString("ACCESS_REFUSED").Short(10).Short(11), token)));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 67, "curl: (67) AMQP server sent connection.close: reply code 403, ACCESS_REFUSED\n");
        Assert.AreEqual("10.51", broker.ClientMethods[^1]);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:RabbitMQ-URI-amqps-1")]
    public async Task Amqps_RunsTheSameConnectionOverTls()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        Amqp0ReferenceBroker broker = new();

        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tls(5671, broker, certificate)], ["-sS", "-k", "-m", "10", "amqps://localhost/vh/q", "-d", "hi"]);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual(ReferenceTransport.Tls, result.Exchanges.Single().Transport);
        CollectionAssert.AreEqual("AMQP\0\0\u0009\u0001"u8.ToArray(), broker.ClientProtocolHeader);
        Assert.AreEqual("10.50", broker.ClientMethods[^1]);
    }

    [TestMethod]
    public void ArgumentReader_FieldValueOfAnotherType_IsRefused()
    {
        byte[] table = new Amqp0ArgumentWriter().LongString(new Amqp0ArgumentWriter().ShortString("n").Octet((byte)'I').Long(1).ToArray()).ToArray();

        Assert.ThrowsExactly<InvalidDataException>(() => new Amqp0ArgumentReader(table, 0).Table());
    }
}
