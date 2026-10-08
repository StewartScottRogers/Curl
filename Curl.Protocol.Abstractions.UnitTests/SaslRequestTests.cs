using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that a <see cref="SaslRequest" /> carries every member ADR-0121 gives it, and
/// compares by value.
/// </summary>
[TestClass]
public sealed class SaslRequestTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsEveryMember()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var credential = new NetworkCredential("u", "p");
        diagnostics.Arrange("credential user", credential.UserName);
        diagnostics.Arrange("authorization identity", "zid");
        diagnostics.Arrange("mechanism", "PLAIN");
        diagnostics.Arrange("service", "smtp://mail.example:587");

        var request = new SaslRequest(credential, "zid", "token", "PLAIN", "smtp", "mail.example", 587);

        diagnostics.Act("authorization identity", request.AuthorizationIdentity);
        diagnostics.Act("required mechanism", request.RequiredMechanism);
        diagnostics.Act("service name", request.ServiceName);
        diagnostics.Act("host", request.Host);
        diagnostics.Act("port", request.Port);
        diagnostics.Assert("port", 587, request.Port);
        Assert.AreSame(credential, request.Credential);
        Assert.AreEqual("zid", request.AuthorizationIdentity);
        Assert.AreEqual("token", request.BearerToken);
        Assert.AreEqual("PLAIN", request.RequiredMechanism);
        Assert.AreEqual("smtp", request.ServiceName);
        Assert.AreEqual("mail.example", request.Host);
        Assert.AreEqual(587, request.Port);
    }

    [TestMethod]
    public void Constructor_WithoutPort_HoldsZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service", "imap://mail.example");

        var request = new SaslRequest(null, null, null, null, "imap", "mail.example");

        diagnostics.Act("port", request.Port);
        diagnostics.Assert("port", 0, request.Port);
        Assert.AreEqual(0, request.Port);
    }

    [TestMethod]
    public void Equals_ForTheSameRequestToAnotherPort_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var submission = new SaslRequest(null, null, null, null, "smtp", "mail.example", 587);
        diagnostics.Arrange("submission", submission);
        diagnostics.Arrange("other port", 25);

        bool equal = submission.Equals(submission with { Port = 25 });

        diagnostics.Act("equal", equal);
        diagnostics.Assert("equal", false, equal);
        Assert.AreNotEqual(submission, submission with { Port = 25 });
    }

    [TestMethod]
    public void Constructor_WithNothingOptional_HoldsNulls()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("service", "imap://mail.example");

        var request = new SaslRequest(null, null, null, null, "imap", "mail.example");

        diagnostics.Act("credential", request.Credential);
        diagnostics.Act("authorization identity", request.AuthorizationIdentity);
        diagnostics.Act("bearer token", request.BearerToken);
        diagnostics.Act("required mechanism", request.RequiredMechanism);
        diagnostics.Assert("credential", null, request.Credential);
        Assert.IsNull(request.Credential);
        Assert.IsNull(request.AuthorizationIdentity);
        Assert.IsNull(request.BearerToken);
        Assert.IsNull(request.RequiredMechanism);
    }

    [TestMethod]
    public void Equals_ForTheSameRequestToAnotherService_ReturnsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var smtp = new SaslRequest(null, null, null, null, "smtp", "mail.example");
        var pop = smtp with { ServiceName = "pop" };
        diagnostics.Arrange("smtp", smtp);
        diagnostics.Arrange("pop", pop);

        bool differ = !smtp.Equals(pop);
        bool sameContentEqual = smtp.Equals(new SaslRequest(null, null, null, null, "smtp", "mail.example"));

        diagnostics.Act("differ", differ);
        diagnostics.Act("same content equal", sameContentEqual);
        diagnostics.Assert("differ", true, differ);
        diagnostics.Assert("same content equal", true, sameContentEqual);
        Assert.AreNotEqual(smtp, pop);
        Assert.AreEqual(smtp, new SaslRequest(null, null, null, null, "smtp", "mail.example"));
    }
}
