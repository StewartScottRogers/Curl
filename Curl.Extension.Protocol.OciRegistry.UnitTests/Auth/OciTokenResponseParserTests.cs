using System.Text;

namespace Curl.Extension.Protocol.OciRegistry.Auth;

/// <summary>Pins the token answer against the Docker/Distribution token authentication specification, "Token Response Fields".</summary>
[TestClass]
public sealed class OciTokenResponseParserTests
{
    [TestMethod]
    [DataRow("""{"token":"abc","access_token":"def","expires_in":300}""", "abc", DisplayName = "token first")]
    [DataRow("""{"access_token":"def"}""", "def", DisplayName = "access_token alone")]
    [DataRow("""{"token":"","access_token":"def"}""", "def", DisplayName = "Empty token falls back")]
    [DataRow("""{"token":1,"access_token":"def"}""", "def", DisplayName = "Non-string token falls back")]
    public void ParseToken_TokenAnswer_ReturnsTheToken(string json, string expected)
    {
        var token = OciTokenResponseParser.ParseToken(Encoding.UTF8.GetBytes(json));

        Assert.AreEqual(expected, token);
    }

    [TestMethod]
    [DataRow("""{"expires_in":300}""", DisplayName = "Neither member")]
    [DataRow("""["token"]""", DisplayName = "Not an object")]
    [DataRow("""{"token":""", DisplayName = "Not JSON")]
    [DataRow("", DisplayName = "Empty body")]
    public void ParseToken_NoToken_ReturnsNull(string json)
    {
        var token = OciTokenResponseParser.ParseToken(Encoding.UTF8.GetBytes(json));

        Assert.IsNull(token);
    }
}
