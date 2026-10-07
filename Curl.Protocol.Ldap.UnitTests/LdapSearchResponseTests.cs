using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

[TestClass]
public sealed class LdapSearchResponseTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decode_SearchResultDone_IsDoneWithItsCodeAndDiagnosticMessage()
    {
        byte[] message = Hex.Bytes("30 10 02 01 02 65 0b 0a 01 20 04 00 04 04 6f 6f 70 73");
        Diagnostics.Bytes("message", message);
        Diagnostics.Arrange("message ID", 2);

        LdapSearchReply reply = LdapSearchResponse.Decode(Hex.Bytes("30 10 02 01 02 65 0b 0a 01 20 04 00 04 04 6f 6f 70 73"), 2);

        Diagnostics.Act("reply", reply);
        Diagnostics.Assert("reply", new LdapSearchReply(LdapSearchReplyKind.Done, 32, "oops"), reply);
        Assert.AreEqual(new LdapSearchReply(LdapSearchReplyKind.Done, 32, "oops"), reply);
        Assert.IsFalse(reply.IsSuccess);
    }

    [TestMethod]
    [DataRow("00", true)]
    [DataRow("04", true)]
    [DataRow("03", false)]
    public void IsSuccess_IsSuccessOrSizeLimitExceeded(string resultCode, bool expected)
    {
        Diagnostics.Arrange("result code", resultCode);
        Diagnostics.Bytes("message", Hex.Bytes($"30 0c 02 01 02 65 07 0a 01 {resultCode} 04 00 04 00"));

        LdapSearchReply reply = LdapSearchResponse.Decode(Hex.Bytes($"30 0c 02 01 02 65 07 0a 01 {resultCode} 04 00 04 00"), 2);

        Diagnostics.Act("reply", reply);
        Diagnostics.Assert("IsSuccess", expected, reply.IsSuccess);
        Assert.AreEqual(expected, reply.IsSuccess);
    }

    [TestMethod]
    public void IsSuccess_ReplyThatIsNotTheDone_IsFalse()
    {
        Diagnostics.Arrange("reply kind", LdapSearchReplyKind.Entry);
        Diagnostics.Act("IsSuccess", LdapSearchReply.Of(LdapSearchReplyKind.Entry).IsSuccess);
        Diagnostics.Assert("IsSuccess", false, LdapSearchReply.Of(LdapSearchReplyKind.Entry).IsSuccess);
        Assert.IsFalse(LdapSearchReply.Of(LdapSearchReplyKind.Entry).IsSuccess);
    }

    [TestMethod]
    [DataRow("30 0f 02 01 02 64 0a 04 04 64 63 3d 78 30 02 30 00", (int)LdapSearchReplyKind.Entry)]
    [DataRow("30 09 02 01 02 73 04 04 02 6c 3a", (int)LdapSearchReplyKind.OtherResponse)]
    [DataRow("30 05 02 01 02 04 00", (int)LdapSearchReplyKind.OtherResponse)]
    [DataRow("30 0c 02 01 07 65 07 0a 01 00 04 00 04 00", (int)LdapSearchReplyKind.OtherMessage)]
    [DataRow("30 09 02 05 01 00 00 00 00 42 00", (int)LdapSearchReplyKind.Lost)]
    [DataRow("30 03 04 01 00", (int)LdapSearchReplyKind.Lost)]
    [DataRow("30 0c 02 01 02 65 07 0a 01 ff 04 00 04 00", (int)LdapSearchReplyKind.Lost)]
    [DataRow("30 10 02 01 02 65 0b 0a 05 01 00 00 00 00 04 00 04 00", (int)LdapSearchReplyKind.Lost)]
    [DataRow("30 08 02 01 02 65 03 0a 01 00", (int)LdapSearchReplyKind.Lost)]
    [DataRow("30 03 02 01 02 00", (int)LdapSearchReplyKind.Lost)]
    public void Decode_OtherMessage_IsItsKind(string message, int expected)
    {
        Diagnostics.Bytes("message", Hex.Bytes(message));
        Diagnostics.Arrange("message ID", 2);
        Diagnostics.Act("kind", LdapSearchResponse.Decode(Hex.Bytes(message), 2).Kind);
        Diagnostics.Assert("kind", (LdapSearchReplyKind)expected, LdapSearchResponse.Decode(Hex.Bytes(message), 2).Kind);
        Assert.AreEqual((LdapSearchReplyKind)expected, LdapSearchResponse.Decode(Hex.Bytes(message), 2).Kind);
    }
}
