using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The key schedule and transcript hash beyond the RFC 8448 traces: SHA-384, the key
/// update, the external binder key, the HelloRetryRequest <c>message_hash</c>, and the
/// hashes TLS 1.3 does not use.
/// </summary>
[TestClass]
public sealed class Tls13KeyScheduleTests
{
    [TestMethod]
    public void Sha384ScheduleDerivesFortyEightByteSecrets()
    {
        Tls13KeySchedule schedule = Tls13KeySchedule.Sha384;
        byte[] zeros = new byte[48];

        Assert.AreEqual(HashAlgorithmName.SHA384, schedule.HashAlgorithm);
        Assert.AreEqual(48, schedule.HashLength);
        CollectionAssert.AreEqual(HMACSHA384.HashData(zeros, zeros), schedule.ComputeEarlySecret(null));
        Assert.HasCount(48, schedule.ComputeMasterSecret(schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), new byte[32])));
    }

    [TestMethod]
    public void Sha384DeriveSecretExpandsWithTheSha384Hash()
    {
        byte[] secret = new byte[48];
        byte[] transcriptHash = SHA384.HashData(Array.Empty<byte>());
        byte[] expected = HKDF.Expand(HashAlgorithmName.SHA384, secret, 48, HkdfLabel.Encode("c hs traffic", transcriptHash, 48));

        CollectionAssert.AreEqual(expected, Tls13KeySchedule.Sha384.DeriveClientHandshakeTrafficSecret(secret, transcriptHash));
    }

    [TestMethod]
    public void Sha384TrafficKeysUseTheRequestedKeyLengthAndATwelveByteIv()
    {
        Tls13TrafficKeys keys = Tls13KeySchedule.Sha384.DeriveTrafficKeys(new byte[48], 32);

        Assert.HasCount(32, keys.Key);
        Assert.HasCount(12, keys.Iv);
    }

    [TestMethod]
    public void DeriveNextApplicationTrafficSecretExpandsTrafficUpd()
    {
        byte[] secret = Convert.FromHexString("9e40646ce79a7f9dc05af8889bce6552875afa0b06df0087f792ebb7c17504a5");
        byte[] expected = HKDF.Expand(HashAlgorithmName.SHA256, secret, 32, HkdfLabel.Encode("traffic upd", [], 32));

        CollectionAssert.AreEqual(expected, Tls13KeySchedule.Sha256.DeriveNextApplicationTrafficSecret(secret));
    }

    [TestMethod]
    public void DeriveExternalBinderKeyUsesExtBinderOverTheEmptyHash()
    {
        byte[] earlySecret = Tls13KeySchedule.Sha256.ComputeEarlySecret(new byte[32]);
        byte[] expected = HKDF.Expand(HashAlgorithmName.SHA256, earlySecret, 32, HkdfLabel.Encode("ext binder", SHA256.HashData(Array.Empty<byte>()), 32));

        CollectionAssert.AreEqual(expected, Tls13KeySchedule.Sha256.DeriveExternalBinderKey(earlySecret));
    }

    [TestMethod]
    public void ReplaceWithMessageHashLeavesTheSyntheticMessageHashMessage()
    {
        byte[] clientHello = Convert.FromHexString(Rfc8448Messages.SimpleClientHello);
        byte[] messageHash = [0xfe, 0x00, 0x00, 0x20, .. SHA256.HashData(clientHello)];
        using TranscriptHash transcript = Tls13KeySchedule.Sha256.CreateTranscriptHash();

        transcript.Append(clientHello);
        transcript.ReplaceWithMessageHash();

        Assert.AreEqual(32, transcript.HashLength);
        CollectionAssert.AreEqual(SHA256.HashData(messageHash), transcript.GetCurrentHash());
    }

    [TestMethod]
    public void KeyScheduleRejectsAHashNoTls13SuiteUses() =>
        Assert.ThrowsExactly<ArgumentException>(() => new Tls13KeySchedule(HashAlgorithmName.SHA512));

    [TestMethod]
    public void TranscriptHashRejectsAHashNoTls13SuiteUses() =>
        Assert.ThrowsExactly<ArgumentException>(() => new TranscriptHash(HashAlgorithmName.SHA1));
}
