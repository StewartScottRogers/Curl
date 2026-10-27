using System.Globalization;
using System.Text;
using Curl.Kerberos;

namespace Curl.Authentication;

/// <summary>
/// CGR-078: a path or query character above <c>~</c> is signed in the bytes the request line
/// sends, the argument's code page, so curl 8.21.0 on Windows (cp1252, the same bytes as
/// Latin-1 here) sends <c>GET /%FC</c> for <c>http://127.0.0.1:20571/ü</c> and signs that path,
/// and sends <c>GET /p?k=</c> and the byte <c>0xE4</c> for <c>?k=ä</c> and signs <c>k=%E4</c>
/// (measured 2026-10-26 with Record-CurlExchange.ps1, BL-3607).
/// </summary>
[TestClass]
public sealed class Cgr078Tests
{
    [TestMethod]
    [DataRow("/ü", null, "164bdd7fc297cf52b27491fecb31907f2011a6b09af53e8f0dba4a1decc1c3b9", DisplayName = "path")]
    [DataRow("/p", "k=ä", "bbb8659e120557985bb1179c26fd4102ae6e2d254d6e7b29327532e491f631fe", DisplayName = "query")]
    public void Sign_CharacterAboveTilde_SignsTheBytesTheRequestLineSends(string path, string? query, string signature)
    {
        AwsSigV4SigningResult result = Sign(path, query);

        StringAssert.EndsWith(result.HeaderLines[0], "Signature=" + signature);
    }

    [TestMethod]
    public void Sign_EscapedArgumentByteInPath_SignsTheByteItself()
    {
        string path = "/" + (char)0xDCFC;

        AwsSigV4SigningResult result = Sign(path, null);

        StringAssert.EndsWith(result.HeaderLines[0], "Signature=164bdd7fc297cf52b27491fecb31907f2011a6b09af53e8f0dba4a1decc1c3b9");
    }

    [TestMethod]
    [DataRow("/\U000100FC", "/%F0%90%83%BC", DisplayName = "surrogate pair whose low half is in the escaped range")]
    [DataRow("/\U0001F600", "/%F0%9F%98%80", DisplayName = "surrogate pair")]
    public void CanonicalPath_S3PathAboveTilde_IsThePathTheRequestLineSends(string path, string expected)
    {
        string canonical = AwsSigV4UriEncoding.CanonicalPath(path, "s3", Encoding.UTF8);

        Assert.AreEqual(expected, canonical);
    }

    [TestMethod]
    public void CanonicalPath_S3PathStartingWithEscapedByte_IsTheByteItself()
    {
        string path = (char)0xDCFC + "/a";

        string canonical = AwsSigV4UriEncoding.CanonicalPath(path, "s3", Encoding.UTF8);

        Assert.AreEqual("%FC/a", canonical);
    }

    private static AwsSigV4SigningResult Sign(string path, string? query)
    {
        AwsSigV4Request request = new()
        {
            SigV4Parameter = "aws:amz:us-east-1:s3",
            UserName = "AKID",
            Password = "SECRET",
            Method = "GET",
            HostName = "127.0.0.1",
            HostHeaderValue = "127.0.0.1:20571",
            Path = path,
            Query = query,
            CustomHeaders = ["X-Amz-Date: 20250101T000000Z"],
            IsGetOrHead = true,
        };
        DateTimeOffset now = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);

        return new AwsSigV4Signer(new FixedTimeProvider(now), Encoding.Latin1).Sign(request);
    }
}
