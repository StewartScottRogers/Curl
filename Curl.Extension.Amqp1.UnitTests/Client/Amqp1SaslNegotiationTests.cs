using System.Net;
using Curl.Extension.Amqp1.Frames;
using Curl.Extension.Amqp1.Types;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Amqp1.Client.Amqp1ServerBytes;

namespace Curl.Extension.Amqp1.Client;

[TestClass]
public sealed class Amqp1SaslNegotiationTests
{
    [TestMethod]
    public async Task NegotiateAsync_NoCredentials_SendsAnonymousWithHostname_Part5Section5_3_3_2()
    {
        ScriptedAmqp1Connection connection = new([.. SaslHeader, .. Mechanisms("PLAIN", "ANONYMOUS"), .. Outcome(0)]);

        Assert.IsNull(await NegotiateAsync(connection, null));

        var written = Written(connection);
        Assert.IsNull(written[0].Performative);
        CollectionAssert.AreEqual(Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.SaslProtocolId), connection.Writes[0]);
        var init = written[1].Performative!;
        Assert.AreEqual(Amqp1SaslInitialResponse.Anonymous, init["mechanism"]);
        CollectionAssert.AreEqual(Array.Empty<byte>(), (byte[])init["initial-response"]!);
        Assert.AreEqual("broker", init["hostname"]);
    }

    [TestMethod]
    public async Task NegotiateAsync_Credentials_SendsPlainFromASingleOfferedSymbol_Rfc4616()
    {
        ScriptedAmqp1Connection connection = new([.. SaslHeader, .. Mechanisms(new Amqp1Symbol("PLAIN")), .. Outcome(0)]);

        Assert.IsNull(await NegotiateAsync(connection, new NetworkCredential("u", "p")));

        var init = Written(connection)[1].Performative!;
        Assert.AreEqual(Amqp1SaslInitialResponse.Plain, init["mechanism"]);
        CollectionAssert.AreEqual("\0u\0p"u8.ToArray(), (byte[])init["initial-response"]!);
    }

    [TestMethod]
    public async Task NegotiateAsync_MechanismNotOffered_EndsWithLoginDenied()
    {
        var failure = await NegotiateAsync(new([.. SaslHeader, .. Mechanisms("PLAIN")]), null);

        Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode);
        Assert.AreEqual("AMQP 1.0 server does not offer the SASL ANONYMOUS mechanism", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied()
    {
        var failure = await NegotiateAsync(new([.. SaslHeader, .. Mechanisms((object)"PLAIN")]), new NetworkCredential("u", "p"));

        Assert.AreEqual("AMQP 1.0 server does not offer the SASL PLAIN mechanism", failure!.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_OutcomeNotOk_EndsWithLoginDenied_Part5Section5_3_3_6()
    {
        var failure = await NegotiateAsync(new([.. SaslHeader, .. Mechanisms("ANONYMOUS"), .. Outcome(1)]), null);

        Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode);
        Assert.AreEqual("AMQP 1.0 SASL authentication failed with outcome code 1", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAsync_ChallengeInsteadOfOutcome_EndsWithWeirdServerReply()
    {
        var failure = await NegotiateAsync(
            new([.. SaslHeader, .. Mechanisms("ANONYMOUS"), .. Sasl(Amqp1CompositeType.SaslChallenge, ("challenge", new byte[] { 1 }))]), null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure!.ExitCode);
        Assert.AreEqual("Weird server reply: AMQP 1.0 sasl-challenge where sasl-outcome was expected", failure.Message);
    }

    private static async Task<Amqp1ClientException?> NegotiateAsync(ScriptedAmqp1Connection connection, NetworkCredential? credentials)
    {
        try
        {
            await Amqp1SaslNegotiation.NegotiateAsync(new Amqp1FrameChannel(connection, CancellationToken.None), credentials, "broker");
            return null;
        }
        catch (Amqp1ClientException failure)
        {
            return failure;
        }
    }
}
