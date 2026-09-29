using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="LdapBindResponse.Decode(ReadOnlyMemory{byte}, int)" /> and
/// <see cref="LdapBindReply.IsSuccess" />: the resultCode of a BindResponse to the request,
/// and <see cref="LdapBindReplyStatus.Malformed" /> for anything else.
/// </summary>
[TestClass]
public sealed class LdapBindResponseTests
{
    [TestMethod]
    [DataRow("30 0c 02 01 01 61 07 0a 01 00 04 00 04 00", 0)]
    [DataRow("30 0c 02 01 01 61 07 0a 01 31 04 00 04 00", 49)]
    [DataRow("30 10 02 01 01 61 0b 0a 01 31 04 00 04 04 6e 6f 70 65", 49)]
    [DataRow("30 84 00 00 00 10 02 01 01 61 84 00 00 00 07 0a 01 35 04 00 04 00", 53)]
    [DataRow("30 0e 02 01 01 61 07 0a 01 00 04 00 04 00 87 00", 0)]
    [DataRow("30 10 02 01 01 61 07 0a 01 00 04 00 04 00 a0 02 30 00", 0)]
    [DataRow("30 0d 02 01 01 61 08 0a 02 00 c8 04 00 04 00", 200)]
    public void Decode_BindResponseToTheRequest_ReturnsItsResultCode(string message, int resultCode)
    {
        Assert.AreEqual(new LdapBindReply(LdapBindReplyStatus.Answered, resultCode), LdapBindResponse.Decode(Hex.Bytes(message), 1));
    }

    [TestMethod]
    [DataRow("30 0c 02 01 02 61 07 0a 01 00 04 00 04 00")]
    [DataRow("30 10 02 05 01 00 00 00 01 61 07 0a 01 00 04 00 04 00")]
    [DataRow("30 05 02 01 01 04 00")]
    [DataRow("30 0c 02 01 01 61 07 0a 01 ff 04 00 04 00")]
    [DataRow("30 10 02 01 01 61 0b 0a 05 01 00 00 00 00 04 00 04 00")]
    [DataRow("30 09 02 01 01 61 04 0a 01 00 04 00")]
    [DataRow("30 0c 02 01 01 61 07 0a 01 00 04 00 04 00 00")]
    [DataRow("30 0c 02 01 01 61 07 0a 01 00")]
    public void Decode_AnythingElse_ReturnsMalformed(string message)
    {
        Assert.AreEqual(new LdapBindReply(LdapBindReplyStatus.Malformed, 0), LdapBindResponse.Decode(Hex.Bytes(message), 1));
    }
}
