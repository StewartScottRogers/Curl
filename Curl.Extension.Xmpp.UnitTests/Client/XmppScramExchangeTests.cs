using System.Security.Cryptography;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Xmpp.Client;

/// <summary>Checks the SCRAM client against RFC 5802 section 5's and RFC 7677 section 3's example exchanges.</summary>
[TestClass]
public sealed class XmppScramExchangeTests
{
    private const string Sha1Nonce = "fyko+d2lbbFgONRv9qkxdawL";

    private const string Sha1ServerFirst = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096";

    private const string Sha1ServerFinal = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=";

    private const string Sha256Nonce = "rOprNGfwEbeRWgbNEkqO";

    [TestMethod]
    public void Respond_Rfc5802Example_GivesItsClientFinalMessageAndAcceptsItsServerSignature()
    {
        XmppScramExchange exchange = Sha1();

        string clientFinal = exchange.Respond(Sha1ServerFirst);
        exchange.Complete(Sha1ServerFinal);

        Assert.AreEqual("n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL", exchange.InitialResponse);
        Assert.AreEqual("c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=", clientFinal);
    }

    [TestMethod]
    public void Respond_Rfc7677Example_GivesItsClientFinalMessageAndAcceptsItsServerSignature()
    {
        XmppScramExchange exchange = new(HashAlgorithmName.SHA256, "user", "pencil", Sha256Nonce);

        string clientFinal = exchange.Respond("r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096");
        exchange.Complete("v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=");

        Assert.AreEqual("n,,n=user,r=rOprNGfwEbeRWgbNEkqO", exchange.InitialResponse);
        Assert.AreEqual("c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=", clientFinal);
    }

    [TestMethod]
    public void Respond_ServerFinalAsSecondChallenge_AnswersEmptyAndCompletesWithoutData()
    {
        XmppScramExchange exchange = Sha1();
        exchange.Respond(Sha1ServerFirst);

        string answer = exchange.Respond(Sha1ServerFinal);
        exchange.Complete(string.Empty);

        Assert.AreEqual(string.Empty, answer);
    }

    [TestMethod]
    public void InitialResponse_UserNameWithEqualsAndComma_EscapesThemAsSaslname() =>
        Assert.AreEqual("n,,n=a=3Db=2Cc,r=x", new XmppScramExchange(HashAlgorithmName.SHA1, "a=b,c", "p", "x").InitialResponse);

    [TestMethod]
    [DataRow("s=QSXCR+Q6sek8bf92,i=4096", DisplayName = "no nonce")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL,s=QSXCR+Q6sek8bf92,i=4096", DisplayName = "no server part")]
    [DataRow("r=other3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096", DisplayName = "not the client's nonce")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL3rfc,i=4096", DisplayName = "no salt")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL3rfc,s=!!,i=4096", DisplayName = "salt not base64")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL3rfc,s=QSXCR+Q6sek8bf92", DisplayName = "no iteration count")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL3rfc,s=QSXCR+Q6sek8bf92,i=x", DisplayName = "iteration count not a number")]
    [DataRow("r=fyko+d2lbbFgONRv9qkxdawL3rfc,s=QSXCR+Q6sek8bf92,i=0,x,", DisplayName = "iteration count zero")]
    public void Respond_MalformedServerFirst_FailsAuthentication(string serverFirst)
    {
        XmppClientException failure = Assert.ThrowsExactly<XmppClientException>(() => Sha1().Respond(serverFirst));

        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("XMPP authentication failed: malformed SCRAM server-first-message", failure.Message);
    }

    [TestMethod]
    [DataRow("v=AAAAAAAAAAAAAAAAAAAAAAAAAAA=", "server signature mismatch", DisplayName = "wrong signature")]
    [DataRow("v=!!", "server signature mismatch", DisplayName = "signature not base64")]
    [DataRow("", "server signature mismatch", DisplayName = "no signature")]
    [DataRow("e=invalid-proof", "invalid-proof", DisplayName = "server error")]
    public void Complete_ServerFinalWithoutTheExpectedSignature_FailsAuthentication(string serverFinal, string condition)
    {
        XmppScramExchange exchange = Sha1();
        exchange.Respond(Sha1ServerFirst);

        XmppClientException failure = Assert.ThrowsExactly<XmppClientException>(() => exchange.Complete(serverFinal));

        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual($"XMPP authentication failed: {condition}", failure.Message);
    }

    [TestMethod]
    public void Complete_BeforeAnyChallenge_FailsAuthentication() =>
        Assert.ThrowsExactly<XmppClientException>(() => Sha1().Complete(Sha1ServerFinal));

    [TestMethod]
    public void CreateClientNonce_Twice_GivesTwoDifferentNoncesWithoutAComma()
    {
        string first = XmppScramExchange.CreateClientNonce();

        string second = XmppScramExchange.CreateClientNonce();

        Assert.AreNotEqual(first, second);
        Assert.DoesNotContain(",", first);
        Assert.AreEqual(24, first.Length);
    }

    private static XmppScramExchange Sha1() => new(HashAlgorithmName.SHA1, "user", "pencil", Sha1Nonce);
}
