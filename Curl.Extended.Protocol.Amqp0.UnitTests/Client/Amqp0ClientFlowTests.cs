using System.Net;

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
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), Amqp0ClientFlow.PlainResponse(null));

    [TestMethod]
    public void PlainResponse_EmptyUserName_IsGuestGuest() =>
        CollectionAssert.AreEqual("\0guest\0guest"u8.ToArray(), Amqp0ClientFlow.PlainResponse(new NetworkCredential(string.Empty, "pw")));

    [TestMethod]
    public void PlainResponse_UserWithEmptyPassword_SendsTheEmptyPassword() =>
        CollectionAssert.AreEqual("\0me\0"u8.ToArray(), Amqp0ClientFlow.PlainResponse(new NetworkCredential("me", string.Empty)));

    [TestMethod]
    public void ClientProperties_AskForAuthenticationFailureClose()
    {
        var capabilities = (Wire.Amqp0FieldTable)Amqp0ClientFlow.ClientProperties.Find("capabilities")!.Value!;

        Assert.AreEqual(true, capabilities.Find("authentication_failure_close")!.Value);
    }
}
