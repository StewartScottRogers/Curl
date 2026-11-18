using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the <c>qop=auth-conf</c> negotiation of the DIGEST-MD5 bind (BL-5376): the response names
/// <c>cipher=3des</c>, and every later PDU travels RFC 2831 section 2.3 sealed.
/// </summary>
[TestClass]
public sealed class LdapDigestMd5AuthConfTests
{
    private const string AllQops = "realm=\"r\",nonce=\"n1\",qop=\"auth,auth-int,auth-conf\",cipher=\"3des,rc4,des\",algorithm=md5-sess,charset=utf-8";

    private static readonly LdapBerWriter Writer = new(LdapDialect.WinLdap);

    private static readonly NetworkCredential User = new("u", "secret");

    [TestMethod]
    public void AnswerRequest_AllQopsOffered_AnswersAuthConfWith3desAndTheRfc2831Response()
    {
        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 2, Encoding.ASCII.GetBytes(AllQops), "u", "secret", "ldap/h", "cn1");

        Assert.IsNotNull(request);
        byte[] secret = MD5.HashData(Encoding.UTF8.GetBytes("u:r:secret"));
        string a1 = Convert.ToHexStringLower(MD5.HashData([.. secret, .. ":n1:cn1"u8]));
        string a2 = Convert.ToHexStringLower(MD5.HashData("AUTHENTICATE:ldap/h:00000000000000000000000000000000"u8));
        string expected = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes($"{a1}:n1:00000001:cn1:auth-conf:{a2}")));
        string sent = Encoding.ASCII.GetString(request);
        StringAssert.Contains(sent, "nc=00000001,qop=auth-conf,cipher=3des,digest-uri=\"ldap/h\",response=" + expected + ",charset=utf-8");
    }

    [TestMethod]
    [DataRow("realm=\"r\",nonce=\"n1\",qop=\"auth-conf\",cipher=\"3des\"", true, DisplayName = "Only auth-conf")]
    [DataRow("nonce=\"n1\",qop=\"auth,auth-conf\",cipher=\"rc4\"", true, DisplayName = "auth-conf with only rc4")]
    [DataRow("nonce=\"n1\",qop=\"auth,auth-conf\",cipher=\"des\"", false, DisplayName = "auth-conf without a supported cipher falls back to auth")]
    [DataRow("nonce=\"n1\",qop=\"auth\",cipher=\"3des\"", false, DisplayName = "Only auth")]
    [DataRow("nonce=\"n1\",qop=\"auth-conf\",cipher=\"3des\",charset=utf-8", true, DisplayName = "UTF-8 charset")]
    [DataRow("qop=\"auth-conf\",cipher=\"3des\"", false, DisplayName = "No nonce")]
    public void SecurityLayerFor_TracksWhetherTheAnswerWasAuthConf(string challenge, bool conf)
    {
        LdapDigestMd5SecurityLayer? layer = LdapDigestMd5Bind.SecurityLayerFor(Encoding.ASCII.GetBytes(challenge), "u", "p", "c");

        Assert.AreEqual(conf, layer is not null);
    }

    [TestMethod]
    public void AnswerRequest_OnlyAuthOffered_KeepsQopAuthWithNoCipher()
    {
        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 2, "nonce=\"n\",qop=\"auth\",cipher=\"3des\""u8, "u", "p", "ldap/h", "c");

        Assert.IsNotNull(request);
        string sent = Encoding.ASCII.GetString(request);
        StringAssert.Contains(sent, ",qop=auth,digest-uri=");
        Assert.IsFalse(sent.Contains("cipher", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnswerRequest_OnlyAuthConfWithoutCipher_CannotAnswer()
    {
        Assert.IsNull(LdapDigestMd5Bind.AnswerRequest(Writer, 2, "nonce=\"n\",qop=\"auth-conf\""u8, "u", "p", "ldap/h", "c"));
        Assert.IsNull(LdapDigestMd5Bind.SecurityLayerFor("qop=\"auth-conf\",cipher=3des"u8, "u", "p", "c"));
    }

    [TestMethod]
    public void WrapAndUnwrap_ClientToServerAndBack_RoundTripWithSequenceNumbers()
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer client = LdapDigestMd5SecurityLayer.Create(hashA1);
        using LdapDigestMd5SecurityLayer server = LdapDigestMd5SecurityLayer.Create(hashA1, isServer: true);

        foreach (int length in new[] { 0, 1, 5, 6, 7, 8, 9, 100 })
        {
            byte[] message = RandomNumberGenerator.GetBytes(length);
            byte[] wrapped = client.Wrap(message);
            Assert.AreEqual(0, (wrapped.Length - 6) % 8);
            CollectionAssert.AreEqual(message, server.Unwrap(wrapped));
            CollectionAssert.AreEqual(message, client.Unwrap(server.Wrap(message)));
        }

        byte[] next = client.Wrap("x"u8);
        Assert.AreEqual(8u, BinaryPrimitives.ReadUInt32BigEndian(next.AsSpan(next.Length - 4)));
        Assert.AreEqual(1, next[^5]);
        Assert.IsNull(client.NextToken([]));
        Assert.IsTrue(client.IsAuthenticated);
    }

    [TestMethod]
    public void Rc4_KnownVector_MatchesRfc6229StyleTestCase()
    {
        // The widely published RC4 test vector: key "Key", plaintext "Plaintext".
        byte[] encrypted = new LdapRc4("Key"u8).Transform("Plaintext"u8);

        Assert.AreEqual("BBF316E8D940AF0AD3", Convert.ToHexString(encrypted));
    }

    [TestMethod]
    [DataRow("rc4")]
    [DataRow("rc4-56")]
    [DataRow("rc4-40")]
    public void WrapAndUnwrap_StreamCipher_RoundTripWithoutPaddingAndRejectDamage(string cipher)
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer client = LdapDigestMd5SecurityLayer.Create(hashA1, cipher: cipher);
        using LdapDigestMd5SecurityLayer server = LdapDigestMd5SecurityLayer.Create(hashA1, isServer: true, cipher: cipher);

        foreach (int length in new[] { 0, 1, 7, 8, 100 })
        {
            byte[] message = RandomNumberGenerator.GetBytes(length);
            byte[] wrapped = client.Wrap(message);
            Assert.AreEqual(length + 10 + 6, wrapped.Length);
            CollectionAssert.AreEqual(message, server.Unwrap(wrapped));
            CollectionAssert.AreEqual(message, client.Unwrap(server.Wrap(message)));
        }

        byte[] flipped = client.Wrap("hello"u8);
        flipped[1] ^= 0x01;
        Assert.IsNull(server.Unwrap(flipped));
        Assert.IsNull(server.Unwrap(new byte[9]));
        Assert.IsNull(server.Unwrap(client.Wrap("x"u8)[..^1]));
    }

    [TestMethod]
    public void Create_Rc4VersusRc456_SealDifferently()
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer full = LdapDigestMd5SecurityLayer.Create(hashA1, cipher: "rc4");
        using LdapDigestMd5SecurityLayer weak = LdapDigestMd5SecurityLayer.Create(hashA1, cipher: "rc4-56");

        CollectionAssert.AreNotEqual(full.Wrap("same"u8), weak.Wrap("same"u8));
    }

    [TestMethod]
    [DataRow("realm=\"r\",nonce=\"n\",qop=\"auth-conf\",cipher=\"rc4-40,rc4\"", "rc4")]
    [DataRow("nonce=\"n\",qop=\"auth-conf\",cipher=\"rc4-40,rc4-56,des\"", "rc4-56")]
    [DataRow("nonce=\"n\",qop=\"auth-conf\",cipher=\"rc4,3des\"", "3des")]
    public void AnswerRequest_PrefersTheStrongestSupportedCipher(string challenge, string expected)
    {
        byte[]? request = LdapDigestMd5Bind.AnswerRequest(Writer, 2, Encoding.ASCII.GetBytes(challenge), "u", "p", "ldap/h", "c");

        Assert.IsNotNull(request);
        StringAssert.Contains(Encoding.ASCII.GetString(request), $"qop=auth-conf,cipher={expected},digest-uri=");
    }

    [TestMethod]
    public void Unwrap_DamagedMessage_ReturnsNull()
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer client = LdapDigestMd5SecurityLayer.Create(hashA1);
        byte[] wrapped = client.Wrap("hello"u8);
        byte[] again = client.Wrap("hello"u8);
        byte[] flipped = [.. wrapped];
        flipped[2] ^= 0x40;
        byte[] badVersion = [.. wrapped];
        badVersion[^5] = 2;
        byte[] badLength = [.. wrapped, 0];

        foreach (byte[] damaged in new[] { flipped, badVersion, badLength, wrapped[..8], again, [] })
        {
            using LdapDigestMd5SecurityLayer server = LdapDigestMd5SecurityLayer.Create(hashA1, isServer: true);
            Assert.IsNull(server.Unwrap(damaged));
        }

        using LdapDigestMd5SecurityLayer other = LdapDigestMd5SecurityLayer.Create(MD5.HashData("other"u8), isServer: true);
        Assert.IsNull(other.Unwrap(wrapped));
    }

    [TestMethod]
    public void Unwrap_ForgedTrailerOrPaddingOrMessage_ReturnsNull()
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer client = LdapDigestMd5SecurityLayer.Create(hashA1);
        byte[] wrapped = client.Wrap("0123456789abc"u8);
        byte[] badVersion = [.. wrapped];
        badVersion[^6] = 1;
        byte[] badPadding = [.. wrapped];
        badPadding[5] ^= 0x01;
        byte[] badMessage = [.. wrapped];
        badMessage[0] ^= 0x01;

        Assert.AreEqual(30, wrapped.Length);
        foreach (byte[] forged in new[] { badVersion, badPadding, badMessage })
        {
            using LdapDigestMd5SecurityLayer server = LdapDigestMd5SecurityLayer.Create(hashA1, isServer: true);
            Assert.IsNull(server.Unwrap(forged));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthConfBindRefused_FailsWith38()
    {
        byte[] body = "nonce=\"n1\",qop=\"auth-conf\",cipher=\"3des\""u8.ToArray();
        var connection = new ScriptedConnection(
            Hex.Bytes($"30 {5 + 9 + body.Length:x2} 02 01 01 61 {9 + body.Length:x2} 0a 01 0e 04 00 04 00 87 {body.Length:x2} {Convert.ToHexString(body)}"),
            Hex.Bytes("30 0c 02 01 02 61 07 0a 01 31 04 00 04 00"));
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = User,
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapCannotBind, "LDAP local: bind via ldap_win_bind Invalid Credentials"), result);
    }

    [TestMethod]
    public void Unwrap_MessageSealedForTheOtherDirection_ReturnsNull()
    {
        byte[] hashA1 = MD5.HashData("a1"u8);
        using LdapDigestMd5SecurityLayer client = LdapDigestMd5SecurityLayer.Create(hashA1);

        Assert.IsNull(client.Unwrap(client.Wrap("0123456789"u8)));
    }

    [TestMethod]
    [DataRow("nonce=\"n1\",qop=\"auth,auth-int,auth-conf\",cipher=\"3des\"", DisplayName = "auth, auth-int and auth-conf offered")]
    [DataRow("nonce=\"n1\",qop=\"auth-conf\",cipher=\"3des\"", DisplayName = "Only auth-conf offered")]
    [DataRow("nonce=\"n1\",qop=\"auth-conf\",cipher=\"rc4\"", DisplayName = "Only rc4 offered")]
    [DataRow("nonce=\"n1\",qop=\"auth-conf\",cipher=\"rc4-40\"", DisplayName = "Only rc4-40 offered")]
    public async Task ExecuteAsync_AuthConfBind_SealsEveryLaterPduAndUnsealsEveryReply(string challenge)
    {
        var standIn = new SealingServer(challenge);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(standIn)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:18389/dc=example"),
            Output = new MemoryStream(),
            Credentials = User,
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Success(0), result);
        StringAssert.Contains(standIn.AnswerText, "qop=auth-conf,cipher=" + challenge.Split("cipher=\"")[1].Split('"')[0]);
        Assert.IsTrue(standIn.Unsealed.Any(message => message.Contains((byte)0x63)), "The SearchRequest arrives sealed.");
        Assert.IsTrue(standIn.AllLaterWritesStartWithALength);
    }

    /// <summary>An LDAP server that offers <c>challenge</c>, accepts the answer, then unseals requests and seals its reply.</summary>
    private sealed class SealingServer(string challenge) : IConnection
    {
        private readonly List<byte> replies = [];

        private readonly List<byte[]> unsealed = [];

        private int writes;

        private LdapDigestMd5SecurityLayer? layer;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public string AnswerText { get; private set; } = string.Empty;

        public IReadOnlyList<byte[]> Unsealed => unsealed;

        public bool AllLaterWritesStartWithALength { get; private set; } = true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int length = Math.Min(buffer.Length, replies.Count);
            replies.Take(length).ToArray().CopyTo(buffer.Span);
            replies.RemoveRange(0, length);
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            writes++;
            if (writes == 1)
            {
                replies.AddRange(ChallengeReply(challenge));
            }
            else if (writes == 2)
            {
                AnswerText = Encoding.ASCII.GetString(buffer.Span);
                string cnonce = AnswerText.Split("cnonce=\"")[1].Split('"')[0];
                string nonce = challenge.Split("nonce=\"")[1].Split('"')[0];
                byte[] secret = MD5.HashData(Encoding.Latin1.GetBytes("u::secret"));
                string cipher = AnswerText.Split("cipher=")[1].Split(',')[0];
                layer = LdapDigestMd5SecurityLayer.Create(MD5.HashData([.. secret, .. Encoding.Latin1.GetBytes($":{nonce}:{cnonce}")]), isServer: true, cipher: cipher);
                replies.AddRange(Hex.Bytes("30 0c 02 01 02 61 07 0a 01 00 04 00 04 00"));
            }
            else
            {
                AllLaterWritesStartWithALength &= buffer.Span[0] == 0 && BinaryPrimitives.ReadInt32BigEndian(buffer.Span) == buffer.Length - 4;
                byte[] message = layer!.Unwrap(buffer.Span[4..])!;
                unsealed.Add(message);
                if (message.Contains((byte)0x63))
                {
                    byte[] sealedDone = layer.Wrap(Hex.Bytes("30 0c 02 01 03 65 07 0a 01 00 04 00 04 00"));
                    byte[] framed = new byte[4 + sealedDone.Length];
                    BinaryPrimitives.WriteInt32BigEndian(framed, sealedDone.Length);
                    sealedDone.CopyTo(framed, 4);
                    replies.AddRange(framed);
                }
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static byte[] ChallengeReply(string text)
        {
            byte[] body = Encoding.ASCII.GetBytes(text);
            return Hex.Bytes($"30 {5 + 9 + body.Length:x2} 02 01 01 61 {9 + body.Length:x2} 0a 01 0e 04 00 04 00 87 {body.Length:x2} {Convert.ToHexString(body)}");
        }
    }
}
