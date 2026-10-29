using System.Net;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-053: a URL credential escape that is not valid UTF-8 is sent as its byte.
/// <c>curl -sS "http://user:pa%FFss@127.0.0.1:20582/a"</c> sends
/// <c>Authorization: Basic dXNlcjpwYf9zcw==</c> (<c>user:pa\xFFss</c>) on curl 8.22.0 Schannel
/// (measured 2026-10-26) and curl 8.18.0 OpenSSL on Linux (the gap report), where Curl sent
/// U+FFFD's bytes off Windows (ADR-0920).
/// </summary>
[TestClass]
public sealed class Cgr053Tests
{
    private static readonly byte[] Expected = [.. "user:pa"u8, 0xFF, .. "ss"u8];

    [TestMethod]
    public void TryLookUp_InvalidUtf8EscapeOffWindows_SendsTheByte()
    {
        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: false);

        NetworkCredential credentials = LookUp(encoding, "http://user:pa%FFss@h/");

        CollectionAssert.AreEqual(Expected, SentBytes(encoding, credentials));
    }

    [TestMethod]
    public void TryLookUp_InvalidUtf8EscapeOnWindows_SendsTheByte()
    {
        Encoding encoding = CredentialEncoding.ForPlatform(isWindows: true);

        NetworkCredential credentials = LookUp(encoding, "http://user:pa%FFss@h/");

        CollectionAssert.AreEqual(Expected, SentBytes(encoding, credentials));
    }

    private static NetworkCredential LookUp(Encoding encoding, string url)
    {
        TransferCredentialLookup lookup = new(_ => null, _ => null, runsOnWindows: false, credentialEncoding: encoding);
        Assert.IsTrue(lookup.TryLookUp(new EasyAuthenticationOptions(), CurlUrl.Parse(url), out NetworkCredential? credentials, out _));
        return credentials!;
    }

    private static byte[] SentBytes(Encoding encoding, NetworkCredential credentials) =>
        encoding.GetBytes(credentials.UserName + ":" + credentials.Password);
}
