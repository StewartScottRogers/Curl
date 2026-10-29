namespace Curl.Networking;

/// <summary>
/// CGR-292 (BL-3821): the OpenSSL build's <c>SSL_read</c> failure texts name the Linux reference
/// build's OpenSSL, 3.5.5, not a different version.
/// </summary>
[TestClass]
public sealed class Cgr292Tests
{
    [TestMethod]
    public void MissingCloseNotify_OpenSslBuild_NamesOpenSsl355()
    {
        Assert.AreEqual(
            "OpenSSL SSL_read: OpenSSL/3.5.5: error:0A000126:SSL routines::unexpected eof while reading, errno 0",
            TlsFailureMessages.MissingCloseNotify(matchesSchannelBuild: false));
    }
}
