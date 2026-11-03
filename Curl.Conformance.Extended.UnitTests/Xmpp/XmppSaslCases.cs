using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Xmpp;
using static Curl.Conformance.Extended.UnitTests.Xmpp.XmppRun;

namespace Curl.Conformance.Extended.UnitTests.Xmpp;

/// <summary>RFC 6120, 6: SASL mechanism choice, PLAIN, ANONYMOUS, SCRAM-SHA-1 and SCRAM-SHA-256, and <c>&lt;failure/&gt;</c>.</summary>
[TestClass]
public sealed class XmppSaslCases
{
    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-6.4.2-1")]
    [SpecRequirement("Xmpp:RFC7677-3-1")]
    [SpecRequirement("Xmpp:RFC6120-6.3.10-1")]
    public async Task ScramSha256_OfferedLastByTheServer_IsChosenAndItsProofVerifies()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { Mechanisms = ["PLAIN", "SCRAM-SHA-1", "SCRAM-SHA-256"] });

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        StringAssert.Contains(server.Received[0], "mechanism=\"SCRAM-SHA-256\"");
        Assert.IsTrue(server.InitialResponse.StartsWith("n,,n=alice,r=", StringComparison.Ordinal), server.InitialResponse);
        Assert.IsTrue(server.ProofVerified);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC5802-5-1")]
    public async Task ScramSha1_OfferedWithPlain_IsChosenAndItsProofVerifies()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { Mechanisms = ["PLAIN", "SCRAM-SHA-1"] });

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        StringAssert.Contains(server.Received[0], "mechanism=\"SCRAM-SHA-1\"");
        Assert.IsTrue(server.ProofVerified);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC4616-2-1")]
    public async Task Plain_OnlyMechanismOffered_SendsEmptyAuthzidUserAndPassword()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { Mechanisms = ["PLAIN"] });

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual("\0alice\0secret", server.InitialResponse);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC4505-2-1")]
    [SpecRequirement("Xmpp:RFC6120-6.4.2-2")]
    public async Task Anonymous_WithoutUser_SendsAnEmptyInitialResponseAsEquals()
    {
        XmppReferenceServer server = new(new XmppReferenceScript());

        CurlRunResult result = await RunAsync(server, "-d", "hi", Url);

        AssertExit(result, 0, string.Empty);
        Assert.AreEqual("<auth mechanism=\"ANONYMOUS\" xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\">=</auth>", server.Received[0]);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC5802-3-1")]
    public async Task Scram_ServerSignatureForged_FailsWithLoginDenied()
    {
        XmppReferenceServer server = new(new XmppReferenceScript { ForgeServerSignature = true });

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        Assert.AreEqual(67, result.ExitCode);
        StringAssert.StartsWith(System.Text.Encoding.UTF8.GetString(result.StandardError), "curl: (67) XMPP authentication failed: ");
        Assert.AreEqual(2, server.Received.Count);
    }

    [TestMethod]
    [SpecRequirement("Xmpp:RFC6120-6.5-1")]
    [DataRow("aborted")]
    [DataRow("account-disabled")]
    [DataRow("credentials-expired")]
    [DataRow("encryption-required")]
    [DataRow("incorrect-encoding")]
    [DataRow("invalid-authzid")]
    [DataRow("invalid-mechanism")]
    [DataRow("malformed-request")]
    [DataRow("mechanism-too-weak")]
    [DataRow("not-authorized")]
    [DataRow("temporary-auth-failure")]
    public async Task Failure_EachDefinedCondition_FailsWithLoginDeniedAndTheCondition(string condition)
    {
        XmppReferenceServer server = new(new XmppReferenceScript { FailureCondition = condition });

        CurlRunResult result = await RunAsync(server, "-u", "alice:secret", "-d", "hi", Url);

        AssertExit(result, 67, $"curl: (67) XMPP authentication failed: {condition}\n");
    }
}
