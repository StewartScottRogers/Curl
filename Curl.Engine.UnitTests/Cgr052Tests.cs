using System.Net;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-052: curl 8.22.0 on Windows sends the bytes a URL's user name and password percent-decode to,
/// and a netrc file's bytes, as they are: <c>curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20581/a"</c>
/// sends <c>Authorization: Basic w6k64oKs</c> (C3 A9 3A E2 82 AC), not the ANSI code page's
/// <c>Basic 6TqA</c>, while a raw <c>é</c> on the command line still goes as the ANSI byte E9
/// (<c>Basic 6To=</c>), as the arguments reach curl in that code page (measured, ADR-0910).
/// </summary>
[TestClass]
public sealed class Cgr052Tests
{
    private static readonly Encoding WindowsCredentialEncoding = CredentialEncoding.ForPlatform(isWindows: true);

    [TestMethod]
    public void TryLookUp_PercentEncodedUtf8OnWindows_SendsTheDecodedBytes()
    {
        NetworkCredential credentials = LookUp(WindowsCredentialEncoding, "http://%C3%A9:%E2%82%AC@h/");

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9, 0x3A, 0xE2, 0x82, 0xAC }, SentBytes(WindowsCredentialEncoding, credentials));
    }

    [TestMethod]
    public void TryLookUp_PercentEncodedUtf8InUtf8_SendsTheDecodedBytes()
    {
        NetworkCredential credentials = LookUp(Encoding.UTF8, "http://%C3%A9:%E2%82%AC@h/");

        Assert.AreEqual("é:€", credentials.UserName + ":" + credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_RawCharacterOnWindows_SendsItsAnsiByte()
    {
        NetworkCredential credentials = LookUp(WindowsCredentialEncoding, "http://é@h/");

        CollectionAssert.AreEqual(new byte[] { 0xE9 }, WindowsCredentialEncoding.GetBytes(credentials.UserName));
    }

    [TestMethod]
    [DataRow("http://a%zz@h/", "a%zz")]
    [DataRow("http://a%4@h/", "a%4")]
    [DataRow("http://a%@h/", "a%")]
    [DataRow("http://%41%62@h/", "Ab")]
    public void TryLookUp_EscapeNotFollowedByTwoHexDigits_KeepsThePercentSign(string url, string expected)
    {
        NetworkCredential credentials = LookUp(WindowsCredentialEncoding, url);

        Assert.AreEqual(expected, credentials.UserName);
    }

    [TestMethod]
    public void TryLookUp_Utf8NetrcEntryOnWindows_SendsTheFilesBytesAndMatchesTheUrlUser()
    {
        byte[] netrc = Encoding.UTF8.GetBytes("machine h login é password €\n");
        TransferCredentialLookup lookup = new(_ => netrc, _ => null, runsOnWindows: true, credentialEncoding: WindowsCredentialEncoding);
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Required, NetrcFile = "netrc" };

        Assert.IsTrue(lookup.TryLookUp(options, CurlUrl.Parse("http://%C3%A9@h/"), out NetworkCredential? credentials, out _));

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9, 0x3A, 0xE2, 0x82, 0xAC }, SentBytes(WindowsCredentialEncoding, credentials!));
    }

    private static NetworkCredential LookUp(Encoding encoding, string url)
    {
        TransferCredentialLookup lookup = new(_ => null, _ => null, runsOnWindows: true, credentialEncoding: encoding);
        Assert.IsTrue(lookup.TryLookUp(new EasyAuthenticationOptions(), CurlUrl.Parse(url), out NetworkCredential? credentials, out _));
        return credentials!;
    }

    private static byte[] SentBytes(Encoding encoding, NetworkCredential credentials) =>
        encoding.GetBytes(credentials.UserName + ":" + credentials.Password);
}
