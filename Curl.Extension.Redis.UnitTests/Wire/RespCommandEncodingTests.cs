using System.Text;

namespace Curl.Extension.Redis.Wire;

/// <summary>Checks commands against RESP3's "Sending commands to a Redis server" and "Bulk strings".</summary>
[TestClass]
public sealed class RespCommandEncodingTests
{
    [TestMethod]
    public void Encode_Arguments_GivesAnArrayOfBulkStrings() =>
        Assert.AreEqual(
            "*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$7\r\nv\r\né !\r\n",
            Encoding.UTF8.GetString(RespCommandEncoding.Encode(RespCommandEncoding.Arguments("SET", "key", "v\r\né !"))));

    [TestMethod]
    public void Encode_EmptyArgument_GivesAZeroLengthBulkString() =>
        Assert.AreEqual("*2\r\n$4\r\nECHO\r\n$0\r\n\r\n", Encoding.UTF8.GetString(RespCommandEncoding.Encode(RespCommandEncoding.Arguments("ECHO", ""))));
}
