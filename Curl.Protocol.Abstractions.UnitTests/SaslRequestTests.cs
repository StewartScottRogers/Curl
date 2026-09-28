using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that a <see cref="SaslRequest" /> carries every member ADR-0121 gives it, and
/// compares by value.
/// </summary>
[TestClass]
public sealed class SaslRequestTests
{
    [TestMethod]
    public void Constructor_RoundTripsEveryMember()
    {
        var credential = new NetworkCredential("u", "p");

        var request = new SaslRequest(credential, "zid", "token", "PLAIN", "smtp", "mail.example");

        Assert.AreSame(credential, request.Credential);
        Assert.AreEqual("zid", request.AuthorizationIdentity);
        Assert.AreEqual("token", request.BearerToken);
        Assert.AreEqual("PLAIN", request.RequiredMechanism);
        Assert.AreEqual("smtp", request.ServiceName);
        Assert.AreEqual("mail.example", request.Host);
    }

    [TestMethod]
    public void Constructor_WithNothingOptional_HoldsNulls()
    {
        var request = new SaslRequest(null, null, null, null, "imap", "mail.example");

        Assert.IsNull(request.Credential);
        Assert.IsNull(request.AuthorizationIdentity);
        Assert.IsNull(request.BearerToken);
        Assert.IsNull(request.RequiredMechanism);
    }

    [TestMethod]
    public void Equals_ForTheSameRequestToAnotherService_ReturnsFalse()
    {
        var smtp = new SaslRequest(null, null, null, null, "smtp", "mail.example");
        var pop = smtp with { ServiceName = "pop" };

        Assert.AreNotEqual(smtp, pop);
        Assert.AreEqual(smtp, new SaslRequest(null, null, null, null, "smtp", "mail.example"));
    }
}
