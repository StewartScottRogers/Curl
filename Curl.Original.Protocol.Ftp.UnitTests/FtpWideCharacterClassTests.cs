namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Drives the internal <c>FtpWideCharacterClass</c> through <see cref="FtpWildcardPattern.Match" />:
/// each POSIX class a system <c>fnmatch</c> names, tested on code points as in a UTF-8 locale
/// (BL-2480). Non-ASCII names are UTF-8 bytes written one Latin-1 character per byte.
/// </summary>
[TestClass]
public sealed class FtpWideCharacterClassTests
{
    [TestMethod]
    [DataRow("alnum", "a", true)]
    [DataRow("alnum", "-", false)]
    [DataRow("alpha", "Ã©", true)]
    [DataRow("alpha", "9", false)]
    [DataRow("blank", "\t", true)]
    [DataRow("blank", " ", true)]
    [DataRow("blank", "x", false)]
    [DataRow("cntrl", "\u0001", true)]
    [DataRow("cntrl", "a", false)]
    [DataRow("digit", "5", true)]
    [DataRow("digit", "/", false)]
    [DataRow("digit", ":", false)]
    [DataRow("graph", "x", true)]
    [DataRow("graph", " ", false)]
    [DataRow("graph", "\u0001", false)]
    [DataRow("lower", "l", true)]
    [DataRow("lower", "L", false)]
    [DataRow("print", "a", true)]
    [DataRow("print", "\u0001", false)]
    [DataRow("print", "Í¸", false)]
    [DataRow("punct", ",", true)]
    [DataRow("punct", "a", false)]
    [DataRow("punct", " ", false)]
    [DataRow("punct", "\u0001", false)]
    [DataRow("space", " ", true)]
    [DataRow("space", "x", false)]
    [DataRow("upper", "U", true)]
    [DataRow("upper", "u", false)]
    [DataRow("xdigit", "f", true)]
    [DataRow("xdigit", "g", false)]
    public void Match_ClassOnLinuxAndMacOs_HoldsAsIswctypeInAUtf8Locale(string name, string fileName, bool holds)
    {
        FtpNameMatchAnswer expected = holds ? FtpNameMatchAnswer.Match : FtpNameMatchAnswer.NoMatch;

        Assert.AreEqual(expected, FtpWildcardPattern.Match("[[:" + name + ":]]", fileName, FtpWildcardMatcher.Linux));
        Assert.AreEqual(expected, FtpWildcardPattern.Match("[[:" + name + ":]]", fileName, FtpWildcardMatcher.MacOs));
    }
}
