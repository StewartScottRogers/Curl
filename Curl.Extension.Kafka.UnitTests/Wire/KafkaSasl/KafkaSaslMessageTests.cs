using System.Text;

namespace Curl.Extension.Kafka.Wire.KafkaSasl;

/// <summary>Checks SaslHandshake and SaslAuthenticate against the protocol guide and the PLAIN message against RFC 4616.</summary>
[TestClass]
public sealed class KafkaSaslMessageTests
{
    [TestMethod]
    public void Handshake_IsVersionOneNamingPlain()
    {
        byte[] expected = KafkaTestBytes.Framed(new KafkaWriter().Int16(17).Int16(1).Int32(4).String("curl").String("PLAIN"));

        CollectionAssert.AreEqual(expected, KafkaSaslRequests.Handshake(4));
    }

    [TestMethod]
    public void Authenticate_IsVersionZeroCarryingThePlainMessage()
    {
        byte[] expected = KafkaTestBytes.Framed(new KafkaWriter().Int16(36).Int16(0).Int32(5).String("curl").Bytes("\0alice\0s3cret"u8));

        CollectionAssert.AreEqual(expected, KafkaSaslRequests.Authenticate(5, "alice", "s3cret"));
    }

    [TestMethod]
    public void PlainMessage_IsEmptyAuthorizationIdentityThenUserAndPasswordInUtf8() =>
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("\0jörg\0päss"), KafkaSaslRequests.PlainMessage("jörg", "päss"));

    [TestMethod]
    public void ReadHandshake_GivesTheErrorCodeAndTheEnabledMechanisms()
    {
        KafkaSaslHandshakeResponse response = KafkaSaslResponses.ReadHandshake(KafkaTestBytes.BodyOf(KafkaSaslTestBytes.HandshakeResponse(1, 33, "SCRAM-SHA-256", "GSSAPI")));

        Assert.AreEqual((short)33, response.ErrorCode);
        CollectionAssert.AreEqual(new[] { "SCRAM-SHA-256", "GSSAPI" }, response.Mechanisms.ToArray());
    }

    [TestMethod]
    public void ReadHandshake_NullMechanism_GivesAnEmptyName()
    {
        byte[] framed = KafkaTestBytes.Framed(new KafkaWriter().Int32(1).Int16(0).Int32(1).NullString());

        CollectionAssert.AreEqual(new[] { string.Empty }, KafkaSaslResponses.ReadHandshake(KafkaTestBytes.BodyOf(framed)).Mechanisms.ToArray());
    }

    [TestMethod]
    public void ReadAuthenticate_GivesTheErrorCodeAndTheBrokersMessage()
    {
        KafkaSaslAuthenticateResponse response = KafkaSaslResponses.ReadAuthenticate(KafkaTestBytes.BodyOf(KafkaSaslTestBytes.AuthenticateResponse(2, 58, "Invalid username or password")));

        Assert.AreEqual(new KafkaSaslAuthenticateResponse(58, "Invalid username or password"), response);
    }

    [TestMethod]
    public void ReadAuthenticate_Truncated_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => KafkaSaslResponses.ReadAuthenticate(new byte[] { 0 }));

    [TestMethod]
    public void AuthenticationFailed_NoBrokerMessage_NamesTheErrorCode() =>
        Assert.AreEqual("Kafka SASL PLAIN authentication failed: error 58", KafkaSaslMessages.AuthenticationFailed(58, null));

    [TestMethod]
    public void PlainNotEnabled_ListsTheEnabledMechanisms() =>
        Assert.AreEqual("Kafka broker does not enable SASL PLAIN, it enables: SCRAM-SHA-256, GSSAPI", KafkaSaslMessages.PlainNotEnabled(["SCRAM-SHA-256", "GSSAPI"]));
}
