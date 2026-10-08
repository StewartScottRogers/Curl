using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The key schedule and transcript hash beyond the RFC 8448 traces: SHA-384, the key
/// update, the external binder key, the HelloRetryRequest <c>message_hash</c>, and the
/// hashes TLS 1.3 does not use.
/// </summary>
[TestClass]
public sealed class Tls13KeyScheduleTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Sha384ScheduleDerivesFortyEightByteSecrets()
    {
        Tls13KeySchedule schedule = Tls13KeySchedule.Sha384;
        byte[] zeros = new byte[48];
        Diagnostics.Arrange("schedule", "SHA-384, no pre-shared key, 32-byte zero shared secret");

        byte[] earlySecret = schedule.ComputeEarlySecret(null);
        byte[] masterSecret = schedule.ComputeMasterSecret(schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), new byte[32]));
        Diagnostics.Bytes("early secret", earlySecret);
        Diagnostics.Act("master secret length", masterSecret.Length);

        Diagnostics.Assert("hash length", 48, schedule.HashLength);
        Diagnostics.Diff("early secret", HMACSHA384.HashData(zeros, zeros), earlySecret);
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
        Diagnostics.Arrange("secret", "48 zero bytes");
        Diagnostics.Bytes("transcript hash", transcriptHash);

        byte[] actual = Tls13KeySchedule.Sha384.DeriveClientHandshakeTrafficSecret(secret, transcriptHash);
        Diagnostics.Act("client handshake traffic secret length", actual.Length);

        Diagnostics.Diff("client handshake traffic secret", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Sha384TrafficKeysUseTheRequestedKeyLengthAndATwelveByteIv()
    {
        Diagnostics.Arrange("inputs", "SHA-384, 48 zero-byte secret, key length 32");

        Tls13TrafficKeys keys = Tls13KeySchedule.Sha384.DeriveTrafficKeys(new byte[48], 32);
        Diagnostics.Act("key and iv lengths", $"{keys.Key.Length}, {keys.Iv.Length}");

        Diagnostics.Assert("key length", 32, keys.Key.Length);
        Diagnostics.Assert("iv length", 12, keys.Iv.Length);
        Assert.HasCount(32, keys.Key);
        Assert.HasCount(12, keys.Iv);
    }

    [TestMethod]
    public void DeriveNextApplicationTrafficSecretExpandsTrafficUpd()
    {
        byte[] secret = Convert.FromHexString("9e40646ce79a7f9dc05af8889bce6552875afa0b06df0087f792ebb7c17504a5");
        byte[] expected = HKDF.Expand(HashAlgorithmName.SHA256, secret, 32, HkdfLabel.Encode("traffic upd", [], 32));
        Diagnostics.Bytes("application traffic secret", secret);
        Diagnostics.Arrange("label", "traffic upd");

        byte[] actual = Tls13KeySchedule.Sha256.DeriveNextApplicationTrafficSecret(secret);
        Diagnostics.Act("next application traffic secret length", actual.Length);

        Diagnostics.Diff("next application traffic secret", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeriveExternalBinderKeyUsesExtBinderOverTheEmptyHash()
    {
        byte[] earlySecret = Tls13KeySchedule.Sha256.ComputeEarlySecret(new byte[32]);
        byte[] expected = HKDF.Expand(HashAlgorithmName.SHA256, earlySecret, 32, HkdfLabel.Encode("ext binder", SHA256.HashData(Array.Empty<byte>()), 32));
        Diagnostics.Arrange("pre-shared key", "32 zero bytes");
        Diagnostics.Bytes("early secret", earlySecret);

        byte[] actual = Tls13KeySchedule.Sha256.DeriveExternalBinderKey(earlySecret);
        Diagnostics.Act("external binder key length", actual.Length);

        Diagnostics.Diff("external binder key", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReplaceWithMessageHashLeavesTheSyntheticMessageHashMessage()
    {
        byte[] clientHello = Convert.FromHexString(Rfc8448Messages.SimpleClientHello);
        byte[] messageHash = [0xfe, 0x00, 0x00, 0x20, .. SHA256.HashData(clientHello)];
        using TranscriptHash transcript = Tls13KeySchedule.Sha256.CreateTranscriptHash();
        Diagnostics.Bytes("client hello", clientHello);
        Diagnostics.Bytes("message_hash message", messageHash);
        Diagnostics.Arrange("hash", "SHA-256, ClientHello then ReplaceWithMessageHash");

        transcript.Append(clientHello);
        transcript.ReplaceWithMessageHash();
        byte[] currentHash = transcript.GetCurrentHash();
        Diagnostics.Act("transcript hash length", transcript.HashLength);

        Diagnostics.Diff("transcript hash", SHA256.HashData(messageHash), currentHash);
        Assert.AreEqual(32, transcript.HashLength);
        CollectionAssert.AreEqual(SHA256.HashData(messageHash), transcript.GetCurrentHash());
    }

    [TestMethod]
    public void KeyScheduleRejectsAHashNoTls13SuiteUses()
    {
        Diagnostics.Arrange("hash", HashAlgorithmName.SHA512);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Tls13KeySchedule(HashAlgorithmName.SHA512));
        Diagnostics.Act("exception", exception.Message);

        Diagnostics.Assert("exception type", typeof(ArgumentException), exception.GetType());
    }

    [TestMethod]
    public void TranscriptHashRejectsAHashNoTls13SuiteUses()
    {
        Diagnostics.Arrange("hash", HashAlgorithmName.SHA1);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new TranscriptHash(HashAlgorithmName.SHA1));
        Diagnostics.Act("exception", exception.Message);

        Diagnostics.Assert("exception type", typeof(ArgumentException), exception.GetType());
    }
}
