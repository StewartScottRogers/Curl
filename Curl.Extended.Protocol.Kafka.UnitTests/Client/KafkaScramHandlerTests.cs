using System.Net;
using System.Text;
using Curl.Extended.Protocol.Kafka.Wire;
using Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>Checks the SCRAM-SHA-256 and SCRAM-SHA-512 logins against a scripted broker (ADR-1317).</summary>
[TestClass]
public sealed class KafkaScramHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static ScriptedKafkaConnection ScramBroker(string serverFinal, short finalError = 0, string mechanism = "SCRAM-SHA-256") => new(
        KafkaSaslTestBytes.HandshakeResponse(1, 0, mechanism),
        KafkaSaslTestBytes.AuthenticateResponse(2, 0, null, Bytes(KafkaScramClientTests.ServerFirst)),
        finalError == 0
            ? KafkaSaslTestBytes.AuthenticateResponse(3, 0, null, Bytes(serverFinal))
            : KafkaSaslTestBytes.AuthenticateResponse(3, finalError, "Authentication failed during authentication due to invalid credentials with SASL mechanism " + mechanism),
        KafkaTestBytes.MetadataLeader(4, "broker", 9092),
        KafkaTestBytes.ProduceResponse(5, 0));

    private static ScriptedKafkaConnection Refusing(string offered) => new(KafkaSaslTestBytes.HandshakeResponse(1, 33, offered));

    private static Task<TransferResult> RunAsync(IConnector connector, string password = "pencil", Func<string>? nonce = null) =>
        new KafkaProtocolHandler(connector, nonce).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("kafka://broker/orders"),
            Credentials = new NetworkCredential("user", password),
            Output = new MemoryStream(),
            PostData = Encoding.UTF8.GetBytes("hi"),
            TimeProvider = new FixedTimeProvider(Now),
        }).AsTask();

    private static string FixedNonce() => KafkaScramClientTests.ClientNonce;

    [TestMethod]
    [DataRow("SCRAM-SHA-256", KafkaScramClientTests.Sha256ServerFinal, KafkaScramClientTests.Sha256Proof)]
    [DataRow("SCRAM-SHA-512", KafkaScramClientTests.Sha512ServerFinal, KafkaScramClientTests.Sha512Proof)]
    public async Task ExecuteAsync_BrokerOffersOnlyScram_ReconnectsAndProducesAfterTheScramLogin(string mechanism, string serverFinal, string proof)
    {
        ScriptedKafkaConnection refusing = Refusing(mechanism);
        ScriptedKafkaConnection scram = ScramBroker(serverFinal, 0, mechanism);

        TransferResult result = await RunAsync(new ScriptedKafkaConnector(ConnectResult.Connected(refusing), ConnectResult.Connected(scram)), nonce: FixedNonce);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(KafkaSaslRequests.Handshake(1), refusing.Writes.Single());
        CollectionAssert.AreEqual(KafkaSaslRequests.Handshake(1, mechanism), scram.Writes[0]);
        CollectionAssert.AreEqual(KafkaSaslRequests.Authenticate(2, Bytes("n,,n=user,r=rOprNGfwEbeRWgbNEkqO")), scram.Writes[1]);
        CollectionAssert.AreEqual(KafkaSaslRequests.Authenticate(3, Bytes($"c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p={proof}")), scram.Writes[2]);
        CollectionAssert.AreEqual(KafkaRequests.Metadata(4, "orders"), scram.Writes[3]);
        Assert.IsTrue(refusing.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramWithAnotherLeader_UsesScramOnTheLeaderWithoutAskingForPlain()
    {
        ScriptedKafkaConnection first = new(
            KafkaSaslTestBytes.HandshakeResponse(1, 0),
            KafkaSaslTestBytes.AuthenticateResponse(2, 0, null, Bytes(KafkaScramClientTests.ServerFirst)),
            KafkaSaslTestBytes.AuthenticateResponse(3, 0, null, Bytes(KafkaScramClientTests.Sha256ServerFinal)),
            KafkaTestBytes.MetadataLeader(4, "leader", 9092));
        ScriptedKafkaConnection second = new(
            KafkaSaslTestBytes.HandshakeResponse(1, 0),
            KafkaSaslTestBytes.AuthenticateResponse(2, 0, null, Bytes(KafkaScramClientTests.ServerFirst)),
            KafkaSaslTestBytes.AuthenticateResponse(3, 0, null, Bytes(KafkaScramClientTests.Sha256ServerFinal)),
            KafkaTestBytes.ProduceResponse(4, 0));

        TransferResult result = await RunAsync(new ScriptedKafkaConnector(ConnectResult.Connected(Refusing("SCRAM-SHA-256")), ConnectResult.Connected(first), ConnectResult.Connected(second)), nonce: FixedNonce);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(KafkaSaslRequests.Handshake(1, "SCRAM-SHA-256"), second.Writes[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramPasswordRefused_EndsWithLoginDeniedWithoutThePassword()
    {
        TransferResult result = await RunAsync(new ScriptedKafkaConnector(ConnectResult.Connected(Refusing("SCRAM-SHA-256")), ConnectResult.Connected(ScramBroker(string.Empty, 58))), "wrong", FixedNonce);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "Kafka SASL SCRAM-SHA-256 authentication failed: Authentication failed");
        Assert.DoesNotContain("wrong", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramRefusedToo_EndsWithLoginDeniedWithoutAnotherReconnect()
    {
        ScriptedKafkaConnector connector = new(ConnectResult.Connected(Refusing("SCRAM-SHA-256")), ConnectResult.Connected(Refusing("SCRAM-SHA-256")));

        TransferResult result = await RunAsync(connector, nonce: FixedNonce);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Kafka broker does not enable SASL PLAIN, it enables: SCRAM-SHA-256", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScramServerSignatureWrong_EndsWithWeirdServerReply()
    {
        TransferResult result = await RunAsync(new ScriptedKafkaConnector(ConnectResult.Connected(Refusing("SCRAM-SHA-256")), ConnectResult.Connected(ScramBroker("v=AAAA"))), nonce: FixedNonce);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RandomNonce_ServerFirstWithAnotherNonceEndsWithWeirdServerReply()
    {
        ScriptedKafkaConnection scram = ScramBroker(KafkaScramClientTests.Sha256ServerFinal);

        TransferResult result = await RunAsync(new ScriptedKafkaConnector(ConnectResult.Connected(Refusing("SCRAM-SHA-256")), ConnectResult.Connected(scram)));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.DoesNotContain("pencil", Encoding.UTF8.GetString(scram.Writes[1]));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
