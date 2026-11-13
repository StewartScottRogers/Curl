using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Ntlm;
using Curl.Original.Protocol.Ldap.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins the sealed session after the explicit-credential (<c>-u</c>) NTLM bind of <c>--ntlm</c>
/// and <c>--negotiate</c> (BL-5142, ADR-1266): the negotiate message offers SIGN and SEAL, and
/// every message after the bind is a 4-byte length and a sealed buffer.
/// </summary>
[TestClass]
public sealed class LdapAuthenticatedNtlmSealTests
{
    private const string Password = "secret";

    private const string UserName = "cn=u,dc=x";

    /// <summary>The repro's challenge flags: SIGN, SEAL, extended session security, target info, 128-bit and key exchange.</summary>
    private const uint ChallengeFlags = 0xe2898235;

    private static readonly byte[] BindSuccess2 = Hex.Bytes("30 0c 02 01 02 61 07 0a 01 00 04 00 04 00");

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Negotiate)]
    public async Task ExecuteAsync_ExplicitCredentials_OffersSignAndSealInTheNegotiateMessage(HttpAuthSchemes schemes)
    {
        var standIn = new UnsealingStandIn();

        await RunAsync(schemes, standIn);

        byte[] negotiate = [.. "NTLMSSP\0"u8, 0x01, 0x00, 0x00, 0x00, 0xb7, 0x82, 0x08, 0xe2];
        Assert.IsTrue(standIn.Sent.AsSpan().IndexOf(negotiate) > 0);
        Assert.AreEqual(0x30u, BinaryPrimitives.ReadUInt32LittleEndian(negotiate.AsSpan(12)) & 0x30u);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Negotiate)]
    public async Task ExecuteAsync_BoundWithSealing_SealsTheSearchAndTheUnbindForAServerThatUnseals(HttpAuthSchemes schemes)
    {
        var standIn = new UnsealingStandIn();

        TransferResult result = await RunAsync(schemes, standIn);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(new byte[] { 0x63, 0x42 }, standIn.UnsealedOperations);
        byte[] afterBind = standIn.SentAfterBind;
        Assert.AreEqual((uint)(afterBind.Length - 4) - standIn.UnbindFrameLength, BinaryPrimitives.ReadUInt32BigEndian(afterBind));
        Assert.IsFalse(afterBind[4] == 0x30 && afterBind[5] == 0x84);
    }

    [TestMethod]
    [DataRow("30 0c 02 01 00 78 07 0a 01 34 04 00 04 00", DisplayName = "Unsealed notice of disconnection")]
    [DataRow("", DisplayName = "Closed")]
    [DataRow("00 00 00 04 01 02 03 04", DisplayName = "Buffer shorter than a signature")]
    [DataRow("00 00 00 14 01 00 00 00 5e 5e 5e 5e 5e 5e 5e 5e 00 00 00 00 30 0c 02 01", DisplayName = "Signature that does not check")]
    public async Task ExecuteAsync_ServerCannotUnseal_FailsWith39Unavailable(string reply)
    {
        var connection = new ScriptedConnection(ChallengeReply(), BindSuccess2, Hex.Bytes(reply));

        TransferResult result = await RunAsync(HttpAuthSchemes.Ntlm, connection);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LdapSearchFailed, "LDAP remote: Unavailable"), result);
        Assert.IsFalse(connection.Sent.AsSpan().IndexOf("\x30\x84\x00\x00\x00\x05\x02\x01\x03\x42"u8) >= 0);
    }

    [TestMethod]
    public void NtlmLdapLogonAuthentication_AfterTheBind_IsAuthenticatedAndHasNoTokenToSend()
    {
        using var authentication = new NtlmLdapLogonAuthentication(NtlmSessionSecurity.CreateClient((NtlmNegotiateFlags)ChallengeFlags, new byte[16]));

        Assert.IsTrue(authentication.IsAuthenticated);
        Assert.IsNull(authentication.NextToken([1, 2, 3]));
    }

    /// <summary>A sicilyNegotiate BindResponse (messageID 1) carrying the repro's challenge, with no target info, in matchedDN.</summary>
    private static byte[] ChallengeReply()
    {
        byte[] challenge = new byte[48];
        "NTLMSSP\0"u8.CopyTo(challenge);
        challenge[8] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(challenge.AsSpan(16), 48);
        BinaryPrimitives.WriteUInt32LittleEndian(challenge.AsSpan(20), ChallengeFlags);
        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }.CopyTo(challenge, 24);
        BinaryPrimitives.WriteUInt32LittleEndian(challenge.AsSpan(44), 48);
        return Hex.Bytes($"30 {challenge.Length + 12:x2} 02 01 01 61 {challenge.Length + 7:x2} 0a 01 00 04 {challenge.Length:x2} {Convert.ToHexString(challenge)} 04 00");
    }

    private static async Task<TransferResult> RunAsync(HttpAuthSchemes schemes, IConnection connection)
    {
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), LdapDialect.WinLdap, new FakeLogonTokenSource(2));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:24900/dc=x"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(UserName, Password),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AuthSchemes = schemes },
        };

        return await handler.ExecuteAsync(context);
    }

    /// <summary>
    /// An LDAP server that knows the password: answers the Sicily binds, recovers the exported
    /// session key from the authenticate message, then unseals each later buffer and answers a
    /// SearchRequest with a sealed SearchResultDone.
    /// </summary>
    private sealed class UnsealingStandIn : IConnection
    {
        private readonly List<byte> sent = [];

        private readonly List<byte> pending = [];

        private readonly List<byte> replies = [];

        private readonly List<byte> operations = [];

        private NtlmSessionSecurity? security;

        private int binds;

        private int bindEnd;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public byte[] Sent => [.. sent];

        public byte[] SentAfterBind => [.. sent.Skip(bindEnd)];

        public byte[] UnsealedOperations => [.. operations];

        public uint UnbindFrameLength { get; private set; }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int length = Math.Min(buffer.Length, replies.Count);
            for (int i = 0; i < length; i++)
            {
                buffer.Span[i] = replies[i];
            }

            replies.RemoveRange(0, length);
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            sent.AddRange(buffer.Span);
            pending.AddRange(buffer.Span);
            while (security is null ? TakePlain() : TakeSealed())
            {
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            security?.Dispose();
            return ValueTask.CompletedTask;
        }

        private static int MessageLength(byte[] message) => 6 + (int)BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(2));

        private bool TakePlain()
        {
            if (pending.Count < 6 || pending.Count < MessageLength([.. pending]))
            {
                return false;
            }

            byte[] message = [.. pending.Take(MessageLength([.. pending]))];
            pending.RemoveRange(0, message.Length);
            binds++;
            if (binds == 1)
            {
                replies.AddRange(ChallengeReply());
                return true;
            }

            security = NtlmSessionSecurity.CreateServer((NtlmNegotiateFlags)ChallengeFlags, ExportedSessionKey(message.AsSpan(message.AsSpan().IndexOf("NTLMSSP\0"u8))));
            bindEnd = sent.Count;
            replies.AddRange(BindSuccess2);
            return true;
        }

        private bool TakeSealed()
        {
            if (pending.Count < 4 || pending.Count < 4 + (int)BinaryPrimitives.ReadUInt32BigEndian([.. pending.Take(4)]))
            {
                return false;
            }

            int length = (int)BinaryPrimitives.ReadUInt32BigEndian([.. pending.Take(4)]);
            byte[] wrapped = [.. pending.Skip(4).Take(length)];
            pending.RemoveRange(0, 4 + length);
            Assert.IsTrue(security!.TryUnseal(wrapped.AsSpan(16), wrapped.AsSpan(0, 16), out byte[]? message));
            operations.Add(message![9]);
            if (message[9] == 0x42)
            {
                UnbindFrameLength = (uint)(4 + length);
                return true;
            }

            byte[] done = Hex.Bytes($"30 0c 02 01 {message[8]:x2} 65 07 0a 01 00 04 00 04 00");
            byte[] sealedDone = security.Seal(done, out byte[] signature);
            byte[] frame = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)(signature.Length + sealedDone.Length));
            replies.AddRange([.. frame, .. signature, .. sealedDone]);
            return true;
        }

        /// <summary>Recovers the exported session key from <paramref name="authenticate" /> with NTLMv2 and the password.</summary>
        private static byte[] ExportedSessionKey(ReadOnlySpan<byte> authenticate)
        {
            ReadOnlySpan<byte> ntResponse = Field(authenticate, 20);
            string user = Encoding.Unicode.GetString(Field(authenticate, 36));
            string domain = Encoding.Unicode.GetString(Field(authenticate, 28));
            byte[] ntOwfV2 = NtlmOneWayFunctions.ComputeNtOwfV2(user, domain, NtlmOneWayFunctions.ComputeNtOwfV1(Password));
            byte[] keyExchangeKey = HMACMD5.HashData(ntOwfV2, ntResponse[..16]);
            return NtlmResponseComputation.EncryptSessionKey(keyExchangeKey, Field(authenticate, 52));
        }

        private static ReadOnlySpan<byte> Field(ReadOnlySpan<byte> message, int at) =>
            message.Slice((int)BinaryPrimitives.ReadUInt32LittleEndian(message[(at + 4)..]), BinaryPrimitives.ReadUInt16LittleEndian(message[at..]));
    }
}
