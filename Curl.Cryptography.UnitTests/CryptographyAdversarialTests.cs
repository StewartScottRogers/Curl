using System.Numerics;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Attacks <c>Curl.Cryptography.UnitLibrary</c>'s public surface as a black box, by the
/// method in Documentation/Wiki/Adversarial-Testing.md (BL-1498): lengths at and around
/// every block and key boundary, a single flipped bit in every byte of a tag, ciphertext
/// or signature, non-canonical curve encodings, every invalid span length, and calls after
/// <c>Dispose</c>, in pieces and from many threads at once. The oracle is each primitive's
/// specification and its own documented contract.
/// </summary>
[TestClass]
public sealed class CryptographyAdversarialTests
{
    // RFC 8032 section 7.1, TEST 1 secret key.
    private const string Ed25519PrivateKey = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";

    // RFC 7748 section 6.1, Alice's X25519 private key.
    private const string X25519PrivateKey = "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a";

    // RFC 7748 section 6.2, Alice's X448 private key.
    private const string X448PrivateKey =
        "9a8f4925d1519f5775cf46b04b5800d4ee9ee8bae8bc5565d498c28dd9c9baf574a9419744897391006382a6f127ab1d9ac2d8c0a598726b";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // Family 1, boundaries: plaintext lengths 0, 1, block - 1, block and block + 1 of ChaCha20's 64-byte block.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(129)]
    public void AeadChaCha20Poly1305_PlaintextAtBlockBoundary_RoundTripsAndRejectsATamperedTag(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("plaintext length", length);
        byte[] plaintext = Filled(length, 0x5a);
        byte[] nonce = new byte[AeadChaCha20Poly1305.NonceSize];
        byte[] ciphertext = new byte[length];
        byte[] tag = new byte[AeadChaCha20Poly1305.TagSize];
        byte[] decrypted = new byte[length];
        using var aead = new AeadChaCha20Poly1305(Filled(AeadChaCha20Poly1305.KeySize, 0x01));

        aead.Encrypt(nonce, plaintext, ciphertext, tag);
        bool opened = aead.TryDecrypt(nonce, ciphertext, tag, decrypted);
        tag[^1] ^= 0x80;
        bool openedTampered = aead.TryDecrypt(nonce, ciphertext, tag, new byte[length]);
        diagnostics.Act("opened", opened);
        diagnostics.Act("opened tampered", openedTampered);

        Assert.IsTrue(opened);
        CollectionAssert.AreEqual(plaintext, decrypted);
        Assert.IsFalse(openedTampered);
    }

    // Family 1, boundaries: all-zero and all-0xFF keys are legal keys, not special cases.
    [TestMethod]
    [DataRow((byte)0x00)]
    [DataRow((byte)0xff)]
    public void AeadChaCha20Poly1305_AllZeroOrAllOnesKey_RoundTrips(byte keyByte)
    {
        TestDiagnostics.For(TestContext).Arrange("key byte", keyByte);
        byte[] plaintext = Filled(100, 0x33);
        byte[] nonce = Filled(AeadChaCha20Poly1305.NonceSize, keyByte);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AeadChaCha20Poly1305.TagSize];
        byte[] decrypted = new byte[plaintext.Length];
        using var aead = new AeadChaCha20Poly1305(Filled(AeadChaCha20Poly1305.KeySize, keyByte));

        aead.Encrypt(nonce, plaintext, ciphertext, tag, [1, 2, 3]);

