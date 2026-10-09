using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Adversarial black-box tests (BL-1492, the method in Documentation/Wiki/Adversarial-Testing.md):
/// Curl.Authentication.UnitLibrary's public surface attacked at its boundaries, with malformed
/// challenges, netrc text and SPNEGO tokens, and under repeated calls. The oracle is each
/// type's documented contract: an answer or a named refusal, never an undocumented exception.
/// </summary>
[TestClass]
public sealed class AuthenticationAdversarialTests
{
    private const string Host = "127.0.0.1";

    [TestMethod]
    [DataRow("Digest realm=\"r\", nonce=\"abc", DisplayName = "An unterminated quoted nonce")]
    [DataRow("Digest realm=\"r, nonce=\"abc\"", DisplayName = "An unterminated quoted realm swallowing the nonce")]
    [DataRow("Digest nonce=\"a\\", DisplayName = "A nonce ending in a lone backslash")]
    [DataRow("Digest nonce=", DisplayName = "A nonce with no value")]
    [DataRow("Digest =\"abc\"", DisplayName = "A parameter with no name")]
    [DataRow("Digest ,,,,", DisplayName = "Only commas")]
    [DataRow("Digest nonce=\"n\", algorithm=ROT13", DisplayName = "An unknown algorithm")]
    [DataRow("Digest nonce=\"n\", qop=\"bogus\"", DisplayName = "An unknown qop")]
    [DataRow("Digest nonce=\"n\", qop=\"\"", DisplayName = "An empty qop")]
    [DataRow("Digest nonce=\"n\", nonce=\"m\", realm=\"a\", realm=\"b\"", DisplayName = "Duplicate parameters")]
    [DataRow("Digest nonce=\"n\", x-unknown=\"\\\"\\\\\", stale=maybe", DisplayName = "Unknown parameters with escapes")]
    [DataRow("Digest nonce=\"n\u0000\u0001\r\n\"", DisplayName = "Control characters in the nonce")]
    [DataRow("Digest nonce=\"n\", userhash=\"\", charset=", DisplayName = "Empty userhash and charset")]
    [DataRow("Digest", DisplayName = "The scheme name alone")]
    [DataRow("", DisplayName = "An empty challenge")]
    public void DigestCreateAuthorization_MalformedChallenge_AnswersOrDeclinesWithoutThrowing(string challenge)
    {
        string? value = DigestAnswer("u", "p", challenge);

        Assert.IsTrue(value is null || value.StartsWith("Digest ", StringComparison.Ordinal), value);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "An empty nonce")]
    [DataRow(1, DisplayName = "A one-character nonce")]
    [DataRow(1023, DisplayName = "A nonce at the 1023-character value limit")]
    public void DigestCreateAuthorization_NonceOfBoundaryLength_EchoesTheNonce(int length)
    {
        string nonce = new('n', length);

        string? value = DigestAnswer("u", "p", "Digest realm=\"r\", nonce=\"" + nonce + "\"");

        Assert.IsNotNull(value);
        StringAssert.Contains(value, "nonce=\"" + nonce + "\"");
    }

    [TestMethod]
    [DataRow(1024, DisplayName = "A nonce one past the 1023-character value limit")]
    [DataRow(8192, DisplayName = "An 8 KiB nonce")]
    [DataRow(65536, DisplayName = "A 64 KiB nonce")]
    public void DigestCreateAuthorization_NoncePastTheValueLimit_EchoesItsFirst1023Characters(int length)
    {
        string nonce = new('n', length);

        string? value = DigestAnswer("u", "p", "Digest realm=\"r\", nonce=\"" + nonce + "\"");

        Assert.IsNotNull(value);
        StringAssert.Contains(value, "nonce=\"" + nonce[..1023] + "\"");
    }

    [TestMethod]
    public void DigestCreateAuthorization_ManyChallengesOnOneLine_AnswersTheFirst()
    {
        string challenges = string.Join(", ", Enumerable.Range(0, 1000).Select(i => "Digest realm=\"r" + i + "\", nonce=\"n" + i + "\""));

        string? value = DigestAnswer("u", "p", challenges);

        Assert.IsNotNull(value);
        StringAssert.Contains(value, "realm=\"r0\"");
    }

    [TestMethod]
    [DataRow("us:er", "p", DisplayName = "A colon in the user name")]
    [DataRow("u", "", DisplayName = "An empty password")]
    [DataRow("", "", DisplayName = "An empty user and password")]
    [DataRow("\"q\\uote\"", "p", DisplayName = "Quote and backslash in the user name")]
    [DataRow("Jäsøn\u0001\t", "pä\u0000", DisplayName = "Non-ASCII and control characters")]
    public void DigestCreateAuthorization_HostileCredentials_Answers(string user, string password)
    {
        string? value = DigestAnswer(user, password, "Digest realm=\"r\", nonce=\"n\", qop=\"auth\"");

        Assert.IsNotNull(value);
        StringAssert.StartsWith(value, "Digest username=");
    }

    [TestMethod]
    public void DigestCreateAuthorization_SameInputRepeated_GivesTheSameAnswer()
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "cnonce");
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), HttpAuthSchemes.Digest);
        string[] challenges = ["Digest realm=\"r\", nonce=\"n\", qop=\"auth\""];

        string? first = authenticator.CreateAuthorization(request, challenges);
        string?[] later = [.. Enumerable.Range(0, 50).Select(_ => authenticator.CreateAuthorization(request, challenges))];

        Assert.IsNotNull(first);
        Assert.IsTrue(later.All(value => value == first));
    }

    [TestMethod]
    public async Task DigestCreateAuthorization_CalledConcurrently_GivesTheSameAnswerEveryTime()
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "cnonce");
        HttpAuthRequest request = Request(new NetworkCredential("u", "p"), HttpAuthSchemes.Digest);
        string[] challenges = ["Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=SHA-256"];
        string? expected = authenticator.CreateAuthorization(request, challenges);

        string?[] values = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => authenticator.CreateAuthorization(request, challenges))));

        Assert.IsTrue(values.All(value => value == expected));
    }

    [TestMethod]
    [DataRow("", DisplayName = "An empty sent header")]
    [DataRow("Basic dTpw", DisplayName = "A Basic answer")]
    [DataRow("Digest username=\"u\", nc=zzzzzzzz, cnonce=\"c\", qop=auth", DisplayName = "A non-hex nonce count")]
    [DataRow("Digest username=\"u\", nc=, cnonce=\"c\", qop=auth", DisplayName = "An empty nonce count")]
    [DataRow("Digest username=\"u, nc=00000001", DisplayName = "An unterminated quote")]
    public void DigestRepeatAuthorization_MalformedSentHeader_DoesNotThrow(string sent)
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "cnonce");

        string value = authenticator.RepeatAuthorization(Request(new NetworkCredential("u", "p"), HttpAuthSchemes.Digest), sent);

        Assert.IsNotNull(value);
    }

    [TestMethod]
    [DataRow("us:er", "p", "dXM6ZXI6cA==", DisplayName = "A colon in the user name is sent as is")]
    [DataRow("u", "", "dTo=", DisplayName = "An empty password")]
    [DataRow("", "", "Og==", DisplayName = "An empty user and password")]
    [DataRow("u", "a:b:c", "dTphOmI6Yw==", DisplayName = "Colons in the password")]
    public void BasicCreateAuthorization_BoundaryCredentials_EncodesUserColonPassword(string user, string password, string expected)
    {
        BasicAndBearerAuthenticator authenticator = new(Encoding.UTF8);

        string? value = authenticator.CreateAuthorization(Request(new NetworkCredential(user, password), HttpAuthSchemes.Basic), []);

        Assert.AreEqual("Basic " + expected, value);
    }

    [TestMethod]
    [DataRow("Basic", DisplayName = "The scheme alone")]
    [DataRow("Basic realm=\"unterminated", DisplayName = "An unterminated realm")]
    [DataRow("basic realm=x, BASIC, Basic realm=y", DisplayName = "Several Basic challenges in mixed case")]
    public void BasicCreateAuthorization_MalformedBasicChallenge_StillAnswers(string challenge)
    {
        BasicAndBearerAuthenticator authenticator = new(Encoding.UTF8);

        string? value = authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), HttpAuthSchemes.Basic), [challenge]);

        Assert.AreEqual("Basic dTpw", value);
    }

    [TestMethod]
    [DataRow("", DisplayName = "An empty challenge")]
    [DataRow(",,,", DisplayName = "Only commas")]
    [DataRow("Basicx realm=r", DisplayName = "A scheme name with Basic as a prefix")]
    public void BasicCreateAuthorization_NoBasicChallenge_Declines(string challenge)
    {
        BasicAndBearerAuthenticator authenticator = new(Encoding.UTF8);

        string? value = authenticator.CreateAuthorization(Request(new NetworkCredential("u", "p"), HttpAuthSchemes.Basic), [challenge]);

        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow("", DisplayName = "Empty text")]
    [DataRow("\n\n\n", DisplayName = "Only newlines")]
    [DataRow("machine", DisplayName = "A machine keyword with no host")]
    [DataRow("machine 127.0.0.1 login", DisplayName = "A login keyword with no value")]
    [DataRow("machine 127.0.0.1 login \"unterminated", DisplayName = "An unterminated quoted login")]
    [DataRow("machine 127.0.0.1 login a password \"p\\", DisplayName = "A quoted password ending in a backslash")]
    [DataRow("\u0000\u0001\u0002machine\u0000127.0.0.1", DisplayName = "Control characters")]
    [DataRow("macdef", DisplayName = "A macdef at end of file")]
    [DataRow("\"\"\"\"\"\"", DisplayName = "Only quotes")]
    public void NetrcFind_MalformedText_ReturnsAnOutcomeWithoutThrowing(string text)
    {
        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        Assert.IsTrue(Enum.IsDefined(result.Outcome));
    }

    [TestMethod]
    public void NetrcFind_EntryAfterTenThousandOtherMachines_IsFound()
    {
        StringBuilder text = new();
        for (int i = 0; i < 10000; i++)
        {
            text.Append("machine host").Append(i).Append(" login l password p\n");
        }

        text.Append("machine 127.0.0.1 login found password fp\n");

        NetrcLookupResult result = NetrcFile.Find(text.ToString(), Host, userName: null);

        Assert.AreEqual("found", result.Login);
        Assert.AreEqual("fp", result.Password);
    }

    [TestMethod]
    [DataRow(4096, DisplayName = "A 4 KiB password")]
    [DataRow(65536, DisplayName = "A 64 KiB password")]
    public void NetrcFind_LongQuotedPassword_ReturnsAnOutcomeWithoutThrowing(int length)
    {
        string text = "machine 127.0.0.1 login a password \"" + new string('p', length) + "\"\n";

        NetrcLookupResult result = NetrcFile.Find(text, Host, userName: null);

        Assert.IsTrue(Enum.IsDefined(result.Outcome));
    }

    [TestMethod]
    [DataRow("", DisplayName = "An empty host name")]
    [DataRow("MACHINE", DisplayName = "A keyword as host name")]
    [DataRow("127.0.0.1.", DisplayName = "A trailing dot")]
    public void NetrcFind_BoundaryHostName_DoesNotThrow(string hostName)
    {
        NetrcLookupResult result = NetrcFile.Find("machine 127.0.0.1 login a password p\n", hostName, userName: null);

        Assert.IsTrue(Enum.IsDefined(result.Outcome));
    }

    [TestMethod]
    [DataRow(new byte[0], DisplayName = "No bytes")]
    [DataRow(new byte[] { 0x00 }, DisplayName = "A zero byte")]
    [DataRow(new byte[] { 0xA1 }, DisplayName = "A NegTokenResp tag with no length")]
    [DataRow(new byte[] { 0xA1, 0x05, 0x30, 0x03 }, DisplayName = "A length longer than the bytes")]
    [DataRow(new byte[] { 0xA1, 0x84, 0xFF, 0xFF, 0xFF, 0xFF }, DisplayName = "A four-gigabyte length")]
    [DataRow(new byte[] { 0xA1, 0x80, 0x00, 0x00 }, DisplayName = "An indefinite length")]
    [DataRow(new byte[] { 0xA1, 0x02, 0x30, 0x00 }, DisplayName = "An empty NegTokenResp sequence")]
    [DataRow(new byte[] { 0xA1, 0x07, 0x30, 0x05, 0xA0, 0x03, 0x0A, 0x01, 0x09 }, DisplayName = "An out-of-range negState")]
    [DataRow(new byte[] { 0xA1, 0x05, 0x30, 0x03, 0xA0, 0x01, 0x0A }, DisplayName = "A truncated negState")]
    [DataRow(new byte[] { 0x60, 0x02, 0x06, 0x00 }, DisplayName = "An initial context token")]
    [DataRow(new byte[] { 0xA0, 0x02, 0x30, 0x00 }, DisplayName = "A NegTokenInit")]
    [DataRow(new byte[] { 0xA1, 0x02, 0x30, 0x00, 0x00 }, DisplayName = "Trailing bytes")]
    public void SpnegoDecode_MalformedToken_ThrowsOnlySpnegoTokenException(byte[] token)
    {
        try
        {
            _ = SpnegoNegotiationResponse.Decode(token);
        }
        catch (SpnegoTokenException)
        {
            // The documented refusal.
        }
    }

    [TestMethod]
    public void SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException()
    {
        byte[] whole = [0xA1, 0x0B, 0x30, 0x09, 0xA0, 0x03, 0x0A, 0x01, 0x01, 0xA2, 0x02, 0x04, 0x00];

        for (int length = 0; length < whole.Length; length++)
        {
            ReadOnlyMemory<byte> truncated = whole.AsMemory(0, length);

            Assert.ThrowsExactly<SpnegoTokenException>(() => SpnegoNegotiationResponse.Decode(truncated), $"A {length}-byte truncation decoded without throwing.");
        }
    }

    private static string? DigestAnswer(string user, string password, string challenge)
    {
        DigestAuthenticator authenticator = new(Encoding.UTF8, () => "cnonce");
        return authenticator.CreateAuthorization(Request(new NetworkCredential(user, password), HttpAuthSchemes.Digest), [challenge]);
    }

    private static HttpAuthRequest Request(NetworkCredential credential, HttpAuthSchemes allowed) =>
        new("GET", CurlUrl.Parse("http://127.0.0.1/"), "/", credential, null, allowed, IsProxy: false);
}
