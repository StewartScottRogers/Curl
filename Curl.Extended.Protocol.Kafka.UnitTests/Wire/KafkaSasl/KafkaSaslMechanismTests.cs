using System.Security.Cryptography;

namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>Checks the choice of SCRAM mechanism, the SCRAM request and response bytes and the failure message (ADR-1315).</summary>
[TestClass]
public sealed class KafkaSaslMechanismTests
{
    [TestMethod]
    public void StrongestScram_BothOffered_IsSha512() =>
        Assert.AreEqual("SCRAM-SHA-512", KafkaSaslMechanisms.StrongestScram(["PLAIN", "SCRAM-SHA-256", "SCRAM-SHA-512"]));

    [TestMethod]
    public void StrongestScram_OnlySha256_IsSha256() =>
        Assert.AreEqual("SCRAM-SHA-256", KafkaSaslMechanisms.StrongestScram(["GSSAPI", "SCRAM-SHA-256"]));

    [TestMethod]
    public void StrongestScram_NoScram_IsNull() =>
        Assert.IsNull(KafkaSaslMechanisms.StrongestScram(["GSSAPI"]));

    [TestMethod]
    public void HashOf_EachMechanism_IsItsHash()
    {
        Assert.AreEqual(HashAlgorithmName.SHA512, KafkaSaslMechanisms.HashOf("SCRAM-SHA-512"));
        Assert.AreEqual(HashAlgorithmName.SHA256, KafkaSaslMechanisms.HashOf("SCRAM-SHA-256"));
    }

    [TestMethod]
    public void Handshake_NamedMechanism_CarriesItsName()
    {
        byte[] request = KafkaSaslRequests.Handshake(3, "SCRAM-SHA-512");

        CollectionAssert.AreEqual("SCRAM-SHA-512"u8.ToArray(), request[^13..]);
    }

    [TestMethod]
    public void Authenticate_AuthBytes_CarriesThemAfterTheirLength()
    {
        byte[] request = KafkaSaslRequests.Authenticate(2, "n,,n=u"u8.ToArray());

        CollectionAssert.AreEqual("n,,n=u"u8.ToArray(), request[^6..]);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 6 }, request[^10..^6]);
    }

    [TestMethod]
    public void ReadAuthenticate_AuthBytes_AreGiven()
    {
        byte[] framed = KafkaSaslTestBytes.AuthenticateResponse(1, 0, null, "r=abc"u8.ToArray());

        KafkaSaslAuthenticateResponse response = KafkaSaslResponses.ReadAuthenticate(framed.AsMemory(8));

        CollectionAssert.AreEqual("r=abc"u8.ToArray(), response.AuthBytes.ToArray());
    }

    [TestMethod]
    public void AuthenticationFailed_ScramMechanism_NamesIt() =>
        Assert.AreEqual("Kafka SASL SCRAM-SHA-256 authentication failed: error 58", KafkaSaslMessages.AuthenticationFailed(58, null, "SCRAM-SHA-256"));
}
