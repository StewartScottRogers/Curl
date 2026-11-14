using System.Text;

namespace Curl.Ntlm;

/// <summary>Checks <see cref="NtlmSessionSecurity" /> against MS-NLMP section 4.2.4 and for tamper detection.</summary>
[TestClass]
public sealed class NtlmSessionSecurityTests
{
    private const NtlmNegotiateFlags Flags = NtlmNegotiateFlags.NegotiateExtendedSessionSecurity | NtlmNegotiateFlags.Negotiate128 | NtlmNegotiateFlags.NegotiateKeyExchange;

    private static readonly byte[] SessionKey = Enumerable.Repeat((byte)0x55, 16).ToArray();

    private static readonly byte[] Plaintext = Encoding.Unicode.GetBytes("Plaintext");

    [TestMethod]
    public void CreateClient_MsNlmp424Vector_DerivesTheSealKeyAndSealsPlaintext()
    {
        using NtlmSessionSecurity client = NtlmSessionSecurity.CreateClient(Flags, SessionKey);

        byte[] sealedMessage = client.Seal(Plaintext, out byte[] signature);

        Assert.AreEqual("59f600973cc4960a25480a7c196e4c58", Convert.ToHexString(client.SendSealKey).ToLowerInvariant());
        Assert.AreEqual("54e50165bf1936dc996020c1811b0f06fb5f", Convert.ToHexString(sealedMessage).ToLowerInvariant());
        Assert.AreEqual("010000007fb38ec5c55d497600000000", Convert.ToHexString(signature).ToLowerInvariant());
    }

    [TestMethod]
    public void ClientAndServer_SealAndUnsealBothWays_WithASequenceNumberPerDirection()
    {
        using NtlmSessionSecurity client = NtlmSessionSecurity.CreateClient(Flags, SessionKey);
        using NtlmSessionSecurity server = NtlmSessionSecurity.CreateServer(Flags, SessionKey);

        CollectionAssert.AreEqual(client.SendSignKey, server.ReceiveSignKey);
        CollectionAssert.AreEqual(server.SendSealKey, client.ReceiveSealKey);
        for (int round = 0; round < 3; round++)
        {
            byte[] toServer = client.Seal(Plaintext, out byte[] clientSignature);
            Assert.IsTrue(server.TryUnseal(toServer, clientSignature, out byte[]? atServer));
            CollectionAssert.AreEqual(Plaintext, atServer);
            Assert.AreEqual(round, clientSignature[12]);

            byte[] toClient = server.Seal(Plaintext, out byte[] serverSignature);
            Assert.IsTrue(client.TryUnseal(toClient, serverSignature, out byte[]? atClient));
            CollectionAssert.AreEqual(Plaintext, atClient);
        }
    }

    [TestMethod]
    public void TryUnseal_TamperedMessage_Fails()
    {
        using NtlmSessionSecurity client = NtlmSessionSecurity.CreateClient(Flags, SessionKey);
        using NtlmSessionSecurity server = NtlmSessionSecurity.CreateServer(Flags, SessionKey);
        byte[] sealedMessage = client.Seal(Plaintext, out byte[] signature);
        sealedMessage[0] ^= 1;

        Assert.IsFalse(server.TryUnseal(sealedMessage, signature, out byte[]? message));
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryUnseal_TamperedSignature_Fails()
    {
        using NtlmSessionSecurity client = NtlmSessionSecurity.CreateClient(Flags, SessionKey);
        using NtlmSessionSecurity server = NtlmSessionSecurity.CreateServer(Flags, SessionKey);
        byte[] sealedMessage = client.Seal(Plaintext, out byte[] signature);
        signature[5] ^= 1;

        Assert.IsFalse(server.TryUnseal(sealedMessage, signature, out _));
    }

    [TestMethod]
    public void SealAndUnseal_WithoutKeyExchange_LeavesTheChecksumUnencrypted()
    {
        const NtlmNegotiateFlags noExchange = NtlmNegotiateFlags.NegotiateExtendedSessionSecurity | NtlmNegotiateFlags.Negotiate128;
        using NtlmSessionSecurity client = NtlmSessionSecurity.CreateClient(noExchange, SessionKey);
        using NtlmSessionSecurity server = NtlmSessionSecurity.CreateServer(noExchange, SessionKey);

        byte[] sealedMessage = client.Seal(Plaintext, out byte[] signature);

        Assert.IsTrue(server.TryUnseal(sealedMessage, signature, out _));
        Assert.AreNotEqual("7fb38ec5c55d4976", Convert.ToHexString(signature.AsSpan(4, 8)).ToLowerInvariant());
    }

    [TestMethod]
    public void Create_ShortKey_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NtlmSessionSecurity.CreateClient(Flags, new byte[8]));
    }

    [TestMethod]
    public void Create_WithoutExtendedSessionSecurityOr128_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NtlmSessionSecurity.CreateServer(NtlmNegotiateFlags.Negotiate128, SessionKey));
        Assert.ThrowsExactly<ArgumentException>(() => NtlmSessionSecurity.CreateServer(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity, SessionKey));
    }
}
