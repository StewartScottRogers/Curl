using System.Text;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpRemotePath" />: the decoding and home-directory rule measured
/// 2026-09-29 (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed class SftpRemotePathTests
{
    [TestMethod]
    [DataRow("/a%20b.txt", "2F6120622E747874", DisplayName = "escaped space")]
    [DataRow("/x%2fy", "2F782F79", DisplayName = "lower-case escape")]
    [DataRow("/%FF", "2FFF", DisplayName = "escape above ASCII stays one byte")]
    [DataRow("/é", "2FC3A9", DisplayName = "unescaped character as UTF-8")]
    [DataRow("/100%", "2F31303025", DisplayName = "percent at the end")]
    [DataRow("/1%4", "2F312534", DisplayName = "one digit at the end")]
    [DataRow("/%zz", "2F257A7A", DisplayName = "not hex digits")]
    [DataRow("/%4g", "2F253467", DisplayName = "second not a hex digit")]
    public void Decode_UrlPath_TurnsEscapesIntoBytes(string urlPath, string expectedHex)
    {
        CollectionAssert.AreEqual(Convert.FromHexString(expectedHex), SftpRemotePath.Decode(urlPath));
    }

    [TestMethod]
    [DataRow("/~/f", "/home/fake/f", DisplayName = "home prefix")]
    [DataRow("/~/", "/home/fake/", DisplayName = "home prefix alone")]
    [DataRow("/~", "/~", DisplayName = "tilde without a slash")]
    [DataRow("/x/~/f", "/x/~/f", DisplayName = "tilde further in")]
    public void Resolve_Path_ReplacesOnlyALeadingHomePrefix(string path, string expected)
    {
        byte[] resolved = SftpRemotePath.Resolve(Encoding.UTF8.GetBytes(path), Encoding.UTF8.GetBytes("/home/fake"));

        Assert.AreEqual(expected, Encoding.UTF8.GetString(resolved));
    }
}
