using System.Security.Cryptography;
using System.Text;

namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>Checks the SCRAM client against RFC 7677's SCRAM-SHA-256 vector, a SCRAM-SHA-512 vector worked out the same way, and malformed server messages.</summary>
[TestClass]
public sealed class KafkaScramClientTests
{
    internal const string ClientNonce = "rOprNGfwEbeRWgbNEkqO";
    internal const string ServerFirst = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
    internal const string Sha256Proof = "dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
    internal const string Sha256ServerFinal = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";
    internal const string Sha512Proof = "gMGXRcevScNtxZ6/8lQYpGtnsNAc3mGcmNomv+xnoOMw+3R2xNJdMNnzMlTN8PPC6wdp6dybEmDYXYTxwnYPJQ==";
    internal const string Sha512ServerFinal = "v=ZQnYEgWQMFmmsM8aQMF0nDDCy/AgCzkwk8CmMZYcMg0vSVlKDanekLtifDSeVGT4+5ZxXnJq199RVG2rR7N7Zw==";

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [TestMethod]
    public void Exchange_Rfc7677Sha256Vector_ProducesTheDocumentedMessages()
    {
        KafkaScramClient client = new(HashAlgorithmName.SHA256, "user", "pencil", ClientNonce);

        Assert.AreEqual("n,,n=user,r=rOprNGfwEbeRWgbNEkqO", Text(client.ClientFirst()));
        Assert.AreEqual($"c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p={Sha256Proof}", Text(client.ClientFinal(Bytes(ServerFirst))));
        client.VerifyServerFinal(Bytes(Sha256ServerFinal));
    }

    [TestMethod]
    public void Exchange_Sha512Vector_ProducesTheProofAndAcceptsTheServerSignature()
    {
        KafkaScramClient client = new(HashAlgorithmName.SHA512, "user", "pencil", ClientNonce);

        client.ClientFirst();
        Assert.AreEqual($"c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p={Sha512Proof}", Text(client.ClientFinal(Bytes(ServerFirst))));
        client.VerifyServerFinal(Bytes(Sha512ServerFinal));
    }

    [TestMethod]
    public void ClientFirst_UserNameWithEqualsAndComma_EscapesThem() =>
        Assert.AreEqual("n,,n=a=3Db=2Cc,r=x", Text(new KafkaScramClient(HashAlgorithmName.SHA256, "a=b,c", "p", "x").ClientFirst()));

    [TestMethod]
    [DataRow("s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096")]
    [DataRow("r=otherNonce,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096")]
    [DataRow("r=rOprNGfwEbeRWgbNEkqOx,i=4096")]
    [DataRow("r=rOprNGfwEbeRWgbNEkqOx,s=W22ZaJ0SNY7soEsUEjb6gQ==")]
    [DataRow("r=rOprNGfwEbeRWgbNEkqOx,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=many")]
    [DataRow("r=rOprNGfwEbeRWgbNEkqOx,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=0")]
    [DataRow("garbage")]
    public void ClientFinal_MalformedServerFirst_IsAFormatException(string serverFirst)
    {
        KafkaScramClient client = new(HashAlgorithmName.SHA256, "user", "pencil", ClientNonce);
        client.ClientFirst();

        Assert.Throws<FormatException>(() => client.ClientFinal(Bytes(serverFirst)));
    }

    [TestMethod]
    [DataRow("v=AAAA")]
    [DataRow("e=invalid-proof")]
    public void VerifyServerFinal_WrongOrMissingSignature_IsAFormatException(string serverFinal)
    {
        KafkaScramClient client = new(HashAlgorithmName.SHA256, "user", "pencil", ClientNonce);
        client.ClientFirst();
        client.ClientFinal(Bytes(ServerFirst));

        Assert.Throws<FormatException>(() => client.VerifyServerFinal(Bytes(serverFinal)));
    }

    [TestMethod]
    public void VerifyServerFinal_AttributeThatIsNotKeyEquals_IsIgnored()
    {
        KafkaScramClient client = new(HashAlgorithmName.SHA256, "user", "pencil", ClientNonce);
        client.ClientFirst();
        client.ClientFinal(Bytes(ServerFirst));

        client.VerifyServerFinal(Bytes("x," + Sha256ServerFinal));
    }
}
