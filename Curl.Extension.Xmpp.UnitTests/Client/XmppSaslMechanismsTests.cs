using System.Net;

namespace Curl.Extension.Xmpp.Client;

/// <summary>Checks which registered SASL mechanisms apply, and in which order.</summary>
[TestClass]
public sealed class XmppSaslMechanismsTests
{
    [TestMethod]
    public void For_Credentials_GivesScramSha256ThenScramSha1ThenPlain() =>
        CollectionAssert.AreEqual(
            new[] { "SCRAM-SHA-256", "SCRAM-SHA-1", "PLAIN" },
            XmppSaslMechanisms.For(new NetworkCredential("u", "p")).Select(mechanism => mechanism.Name).ToArray());

    [TestMethod]
    public void For_NoCredentials_GivesAnonymousOnly() =>
        CollectionAssert.AreEqual(new[] { "ANONYMOUS" }, XmppSaslMechanisms.For(null).Select(mechanism => mechanism.Name).ToArray());
}
