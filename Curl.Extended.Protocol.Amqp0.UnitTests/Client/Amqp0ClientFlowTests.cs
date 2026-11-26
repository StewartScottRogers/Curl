using System.Net;
using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Client;

[TestClass]
public sealed class Amqp0ClientFlowTests
{
    [TestMethod]
    [DataRow(0u, 2047u, 2047u)]
    [DataRow(100u, 2047u, 100u)]
    [DataRow(262144u, 131072u, 131072u)]
    public void Negotiate_ServerAndClientLimits_TakesTheSmallerWhereZeroIsNone(uint server, uint client, uint expected) =>
        Assert.AreEqual(expected, Amqp0ClientFlow.Negotiate(server, client));

    [TestMethod]
    public void PlainResponse_NoCredentials_IsGuestGuest() =>
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), Amqp0Mechanism.Choose("PLAIN", null)!.Response);

    [TestMethod]
    public void PlainResponse_EmptyUserName_IsGuestGuest() =>
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), Amqp0Mechanism.Choose("PLAIN", new NetworkCredential(string.Empty, "pw"))!.Response);

    [TestMethod]
    public void PlainResponse_UserWithEmptyPassword_SendsTheEmptyPassword() =>
        CollectionAssert.AreEqual("\0me\0"u8.ToArray(), Amqp0Mechanism.Choose("PLAIN", new NetworkCredential("me", string.Empty))!.Response);

    [TestMethod]
    public void ClientProperties_AskForAuthenticationFailureClose()
    {
        var capabilities = (Wire.Amqp0FieldTable)Amqp0ClientFlow.ClientProperties.Find("capabilities")!.Value!;

        Assert.AreEqual(true, capabilities.Find("authentication_failure_close")!.Value);
    }

    [TestMethod]
    public void Choose_PlainOffered_IsPreferredEvenWithAnonymous()
    {
        Amqp0Mechanism chosen = Amqp0Mechanism.Choose("ANONYMOUS AMQPLAIN PLAIN", new NetworkCredential("u", "p"))!;

        Assert.AreEqual("PLAIN", chosen.Name);
    }

    [TestMethod]
    public void Choose_OnlyAnonymousAndNoUser_SendsAnEmptyResponse()
    {
        Amqp0Mechanism chosen = Amqp0Mechanism.Choose("ANONYMOUS", null)!;

        Assert.AreEqual("ANONYMOUS", chosen.Name);
        Assert.AreEqual(0, chosen.Response.Length);
    }

    [TestMethod]
    public void Choose_OnlyAnonymousAndAUser_IsNone() =>
        Assert.IsNull(Amqp0Mechanism.Choose("ANONYMOUS", new NetworkCredential("u", "p")));

    [TestMethod]
    public void Choose_AnonymousAndAmqplainWithAUser_IsAmqplainWithLoginAndPassword()
    {
        Amqp0Mechanism chosen = Amqp0Mechanism.Choose("ANONYMOUS AMQPLAIN", new NetworkCredential("me", "pw"))!;

        Assert.AreEqual("AMQPLAIN", chosen.Name);
        byte[] expected = [5, .. "LOGIN"u8, (byte)'S', 0, 0, 0, 2, .. "me"u8, 8, .. "PASSWORD"u8, (byte)'S', 0, 0, 0, 2, .. "pw"u8];
        CollectionAssert.AreEqual(expected, chosen.Response);
    }

    [TestMethod]
    public void Choose_OnlyAmqplainAndNoUser_LogsInAsGuest()
    {
        Amqp0Mechanism chosen = Amqp0Mechanism.Choose("AMQPLAIN", null)!;

        Assert.AreEqual("AMQPLAIN", chosen.Name);
        CollectionAssert.AreEqual(Amqp0FieldTableCodec.Encode(new([new("LOGIN", Amqp0FieldValue.LongString("guest")), new("PASSWORD", Amqp0FieldValue.LongString("guest"))])), chosen.Response);
    }

    [TestMethod]
    public void Choose_NothingUsable_IsNone() =>
        Assert.IsNull(Amqp0Mechanism.Choose("EXTERNAL CRAM-MD5", null));
}
