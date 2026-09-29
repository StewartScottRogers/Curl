using System.Text;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins <see cref="SmbUrlPath" /> to curl 8.21.0's <c>smb_parse_url_path</c>.
/// </summary>
[TestClass]
public sealed class SmbUrlPathTests
{
    [TestMethod]
    [DataRow("/share/x.txt", "share", "x.txt")]
    [DataRow("/share/dir/x.txt", "share", @"dir\x.txt")]
    [DataRow(@"/share\dir\x.txt", "share", @"dir\x.txt")]
    [DataRow("/sh%61re/a%2Fb", "share", @"a\b")]
    [DataRow("/s/100%zz%4", "s", "100%zz%4")]
    [DataRow("//x", "", "x")]
    [DataRow("s/x", "s", "x")]
    public void TryParse_SplitsShareAndFileAsCurl(string absolutePath, string share, string filePath)
    {
        Assert.IsNull(SmbUrlPath.TryParse(absolutePath, out SmbUrlPath? path));

        Assert.AreEqual(share, Encoding.UTF8.GetString(path!.Share));
        Assert.AreEqual(filePath, Encoding.UTF8.GetString(path.FilePath));
    }

    [TestMethod]
    [DataRow("/x.txt", "missing share in URL path for SMB")]
    [DataRow("", "missing share in URL path for SMB")]
    [DataRow("/s/a%01b", "URL using bad/illegal format or missing URL")]
    public void TryParse_RefusesAsCurl(string absolutePath, string expected)
    {
        Assert.AreEqual(expected, SmbUrlPath.TryParse(absolutePath, out SmbUrlPath? path));
        Assert.IsNull(path);
    }

}
