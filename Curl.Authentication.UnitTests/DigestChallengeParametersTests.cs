namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="DigestChallengeParameters" /> to curl 8.21.0's <c>qop</c> list reading
/// and blank skipping.
/// </summary>
[TestClass]
public sealed class DigestChallengeParametersTests
{
    [TestMethod]
    [DataRow("auth", "auth")]
    [DataRow("AUTH-INT", "auth-int")]
    [DataRow("auth-int, auth", "auth")]
    [DataRow(" auth-int,\tAuth", "auth")]
    [DataRow("auth-conf", null)]
    [DataRow("auth ", null, DisplayName = "Trailing blanks are part of the token")]
    [DataRow(",auth", null, DisplayName = "An empty token ends the list")]
    [DataRow("auth,", "auth", DisplayName = "A trailing comma")]
    [DataRow("abcdefghijabcdefghijabcdefghijabc,auth", null, DisplayName = "A 33-character token ends the list")]
    [DataRow("abcdefghijabcdefghijabcdefghijab,auth", "auth", DisplayName = "A 32-character token does not")]
    [DataRow("", null)]
    public void ReadQop_List_ChoosesAsCurlDoes(string list, string? expected)
    {
        Assert.AreEqual(expected, DigestChallengeParameters.ReadQop(list));
    }

    [TestMethod]
    [DataRow("", 0, 0)]
    [DataRow(" \t x", 0, 3)]
    [DataRow("a  ", 1, 3)]
    [DataRow("\r\n", 0, 0, DisplayName = "Line breaks are not blanks")]
    public void SkipBlanks_Text_StopsAtTheFirstNonBlank(string text, int index, int expected)
    {
        Assert.AreEqual(expected, DigestChallengeParameters.SkipBlanks(text, index));
    }
}