        Assert.IsTrue(aead.TryDecrypt(nonce, ciphertext, tag, decrypted, [1, 2, 3]));
        CollectionAssert.AreEqual(plaintext, decrypted);
        CollectionAssert.AreNotEqual(plaintext, ciphertext);
    }

    // Family 2, malformed input: one flipped bit in every byte of the tag, the ciphertext and the associated data.
    [TestMethod]
    public void AeadChaCha20Poly1305_SingleBitFlippedInEveryByte_IsRejectedWithAnAllZeroPlaintext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] plaintext = Filled(70, 0x42);
        byte[] associatedData = Filled(20, 0x17);
        byte[] nonce = Filled(AeadChaCha20Poly1305.NonceSize, 0x07);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[AeadChaCha20Poly1305.TagSize];
        using var aead = new AeadChaCha20Poly1305(Filled(AeadChaCha20Poly1305.KeySize, 0x09));
        aead.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        int accepted = 0;

        foreach (byte[] target in new[] { tag, ciphertext, associatedData })
        {
            for (int index = 0; index < target.Length; index++)
            {
                byte bit = (byte)(1 << (index % 8));
                target[index] ^= bit;
                byte[] decrypted = Filled(plaintext.Length, 0xee);
                if (aead.TryDecrypt(nonce, ciphertext, tag, decrypted, associatedData) || decrypted.Any(value => value != 0))
                {
                    accepted++;
                    diagnostics.Act("accepted or leaked at index", index);
                }

                target[index] ^= bit;
            }
        }

        Assert.AreEqual(0, accepted);
    }

    // Family 3, invalid partitions: every span one byte short and one byte long.
    [TestMethod]
    [DataRow(31, 12, 16, 0)]
    [DataRow(33, 12, 16, 0)]
    [DataRow(32, 11, 16, 0)]
    [DataRow(32, 13, 16, 0)]
    [DataRow(32, 12, 15, 0)]
    [DataRow(32, 12, 17, 0)]
    [DataRow(32, 12, 16, 1)]
    [DataRow(32, 12, 16, -1)]
    public void AeadChaCha20Poly1305_SpanOfTheWrongLength_ThrowsArgumentException(
        int keyLength,
        int nonceLength,
        int tagLength,
        int outputLengthDelta)
    {
        TestDiagnostics.For(TestContext).Arrange("lengths", $"{keyLength}/{nonceLength}/{tagLength}/{outputLengthDelta}");
        byte[] plaintext = new byte[10];

        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            using var aead = new AeadChaCha20Poly1305(new byte[keyLength]);
            aead.Encrypt(new byte[nonceLength], plaintext, new byte[10 + outputLengthDelta], new byte[tagLength]);
        });
        if (keyLength == AeadChaCha20Poly1305.KeySize)
        {
            using var aead = new AeadChaCha20Poly1305(new byte[keyLength]);
            Assert.ThrowsExactly<ArgumentException>(() =>
                aead.TryDecrypt(new byte[nonceLength], plaintext, new byte[tagLength], new byte[10 + outputLengthDelta]));
        }
    }

    // Family 2, malformed input: a flipped bit in every byte of a Poly1305 tag.
    [TestMethod]
    public void Poly1305_Verify_SingleBitFlippedInEveryTagByte_ReturnsFalse()
    {
        byte[] key = Filled(Poly1305.KeySize, 0x85);
        byte[] message = Filled(Poly1305.TagSize + 1, 0x01);
        byte[] tag = new byte[Poly1305.TagSize];
        Poly1305.ComputeTag(key, message, tag);
        TestDiagnostics.For(TestContext).Bytes("tag", tag);

        Assert.IsTrue(Poly1305.Verify(key, message, tag));
        for (int index = 0; index < tag.Length; index++)
        {
            tag[index] ^= 0x01;
            Assert.IsFalse(Poly1305.Verify(key, message, tag), $"Accepted with byte {index} flipped.");
            tag[index] ^= 0x01;
        }
    }

    // Family 3, invalid partitions: a Poly1305 key or tag one byte off.
    [TestMethod]
    [DataRow(31, 16)]
    [DataRow(33, 16)]
    [DataRow(32, 15)]
    [DataRow(32, 17)]
    public void Poly1305_SpanOfTheWrongLength_ThrowsArgumentException(int keyLength, int tagLength)
    {
        TestDiagnostics.For(TestContext).Arrange("lengths", $"{keyLength}/{tagLength}");

        Assert.ThrowsExactly<ArgumentException>(() => Poly1305.ComputeTag(new byte[keyLength], [1], new byte[tagLength]));
        Assert.ThrowsExactly<ArgumentException>(() => Poly1305.Verify(new byte[keyLength], [1], new byte[tagLength]));
    }

    // Family 2, malformed input: RFC 7748 section 5 - the top bit of a u-coordinate is masked.
    [TestMethod]
    public void X25519_PeerKeyWithTheTopBitSet_GivesTheSameSecretAsWithItClear()
    {
        byte[] privateKey = Convert.FromHexString(X25519PrivateKey);
        byte[] basePoint = new byte[X25519.KeySize];
        basePoint[0] = 9;
        byte[] withTopBit = (byte[])basePoint.Clone();
        withTopBit[^1] |= 0x80;
        byte[] expected = new byte[X25519.KeySize];
        byte[] actual = new byte[X25519.KeySize];

        X25519.ComputePublicKey(privateKey, expected);
        bool succeeded = X25519.TryComputeSharedSecret(privateKey, withTopBit, actual);
        TestDiagnostics.For(TestContext).Diff("secret", Convert.ToHexStringLower(expected), Convert.ToHexStringLower(actual));

        Assert.IsTrue(succeeded);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Family 2, malformed input: RFC 7748 section 5 - a u-coordinate of p + 9 is processed as 9.
    [TestMethod]
    public void X25519_NonCanonicalPeerKeyPPlusNine_GivesTheSameSecretAsNine()
    {
        byte[] privateKey = Convert.FromHexString(X25519PrivateKey);
        byte[] nonCanonical = LittleEndian((BigInteger.One << 255) - 19 + 9, X25519.KeySize);
        byte[] expected = new byte[X25519.KeySize];
        byte[] actual = new byte[X25519.KeySize];

        X25519.ComputePublicKey(privateKey, expected);
        bool succeeded = X25519.TryComputeSharedSecret(privateKey, nonCanonical, actual);
        TestDiagnostics.For(TestContext).Diff("secret", Convert.ToHexStringLower(expected), Convert.ToHexStringLower(actual));

        Assert.IsTrue(succeeded);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Family 1, boundaries: big-integer edge values for the peer's u-coordinate - 0 and p, which is 0 again.
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void X25519_PeerKeyZeroOrP_ReturnsFalseWithAnAllZeroSecret(bool encodeP)
    {
        TestDiagnostics.For(TestContext).Arrange("peer u", encodeP ? "p" : "0");
        byte[] peer = encodeP ? LittleEndian((BigInteger.One << 255) - 19, X25519.KeySize) : new byte[X25519.KeySize];
        byte[] secret = Filled(X25519.KeySize, 0xaa);

        bool succeeded = X25519.TryComputeSharedSecret(Convert.FromHexString(X25519PrivateKey), peer, secret);

        Assert.IsFalse(succeeded);
        Assert.IsTrue(secret.All(value => value == 0));
    }

    // Family 3, invalid partitions: X25519 spans one byte off.
    [TestMethod]
    [DataRow(31, 32, 32)]
    [DataRow(33, 32, 32)]
    [DataRow(32, 31, 32)]
    [DataRow(32, 33, 32)]
    [DataRow(32, 32, 31)]
    [DataRow(32, 32, 33)]
    [DataRow(0, 0, 0)]
    public void X25519_SpanOfTheWrongLength_ThrowsArgumentException(int privateLength, int peerLength, int secretLength)
    {
        TestDiagnostics.For(TestContext).Arrange("lengths", $"{privateLength}/{peerLength}/{secretLength}");

        Assert.ThrowsExactly<ArgumentException>(() =>
            X25519.TryComputeSharedSecret(new byte[privateLength], new byte[peerLength], new byte[secretLength]));
    }

    // Family 2, malformed input: RFC 7748 section 5 - an X448 u-coordinate of p + 5 is processed as 5.
    [TestMethod]
    public void X448_NonCanonicalPeerKeyPPlusFive_GivesTheSameSecretAsFive()
    {
        byte[] privateKey = Convert.FromHexString(X448PrivateKey);
        BigInteger p = (BigInteger.One << 448) - (BigInteger.One << 224) - 1;
        byte[] nonCanonical = LittleEndian(p + 5, X448.KeySize);
        byte[] expected = new byte[X448.KeySize];
        byte[] actual = new byte[X448.KeySize];

        X448.ComputePublicKey(privateKey, expected);
        bool succeeded = X448.TryComputeSharedSecret(privateKey, nonCanonical, actual);
        TestDiagnostics.For(TestContext).Diff("secret", Convert.ToHexStringLower(expected), Convert.ToHexStringLower(actual));

        Assert.IsTrue(succeeded);
        CollectionAssert.AreEqual(expected, actual);
    }

    // Family 3, invalid partitions: X448 spans one byte off.
    [TestMethod]
    [DataRow(55, 56, 56)]
    [DataRow(57, 56, 56)]
    [DataRow(56, 55, 56)]
    [DataRow(56, 56, 57)]
    [DataRow(32, 32, 32)]
    public void X448_SpanOfTheWrongLength_ThrowsArgumentException(int privateLength, int peerLength, int secretLength)
    {
        TestDiagnostics.For(TestContext).Arrange("lengths", $"{privateLength}/{peerLength}/{secretLength}");

        Assert.ThrowsExactly<ArgumentException>(() =>
            X448.TryComputeSharedSecret(new byte[privateLength], new byte[peerLength], new byte[secretLength]));
    }

    // Family 2, malformed input: a flipped bit in every byte of an Ed25519 signature.
    [TestMethod]
    public void Ed25519_Verify_SingleBitFlippedInEverySignatureByte_ReturnsFalse()
    {
        byte[] privateKey = Convert.FromHexString(Ed25519PrivateKey);
        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        byte[] message = Filled(3, 0x61);
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.ComputePublicKey(privateKey, publicKey);
        Ed25519.Sign(privateKey, message, signature);
        TestDiagnostics.For(TestContext).Bytes("signature", signature);

        Assert.IsTrue(Ed25519.Verify(publicKey, message, signature));
        for (int index = 0; index < signature.Length; index++)
        {
            byte bit = (byte)(1 << (index % 8));
            signature[index] ^= bit;
            Assert.IsFalse(Ed25519.Verify(publicKey, message, signature), $"Accepted with byte {index} flipped.");
            signature[index] ^= bit;
        }
    }

    // Family 2, malformed input: RFC 8032 section 5.1.7 - S must be below L, so S + L is refused.
    [TestMethod]
    public void Ed25519_Verify_NonCanonicalSPlusGroupOrder_ReturnsFalse()
    {
        byte[] privateKey = Convert.FromHexString(Ed25519PrivateKey);
        byte[] publicKey = new byte[Ed25519.PublicKeySize];
        byte[] message = [0x72];
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.ComputePublicKey(privateKey, publicKey);
        Ed25519.Sign(privateKey, message, signature);
        BigInteger groupOrder = (BigInteger.One << 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        BigInteger s = new(signature.AsSpan(32), isUnsigned: true, isBigEndian: false);
        LittleEndian(s + groupOrder, 32).CopyTo(signature, 32);
        TestDiagnostics.For(TestContext).Bytes("signature", signature);

        Assert.IsFalse(Ed25519.Verify(publicKey, message, signature));
    }

    // Family 2, malformed input: RFC 8032 section 5.1.3 - a public key whose y is p + 1 (the
    // identity's y, non-canonically) or whose x is 0 with the sign bit set fails to decode,
    // so even the identity signature R = identity, S = 0 is refused under it.
    [TestMethod]
    [DataRow("eeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000080")]
    public void Ed25519_Verify_NonCanonicalIdentityPublicKey_ReturnsFalse(string publicKey)
    {
        TestDiagnostics.For(TestContext).Arrange("public key", publicKey);
        byte[] signature = new byte[Ed25519.SignatureSize];
        signature[0] = 1;

        Assert.IsFalse(Ed25519.Verify(Convert.FromHexString(publicKey), [0x61], signature));
    }

    // Family 3, invalid partitions: Ed25519 spans one byte off.
    [TestMethod]
    [DataRow(31, 64)]
    [DataRow(33, 64)]
    [DataRow(32, 63)]
    [DataRow(32, 65)]
    public void Ed25519_SpanOfTheWrongLength_ThrowsArgumentException(int keyLength, int signatureLength)
    {
        TestDiagnostics.For(TestContext).Arrange("lengths", $"{keyLength}/{signatureLength}");

        Assert.ThrowsExactly<ArgumentException>(() => Ed25519.Sign(new byte[keyLength], [1], new byte[signatureLength]));
        Assert.ThrowsExactly<ArgumentException>(() => Ed25519.Verify(new byte[keyLength], [1], new byte[signatureLength]));
    }

    // Family 4, concurrency: the static primitives give the same answers from many threads at once.
    [TestMethod]
    public void Ed25519AndX25519_CalledFromManyThreadsAtOnce_GiveTheSameAnswersAsOneAfterAnother()
    {
        byte[] privateKey = Convert.FromHexString(Ed25519PrivateKey);
        string expectedSignature = Ed25519SignatureOver(privateKey, 0);
        string expectedSecret = X25519SecretWith(0);
        int mismatches = 0;

        Parallel.For(0, 64, iteration =>
        {
            if (Ed25519SignatureOver(privateKey, 0) != expectedSignature || X25519SecretWith(0) != expectedSecret)
            {
                Interlocked.Increment(ref mismatches);
            }
        });
        TestDiagnostics.For(TestContext).Act("mismatches", mismatches);

        Assert.AreEqual(0, mismatches);
    }

    // Family 1, boundaries: key lengths just inside and just outside each documented range.
    [TestMethod]
    [DataRow("Rc4", 0, false)]
    [DataRow("Rc4", 1, true)]
    [DataRow("Rc4", 256, true)]
    [DataRow("Rc4", 257, false)]
    [DataRow("Blowfish", 0, false)]
    [DataRow("Blowfish", 1, true)]
    [DataRow("Blowfish", 56, true)]
    [DataRow("Blowfish", 57, false)]
    [DataRow("Cast128", 4, false)]
    [DataRow("Cast128", 5, true)]
    [DataRow("Cast128", 16, true)]
    [DataRow("Cast128", 17, false)]
    [DataRow("Des", 7, false)]
    [DataRow("Des", 8, true)]
    [DataRow("Des", 9, false)]
    [DataRow("Camellia", 15, false)]
    [DataRow("Camellia", 16, true)]
    [DataRow("Camellia", 20, false)]
    [DataRow("Camellia", 32, true)]
    [DataRow("Camellia", 33, false)]
    [DataRow("Aria", 0, false)]
    [DataRow("Aria", 24, true)]
    [DataRow("Aria", 31, false)]
    [DataRow("AesCtr", 17, false)]
    [DataRow("AesCtr", 24, true)]
    [DataRow("AesCtr", 64, false)]
    public void Cipher_KeyLengthAtTheEdgeOfItsRange_IsAcceptedOnlyInsideIt(string cipher, int keyLength, bool accepted)
    {
        TestDiagnostics.For(TestContext).Arrange("cipher and key length", $"{cipher}/{keyLength}");
        byte[] key = Filled(keyLength, 0xff);

        Action create = cipher switch
        {
            "Rc4" => () => new Rc4(key).Dispose(),
            "Blowfish" => () => new Blowfish(key).Dispose(),
            "Cast128" => () => new Cast128(key).Dispose(),
            "Des" => () => new Des(key).Dispose(),
            "Camellia" => () => new Camellia(key).Dispose(),
            "Aria" => () => new Aria(key).Dispose(),
            _ => () => new AesCtr(key, new byte[AesCtr.BlockSize]).Dispose(),
        };

        if (accepted)
        {
            create();
        }
        else
        {
            Assert.ThrowsExactly<ArgumentException>(create);
        }
    }

    // Family 3, invalid partitions: a block one byte short, one byte long, and a CBC run that is not whole blocks.
    [TestMethod]
    [DataRow("Des", 7)]
    [DataRow("Des", 9)]
    [DataRow("Blowfish", 0)]
    [DataRow("Blowfish", 16)]
    [DataRow("Cast128", 7)]
    [DataRow("Camellia", 15)]
    [DataRow("Camellia", 17)]
    [DataRow("Aria", 15)]
    [DataRow("Aria", 32)]
    public void BlockCipher_BlockOfTheWrongLength_ThrowsArgumentExceptionBothWays(string cipher, int blockLength)
    {
        TestDiagnostics.For(TestContext).Arrange("cipher and block length", $"{cipher}/{blockLength}");
        byte[] block = new byte[blockLength];
        byte[] destination = new byte[blockLength];

        (Action encrypt, Action decrypt, IDisposable instance) = cipher switch
        {
            "Des" => BothWays(new Des(new byte[8]), (c, s, d) => c.EncryptBlock(s, d), (c, s, d) => c.DecryptBlock(s, d), block, destination),
            "Blowfish" => BothWays(new Blowfish(new byte[8]), (c, s, d) => c.EncryptBlock(s, d), (c, s, d) => c.DecryptBlock(s, d), block, destination),
            "Cast128" => BothWays(new Cast128(new byte[16]), (c, s, d) => c.EncryptBlock(s, d), (c, s, d) => c.DecryptBlock(s, d), block, destination),
            "Camellia" => BothWays(new Camellia(new byte[16]), (c, s, d) => c.EncryptBlock(s, d), (c, s, d) => c.DecryptBlock(s, d), block, destination),
            _ => BothWays(new Aria(new byte[16]), (c, s, d) => c.EncryptBlock(s, d), (c, s, d) => c.DecryptBlock(s, d), block, destination),
        };

        using (instance)
        {
            Assert.ThrowsExactly<ArgumentException>(encrypt);
            Assert.ThrowsExactly<ArgumentException>(decrypt);
        }
    }

    // Family 3, invalid partitions: CBC input that is not a whole number of blocks, and an IV one byte short.
    [TestMethod]
    [DataRow(8, 7)]
    [DataRow(8, 9)]
    [DataRow(7, 8)]
    public void BlowfishAndCast128Cbc_PartialBlockOrShortVector_ThrowsArgumentException(int vectorLength, int sourceLength)
    {
        TestDiagnostics.For(TestContext).Arrange("vector and source", $"{vectorLength}/{sourceLength}");
        using var blowfish = new Blowfish(new byte[16]);
        using var cast = new Cast128(new byte[16]);

        Assert.ThrowsExactly<ArgumentException>(() => blowfish.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[sourceLength]));
        Assert.ThrowsExactly<ArgumentException>(() => blowfish.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[sourceLength]));
        Assert.ThrowsExactly<ArgumentException>(() => cast.EncryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[sourceLength]));
        Assert.ThrowsExactly<ArgumentException>(() => cast.DecryptCbc(new byte[vectorLength], new byte[sourceLength], new byte[sourceLength]));
    }

    // Family 4, state: a keystream applied in two pieces split at every offset equals one call.
    [TestMethod]
    public void Rc4AndAesCtr_KeyStreamSplitAtEveryOffset_EqualsOneCall()
    {
        byte[] source = Enumerable.Range(0, 49).Select(value => (byte)value).ToArray();
        byte[] rc4Expected = new byte[source.Length];
        byte[] ctrExpected = new byte[source.Length];
        byte[] key = Filled(16, 0x2b);
        byte[] counter = Filled(AesCtr.BlockSize, 0xff);
        using (var rc4 = new Rc4(key))
        using (var ctr = new AesCtr(key, counter))
        {
            rc4.ApplyKeyStream(source, rc4Expected);
            ctr.ApplyKeyStream(source, ctrExpected);
        }

        for (int split = 0; split <= source.Length; split++)
        {
            byte[] rc4Actual = new byte[source.Length];
            byte[] ctrActual = new byte[source.Length];
            using var rc4 = new Rc4(key);
            using var ctr = new AesCtr(key, counter);
            rc4.ApplyKeyStream(source.AsSpan(0, split), rc4Actual.AsSpan(0, split));
            rc4.ApplyKeyStream(source.AsSpan(split), rc4Actual.AsSpan(split));
            ctr.ApplyKeyStream(source.AsSpan(0, split), ctrActual.AsSpan(0, split));
            ctr.ApplyKeyStream(source.AsSpan(split), ctrActual.AsSpan(split));

            CollectionAssert.AreEqual(rc4Expected, rc4Actual, $"RC4 split at {split}.");
            CollectionAssert.AreEqual(ctrExpected, ctrActual, $"AES-CTR split at {split}.");
        }
    }

    // Family 1, boundaries: discarding 0 keystream bytes changes nothing; a negative count is refused.
    [TestMethod]
    public void Rc4_DiscardKeyStreamZeroOrNegative_ChangesNothingOrThrows()
    {
        byte[] expected = new byte[8];
        byte[] actual = new byte[8];
        using var reference = new Rc4([1, 2, 3]);
        using var rc4 = new Rc4([1, 2, 3]);
        reference.ApplyKeyStream(new byte[8], expected);

        rc4.DiscardKeyStream(0);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => rc4.DiscardKeyStream(-1));
        rc4.ApplyKeyStream(new byte[8], actual);

        CollectionAssert.AreEqual(expected, actual);
    }

    // Family 1 and 4: hashes fed at every split around the 64-byte block and its padding boundary equal the one-shot hash.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(55)]
    [DataRow(56)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(119)]
    [DataRow(120)]
    public void Md4AndRipemd160_InputFedInTwoPiecesAtEverySplit_EqualsTheOneShotHash(int length)
    {
        TestDiagnostics.For(TestContext).Arrange("input length", length);
        byte[] source = Enumerable.Range(0, length).Select(value => (byte)(value * 7)).ToArray();
        byte[] md4Expected = new byte[Md4.HashSize];
        byte[] ripemdExpected = new byte[Ripemd160.HashSize];
        Md4.HashData(source, md4Expected);
        Ripemd160.HashData(source, ripemdExpected);
        using var md4 = new Md4();
        using var ripemd = new Ripemd160();

        for (int split = 0; split <= length; split++)
        {
            byte[] md4Actual = new byte[Md4.HashSize];
            byte[] ripemdActual = new byte[Ripemd160.HashSize];
            md4.AppendData(source.AsSpan(0, split));
            md4.AppendData(source.AsSpan(split));
            md4.GetHashAndReset(md4Actual);
            ripemd.AppendData(source.AsSpan(0, split));
            ripemd.AppendData(source.AsSpan(split));
            ripemd.GetHashAndReset(ripemdActual);

            CollectionAssert.AreEqual(md4Expected, md4Actual, $"MD4 split at {split}.");
            CollectionAssert.AreEqual(ripemdExpected, ripemdActual, $"RIPEMD-160 split at {split}.");
        }
    }

    // Family 3, invalid partitions: hash and MAC destinations one byte off.
    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    public void Hash_DestinationOneByteOff_ThrowsArgumentException(int delta)
    {
        TestDiagnostics.For(TestContext).Arrange("delta", delta);

        Assert.ThrowsExactly<ArgumentException>(() => Md4.HashData([1], new byte[Md4.HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => Ripemd160.HashData([1], new byte[Ripemd160.HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => HmacRipemd160.HashData([1], [1], new byte[HmacRipemd160.HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => HmacRipemd160.Verify([1], [1], new byte[HmacRipemd160.HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData224([1], new byte[Sha3.Sha3_224HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData256([1], new byte[Sha3.Sha3_256HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData384([1], new byte[Sha3.Sha3_384HashSize + delta]));
        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData512([1], new byte[Sha3.Sha3_512HashSize + delta]));
    }

    // Family 2, malformed input: a flipped bit in every byte of an HMAC-RIPEMD-160 MAC, and an empty key.
    [TestMethod]
    [DataRow(0)]
    [DataRow(64)]
    [DataRow(65)]
    public void HmacRipemd160_Verify_SingleBitFlippedInEveryMacByte_ReturnsFalse(int keyLength)
    {
        TestDiagnostics.For(TestContext).Arrange("key length", keyLength);
        byte[] key = Filled(keyLength, 0x0b);
        byte[] mac = new byte[HmacRipemd160.HashSize];
        HmacRipemd160.HashData(key, [0x48, 0x69], mac);

        Assert.IsTrue(HmacRipemd160.Verify(key, [0x48, 0x69], mac));
        for (int index = 0; index < mac.Length; index++)
        {
            mac[index] ^= 0x80;
            Assert.IsFalse(HmacRipemd160.Verify(key, [0x48, 0x69], mac), $"Accepted with byte {index} flipped.");
            mac[index] ^= 0x80;
        }
    }

    // Family 1 and 4: SHAKE output read in pieces across the rate boundary equals one read.
    [TestMethod]
    [DataRow(1)]
    [DataRow(135)]
    [DataRow(136)]
    [DataRow(167)]
    [DataRow(168)]
    [DataRow(169)]
    public void Shake_OutputReadInPiecesOfEachSize_EqualsOneRead(int pieceLength)
    {
        TestDiagnostics.For(TestContext).Arrange("piece length", pieceLength);
        byte[] expected128 = new byte[400];
        byte[] expected256 = new byte[400];
        Shake.HashData128([0x61, 0x62, 0x63], expected128);
        Shake.HashData256([0x61, 0x62, 0x63], expected256);

        CollectionAssert.AreEqual(expected128, ReadInPieces(Shake.Create128(), pieceLength, expected128.Length));
        CollectionAssert.AreEqual(expected256, ReadInPieces(Shake.Create256(), pieceLength, expected256.Length));
    }

    // Family 4, order: appending after Read is refused as documented, and Reset makes the instance new again.
    [TestMethod]
    public void Shake_AppendAfterReadThenReset_RefusesThenStartsAgain()
    {
        byte[] expected = new byte[32];
        byte[] actual = new byte[32];
        Shake.HashData128([0x78], expected);
        using var shake = Shake.Create128();
        shake.AppendData([0x01]);
        shake.Read(new byte[1]);

        Assert.ThrowsExactly<InvalidOperationException>(() => shake.AppendData([0x02]));
        shake.Reset();
        shake.AppendData([0x78]);
        shake.Read(actual);

        CollectionAssert.AreEqual(expected, actual);
    }

    // Family 4, order: every disposable primitive refuses use after Dispose, and a second Dispose is harmless.
    [TestMethod]
    public void DisposablePrimitive_UsedAfterDispose_ThrowsObjectDisposedException()
    {
        var aead = new AeadChaCha20Poly1305(new byte[32]);
        var rc4 = new Rc4([1]);
        var ctr = new AesCtr(new byte[16], new byte[16]);
        var md4 = new Md4();
        var ripemd = new Ripemd160();
        var hmac = new HmacRipemd160([1]);
        var shake = Shake.Create256();
        var des = new Des(new byte[8]);
        var blowfish = new Blowfish([1]);
        var cast = new Cast128(new byte[5]);
        var camellia = new Camellia(new byte[16]);
        var aria = new Aria(new byte[16]);
        IDisposable[] all = [aead, rc4, ctr, md4, ripemd, hmac, shake, des, blowfish, cast, camellia, aria];
        foreach (IDisposable disposable in all)
        {
            disposable.Dispose();
            disposable.Dispose();
        }

        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.Encrypt(new byte[12], [], [], new byte[16]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aead.TryDecrypt(new byte[12], [], new byte[16], []));
        Assert.ThrowsExactly<ObjectDisposedException>(() => rc4.ApplyKeyStream([1], new byte[1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => rc4.DiscardKeyStream(1));
        Assert.ThrowsExactly<ObjectDisposedException>(() => ctr.ApplyKeyStream([1], new byte[1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => md4.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => md4.GetHashAndReset(new byte[Md4.HashSize]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => ripemd.GetHashAndReset(new byte[Ripemd160.HashSize]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => hmac.GetHashAndReset(new byte[HmacRipemd160.HashSize]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => shake.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => shake.Read(new byte[1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => shake.Reset());
        Assert.ThrowsExactly<ObjectDisposedException>(() => des.EncryptBlock(new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => blowfish.DecryptBlock(new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cast.EncryptCbc(new byte[8], new byte[8], new byte[8]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => camellia.DecryptCbc(new byte[16], new byte[16], new byte[16]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => aria.EncryptBlock(new byte[16], new byte[16]));
    }

    private static (Action Encrypt, Action Decrypt, IDisposable Instance) BothWays<T>(
        T cipher,
        Action<T, byte[], byte[]> encrypt,
        Action<T, byte[], byte[]> decrypt,
        byte[] source,
        byte[] destination)
        where T : IDisposable =>
        (() => encrypt(cipher, source, destination), () => decrypt(cipher, source, destination), cipher);

    private static byte[] ReadInPieces(Shake shake, int pieceLength, int totalLength)
    {
        using (shake)
        {
            byte[] output = new byte[totalLength];
            shake.AppendData([0x61, 0x62, 0x63]);
            for (int offset = 0; offset < totalLength; offset += pieceLength)
            {
                shake.Read(output.AsSpan(offset, Math.Min(pieceLength, totalLength - offset)));
            }

            return output;
        }
    }

    private static string Ed25519SignatureOver(byte[] privateKey, byte messageByte)
    {
        byte[] signature = new byte[Ed25519.SignatureSize];
        Ed25519.Sign(privateKey, [messageByte], signature);
        return Convert.ToHexStringLower(signature);
    }

    private static string X25519SecretWith(byte peerByte)
    {
        byte[] peer = new byte[X25519.KeySize];
        peer[0] = 9;
        peer[1] = peerByte;
        byte[] secret = new byte[X25519.KeySize];
        X25519.TryComputeSharedSecret(Convert.FromHexString(X25519PrivateKey), peer, secret);
        return Convert.ToHexStringLower(secret);
    }

    private static byte[] LittleEndian(BigInteger value, int length)
    {
        byte[] encoded = new byte[length];
        value.TryWriteBytes(encoded, out _, isUnsigned: true, isBigEndian: false);
        return encoded;
    }

    private static byte[] Filled(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }
}
