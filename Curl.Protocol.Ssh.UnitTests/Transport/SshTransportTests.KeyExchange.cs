using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Transport;

public sealed partial class SshTransportTests
{
    /// <summary>Measured 2026-09-29 (BL-564) for every failure inside the key exchange.</summary>
    private const string KeyExchangeMethodFailed = "Failure establishing ssh session: -8, Unable to exchange encryption keys";

    private const string StrictServer = "kex-strict-s-v00@openssh.com";

    private static byte[] ClientKexInit =>
        ClientKexInitFor(SshAlgorithmPreferences.Full);

    [TestMethod]
    [DataRow("mlkem768x25519-sha256")]
    [DataRow("mlkem768nistp256-sha256")]
    [DataRow("mlkem1024nistp384-sha384")]
    [DataRow("sntrup761x25519-sha512")]
    [DataRow("sntrup761x25519-sha512@openssh.com")]
    [DataRow("curve25519-sha256")]
    [DataRow("curve25519-sha256@libssh.org")]
    [DataRow("ecdh-sha2-nistp256")]
    [DataRow("ecdh-sha2-nistp384")]
    [DataRow("ecdh-sha2-nistp521")]
    [DataRow("diffie-hellman-group-exchange-sha256")]
    [DataRow("diffie-hellman-group16-sha512")]
    [DataRow("diffie-hellman-group18-sha512")]
    [DataRow("diffie-hellman-group14-sha256")]
    [DataRow("diffie-hellman-group14-sha1")]
    [DataRow("diffie-hellman-group1-sha1")]
    [DataRow("diffie-hellman-group-exchange-sha1")]
    public async Task ExchangeKeysAsync_EachMethod_ReachesTheServersExchangeHashAndKeys(string method)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script(method, TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));
        diagnostics.Arrange("key exchange method", method);
        diagnostics.Arrange("host key algorithm", "ecdsa-sha2-nistp256");

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("negotiated key exchange", result.Algorithms.KeyExchange);
        diagnostics.Bytes("exchange hash", result.ExchangeHash);
        diagnostics.Bytes("host key", result.HostKey);
        diagnostics.Diff("exchange hash", run.Server.ExchangeHash, result.ExchangeHash);
        diagnostics.Diff("session identifier", run.Server.ExchangeHash, result.SessionIdentifier);
        diagnostics.Assert("negotiated key exchange", method, result.Algorithms.KeyExchange);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.SessionIdentifier, "the first exchange's hash is the session identifier");
        CollectionAssert.AreEqual(run.Server.ExchangeHash, run.Transport.SessionIdentifier, "the transport keeps it for publickey signatures");
        CollectionAssert.AreEqual(TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256).Blob, result.HostKey, "the host key is kept for SshHostKeyChecker");
        Assert.AreEqual(method, result.Algorithms.KeyExchange);
        foreach (SshKeyPurpose purpose in Enum.GetValues<SshKeyPurpose>())
        {
            CollectionAssert.AreEqual(run.Server.DeriveKey((char)purpose, 40, run.Server.ExchangeHash), result.Keys.DeriveKey(purpose, 40), purpose.ToString());
        }

        List<byte[]> written = WrittenPayloads(run.Connection.Written);
        List<byte[]> expected = [ClientKexInit, .. run.Server.ClientPayloads, [SshMessageNumber.NewKeys]];
        diagnostics.Act("client messages written", written.Count);
        diagnostics.Assert("client messages written", expected.Count, written.Count);
        Assert.HasCount(expected.Count, written);
        for (int index = 0; index < expected.Count; index++)
        {
            diagnostics.Diff($"client message {index}", expected[index], written[index]);
            CollectionAssert.AreEqual(expected[index], written[index], $"client message {index}");
        }

        diagnostics.Assert("writer sequence number", 0u, run.Transport.PacketWriter.SequenceNumber);
        diagnostics.Assert("reader sequence number", 0u, run.Transport.PacketReader.SequenceNumber);
        Assert.AreEqual(0u, run.Transport.PacketWriter.SequenceNumber, "strict key exchange restarts at 0 after NEWKEYS");
        Assert.AreEqual(0u, run.Transport.PacketReader.SequenceNumber);
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("rsa-sha2-512")]
    [DataRow("rsa-sha2-256")]
    [DataRow("ssh-rsa")]
    [DataRow("ssh-dss")]
    [DataRow("ssh-ed25519")]
    [DataRow("ecdsa-sha2-nistp256-cert-v01@openssh.com")]
    [DataRow("ecdsa-sha2-nistp384-cert-v01@openssh.com")]
    [DataRow("ecdsa-sha2-nistp521-cert-v01@openssh.com")]
    [DataRow("rsa-sha2-512-cert-v01@openssh.com")]
    [DataRow("rsa-sha2-256-cert-v01@openssh.com")]
    [DataRow("ssh-rsa-cert-v01@openssh.com")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com")]
    [DataRow("sk-ecdsa-sha2-nistp256@openssh.com")]
    [DataRow("sk-ssh-ed25519@openssh.com")]
    [DataRow("sk-ecdsa-sha2-nistp256-cert-v01@openssh.com")]
    [DataRow("sk-ssh-ed25519-cert-v01@openssh.com")]
    public async Task ExchangeKeysAsync_EachHostKeyAlgorithm_VerifiesTheServersSignature(string hostKeyAlgorithm)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.For(hostKeyAlgorithm));
        diagnostics.Arrange("key exchange method", "ecdh-sha2-nistp256");
        diagnostics.Arrange("host key algorithm", hostKeyAlgorithm);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("negotiated host key algorithm", result.Algorithms.ServerHostKey);
        diagnostics.Bytes("exchange hash", result.ExchangeHash);
        diagnostics.Assert("negotiated host key algorithm", hostKeyAlgorithm, result.Algorithms.ServerHostKey);
        diagnostics.Diff("exchange hash", run.Server.ExchangeHash, result.ExchangeHash);
        Assert.AreEqual(hostKeyAlgorithm, result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
    }

    [TestMethod]
    public void SessionIdentifier_BeforeTheFirstExchange_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));
        diagnostics.Arrange("exchanges run so far", 0);

        InvalidOperationException failure = Assert.ThrowsExactly<InvalidOperationException>(() => run.Transport.SessionIdentifier);

        diagnostics.Act("exception type", failure.GetType().Name);
        diagnostics.Act("exception message", failure.Message);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), failure.GetType().Name);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedP256Keys_PinsTheExchangeHashAndTheSixKeys()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));
        diagnostics.Arrange("key exchange method", "ecdh-sha2-nistp256");
        diagnostics.Arrange("pinned exchange hash", PinnedP256ExchangeHash);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("exchange hash", Convert.ToHexString(result.ExchangeHash));
        diagnostics.Assert("exchange hash", PinnedP256ExchangeHash, Convert.ToHexString(result.ExchangeHash));
        Assert.AreEqual(PinnedP256ExchangeHash, Convert.ToHexString(result.ExchangeHash));
        string[] keys =
        [
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, 16)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.InitialIvServerToClient, 16)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyClientToServer, 32)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyServerToClient, 32)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.IntegrityKeyClientToServer, 64)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.IntegrityKeyServerToClient, 64)),
        ];
        diagnostics.Act("derived keys", string.Join(",", keys));
        diagnostics.Assert("derived keys", string.Join(",", PinnedP256Keys), string.Join(",", keys));
        Assert.AreEqual(string.Join(",", PinnedP256Keys), string.Join(",", keys));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedGroup14ExponentsAndDsaKey_PinsTheExchangeHash()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("diffie-hellman-group14-sha256", TestHostKey.Dsa());
        diagnostics.Arrange("key exchange method", "diffie-hellman-group14-sha256");
        diagnostics.Arrange("pinned exchange hash", PinnedGroup14ExchangeHash);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("exchange hash", Convert.ToHexString(result.ExchangeHash));
        diagnostics.Assert("exchange hash", PinnedGroup14ExchangeHash, Convert.ToHexString(result.ExchangeHash));
        Assert.AreEqual(PinnedGroup14ExchangeHash, Convert.ToHexString(result.ExchangeHash));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedX25519KeysAndEd25519HostKey_PinsTheExchangeHashAndTheSixKeys()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("curve25519-sha256", TestHostKey.Ed25519());
        diagnostics.Arrange("key exchange method", "curve25519-sha256");
        diagnostics.Arrange("pinned exchange hash", PinnedCurve25519ExchangeHash);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("host key algorithm", result.Algorithms.ServerHostKey);
        diagnostics.Act("exchange hash", Convert.ToHexString(result.ExchangeHash));
        diagnostics.Assert("host key algorithm", "ssh-ed25519", result.Algorithms.ServerHostKey);
        diagnostics.Diff("exchange hash against the test server's", run.Server.ExchangeHash, result.ExchangeHash);
        diagnostics.Assert("exchange hash", PinnedCurve25519ExchangeHash, Convert.ToHexString(result.ExchangeHash));
        Assert.AreEqual("ssh-ed25519", result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash, "H as RFC 8731 defines it, computed by the test server");
        Assert.AreEqual(PinnedCurve25519ExchangeHash, Convert.ToHexString(result.ExchangeHash));
        string[] keys =
        [
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, 16)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.InitialIvServerToClient, 16)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyClientToServer, 32)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyServerToClient, 32)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.IntegrityKeyClientToServer, 64)),
            Convert.ToHexString(result.Keys.DeriveKey(SshKeyPurpose.IntegrityKeyServerToClient, 64)),
        ];
        diagnostics.Act("derived keys", string.Join(",", keys));
        diagnostics.Assert("derived keys", string.Join(",", PinnedCurve25519Keys), string.Join(",", keys));
        Assert.AreEqual(string.Join(",", PinnedCurve25519Keys), string.Join(",", keys));
    }

    [TestMethod]
    [DataRow("00", DisplayName = "u = 0 gives an all-zero secret")]
    [DataRow("01", DisplayName = "u = 1 gives an all-zero secret")]
    [DataRow("short", DisplayName = "31 bytes")]
    [DataRow("long", DisplayName = "33 bytes")]
    public async Task ExchangeKeysAsync_UnusableX25519ServerKey_FailsWithMinus8(string defect)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TestHostKey hostKey = TestHostKey.Ed25519();
        byte[] serverKey = defect switch
        {
            "short" => new byte[31],
            "long" => new byte[33],
            _ => [.. Convert.FromHexString(defect), .. new byte[31]],
        };
        diagnostics.Arrange("defect", defect);
        diagnostics.Bytes("server X25519 key", serverKey);

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", hostKey, tamper: _ => [TestKeyExchangeServer.EcdhReply(hostKey.Blob, serverKey, hostKey.Sign([]))]));
    }

    [TestMethod]
    [DataRow("mlkem768x25519-sha256", 1088 + 32)]
    [DataRow("mlkem768nistp256-sha256", 1088 + 65)]
    [DataRow("mlkem1024nistp384-sha384", 1568 + 97)]
    [DataRow("sntrup761x25519-sha512", 1039 + 32)]
    [DataRow("sntrup761x25519-sha512@openssh.com", 1039 + 32)]
    public async Task ExchangeKeysAsync_HybridServerShareOfTheWrongLength_FailsWithMinus8(string method, int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TestHostKey hostKey = TestHostKey.Ed25519();
        diagnostics.Arrange("key exchange method", method);
        diagnostics.Arrange("correct server share length", length);

        foreach (int wrongLength in new[] { length - 1, length + 1, 0 })
        {
            diagnostics.Arrange("wrong server share length", wrongLength);
            await AssertKeyExchangeFailsAsync(Script(method, hostKey, tamper: _ => [TestKeyExchangeServer.EcdhReply(hostKey.Blob, new byte[wrongLength], hostKey.Sign([]))]));
        }
    }

    [TestMethod]
    [DataRow("mlkem768x25519-sha256", 1088, 32, DisplayName = "an X25519 key giving an all-zero secret")]
    [DataRow("mlkem768nistp256-sha256", 1088, 65, DisplayName = "a P-256 point off the curve")]
    [DataRow("sntrup761x25519-sha512", 1039, 32, DisplayName = "an sntrup761 exchange's all-zero X25519 key")]
    public async Task ExchangeKeysAsync_HybridServerShareWithAnUnusableClassicalKey_FailsWithMinus8(string method, int ciphertextLength, int classicalLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TestHostKey hostKey = TestHostKey.Ed25519();
        byte[] classical = new byte[classicalLength];
        classical[0] = (byte)(classicalLength == 65 ? 0x04 : 0x00);
        diagnostics.Arrange("key exchange method", method);
        diagnostics.Arrange("ciphertext length", ciphertextLength);
        diagnostics.Bytes("classical key", classical);

        await AssertKeyExchangeFailsAsync(Script(method, hostKey, tamper: _ => [TestKeyExchangeServer.EcdhReply(hostKey.Blob, [.. new byte[ciphertextLength], .. classical], hostKey.Sign([]))]));
    }

    [TestMethod]
    [DataRow("mlkem768x25519-sha256")]
    [DataRow("sntrup761x25519-sha512")]
    public async Task ExchangeKeysAsync_HybridCiphertextAltered_FailsWithMinus8BecauseTheSignatureNoLongerVerifies(string method)
    {
        TestDiagnostics.For(TestContext).Arrange("key exchange method", method);
        await AssertKeyExchangeFailsAsync(Script(method, TestHostKey.Ed25519(), tamper: payloads =>
        {
            byte[] reply = payloads[0];
            reply[reply.Length - 200] ^= 0x01;
            return payloads;
        }));
    }

    [TestMethod]
    [DataRow("key", DisplayName = "a 31-byte public key")]
    [DataRow("truncated", DisplayName = "no public key after the name")]
    [DataRow("signature", DisplayName = "a 63-byte signature")]
    public async Task ExchangeKeysAsync_MalformedEd25519HostKeyOrSignature_FailsWithMinus8(string defect)
    {
        TestDiagnostics.For(TestContext).Arrange("defect", defect);
        const string name = "ssh-ed25519";
        TestHostKey valid = TestHostKey.Ed25519();
        TestHostKey broken = defect switch
        {
            "key" => valid with { Blob = Join(Name(name), String(new byte[31])) },
            "truncated" => valid with { Blob = Name(name) },
            _ => valid with { Sign = h => Join(Name(name), String(valid.Sign(h)[^64..^1])) },
        };

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", broken));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_WithoutStrictKeyExchange_KeepsCountingSequenceNumbers_AndSkipsIgnoreDebugAndUnimplemented()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script(
            "diffie-hellman-group14-sha1",
            TestHostKey.Dsa(),
            strict: false,
            tamper: payloads => [[SshMessageNumber.Ignore, 0, 0, 0, 0], [SshMessageNumber.Debug, 0, 0, 0, 0, 0, 0, 0, 0, 0], [SshMessageNumber.Unimplemented, 0, 0, 0, 0], .. payloads]);
        diagnostics.Arrange("key exchange method", "diffie-hellman-group14-sha1");
        diagnostics.Arrange("strict key exchange", false);
        diagnostics.Arrange("extra server messages", "IGNORE, DEBUG, UNIMPLEMENTED before KEXDH_REPLY");

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("writer sequence number", run.Transport.PacketWriter.SequenceNumber);
        diagnostics.Act("reader sequence number", run.Transport.PacketReader.SequenceNumber);
        diagnostics.Diff("exchange hash", run.Server.ExchangeHash, result.ExchangeHash);
        diagnostics.Assert("writer sequence number", 3u, run.Transport.PacketWriter.SequenceNumber);
        diagnostics.Assert("reader sequence number", 6u, run.Transport.PacketReader.SequenceNumber);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
        Assert.AreEqual(3u, run.Transport.PacketWriter.SequenceNumber, "KEXINIT, KEXDH_INIT, NEWKEYS");
        Assert.AreEqual(6u, run.Transport.PacketReader.SequenceNumber, "KEXINIT, IGNORE, DEBUG, UNIMPLEMENTED, KEXDH_REPLY, NEWKEYS");
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_WrongServerGuess_DiscardsTheGuessedPacket()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshKexInit server = ServerKexInit("ecdh-sha2-nistp256", "ssh-dss", strict: false) with
        {
            KeyExchange = ["diffie-hellman-group14-sha256", "ecdh-sha2-nistp256"],
            FirstKexPacketFollows = true,
        };
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), serverKexInit: server, tamper: payloads => [[30, 0, 0, 0, 0], .. payloads]);
        diagnostics.Arrange("server key exchange offer", string.Join(",", server.KeyExchange));
        diagnostics.Arrange("server guesses wrongly and sends a packet first", server.FirstKexPacketFollows);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        diagnostics.Act("negotiated key exchange", result.Algorithms.KeyExchange);
        diagnostics.Diff("exchange hash", run.Server.ExchangeHash, result.ExchangeHash);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
    }

    [TestMethod]
    [DataRow("ecdsa-sha2-nistp256")]
    [DataRow("ecdsa-sha2-nistp384")]
    [DataRow("ecdsa-sha2-nistp521")]
    [DataRow("rsa-sha2-512")]
    [DataRow("rsa-sha2-256")]
    [DataRow("ssh-rsa")]
    [DataRow("ssh-dss")]
    [DataRow("ssh-ed25519")]
    [DataRow("ecdsa-sha2-nistp256-cert-v01@openssh.com")]
    [DataRow("ecdsa-sha2-nistp384-cert-v01@openssh.com")]
    [DataRow("ecdsa-sha2-nistp521-cert-v01@openssh.com")]
    [DataRow("rsa-sha2-512-cert-v01@openssh.com")]
    [DataRow("rsa-sha2-256-cert-v01@openssh.com")]
    [DataRow("ssh-rsa-cert-v01@openssh.com")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com")]
    [DataRow("sk-ecdsa-sha2-nistp256@openssh.com")]
    [DataRow("sk-ssh-ed25519@openssh.com")]
    [DataRow("sk-ecdsa-sha2-nistp256-cert-v01@openssh.com")]
    [DataRow("sk-ssh-ed25519-cert-v01@openssh.com")]
    public async Task ExchangeKeysAsync_BadSignature_FailsWithMinus8AsMeasured(string hostKeyAlgorithm)
    {
        TestDiagnostics.For(TestContext).Arrange("host key algorithm with a flipped last signature bit", hostKeyAlgorithm);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.For(hostKeyAlgorithm), tamper: payloads =>
        {
            byte[] reply = payloads[^1];
            reply[^1] ^= 0x01;
            return payloads;
        });

        await AssertKeyExchangeFailsAsync(run);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_SignatureNamesAnotherAlgorithm_FailsWithMinus8AsMeasured()
    {
        TestDiagnostics.For(TestContext).Arrange("host key signed with rsa-sha2-512, offered as", "rsa-sha2-256");
        ScriptedExchange run = Script("diffie-hellman-group14-sha256", TestHostKey.For("rsa-sha2-512"), offeredHostKey: "rsa-sha2-256");

        await AssertKeyExchangeFailsAsync(run);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_HostKeyOfAnotherType_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("host key type sent, offered as", "ssh-dss, rsa-sha2-256");
        ScriptedExchange run = Script("diffie-hellman-group14-sha256", TestHostKey.Dsa(), offeredHostKey: "rsa-sha2-256");

        await AssertKeyExchangeFailsAsync(run);
    }

    [TestMethod]
    [DataRow("curve", DisplayName = "the key names another curve")]
    [DataRow("point", DisplayName = "the key's point is off the curve")]
    [DataRow("r", DisplayName = "r is longer than the field")]
    [DataRow("s", DisplayName = "s is longer than the field")]
    public async Task ExchangeKeysAsync_MalformedEcdsaHostKeyOrSignature_FailsWithMinus8(string defect)
    {
        TestDiagnostics.For(TestContext).Arrange("defect", defect);
        const string name = "ecdsa-sha2-nistp256";
        TestHostKey valid = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        byte[] point = [0x04, .. TestHostKey.FixedNistP256.Q.X!, .. TestHostKey.FixedNistP256.Q.Y!];
        byte[] offCurve = [.. point[..^1], (byte)(point[^1] ^ 1)];
        byte[] tooLong = [0x01, .. new byte[32]];
        TestHostKey broken = defect switch
        {
            "curve" => valid with { Blob = Join(Name(name), Name("nistp384"), String(point)) },
            "point" => valid with { Blob = Join(Name(name), Name("nistp256"), String(offCurve)) },
            "r" => valid with { Sign = _ => Join(Name(name), String(Join(Mpint(tooLong), Mpint([1])))) },
            _ => valid with { Sign = _ => Join(Name(name), String(Join(Mpint([1]), Mpint(tooLong)))) },
        };

        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", broken));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_RsaSignatureLongerThanTheModulus_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("RSA signature length", 257);
        TestHostKey valid = TestHostKey.For("rsa-sha2-256");
        TestHostKey broken = valid with { Sign = h => Join(Name("rsa-sha2-256"), String(new byte[257])) };

        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", broken));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_RsaHostKeyThePlatformRefuses_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("RSA host key", "empty modulus and exponent");
        TestHostKey broken = new("rsa-sha2-256", Join(Name("ssh-rsa"), Mpint([]), Mpint([])), _ => Join(Name("rsa-sha2-256"), String([])));

        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", broken));
    }

    [TestMethod]
    [DataRow("", DisplayName = "f = 0, measured")]
    [DataRow("01", DisplayName = "f = 1, measured")]
    [DataRow("p-1", DisplayName = "f = p - 1")]
    [DataRow("p", DisplayName = "f = p")]
    public async Task ExchangeKeysAsync_ServerPublicValueOutsideTheGroup_FailsWithMinus8AsMeasured(string f)
    {
        TestDiagnostics.For(TestContext).Arrange("server public value f", f);
        TestHostKey hostKey = TestHostKey.Dsa();
        byte[] prime = Cryptography.FiniteFieldDiffieHellmanGroup.Group14.Prime.ToArray();
        byte[] primeMinusOne = [.. prime[..^1], (byte)(prime[^1] - 1)];
        byte[] value = f switch
        {
            "p-1" => primeMinusOne,
            "p" => prime,
            _ => Convert.FromHexString(f),
        };
        ScriptedExchange run = Script(
            "diffie-hellman-group14-sha256",
            hostKey,
            tamper: _ => [TestKeyExchangeServer.FiniteFieldReply(31, hostKey.Blob, value, hostKey.Sign([]))]);

        await AssertKeyExchangeFailsAsync(run);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_NegativeServerPublicValue_FailsWithMinus8()
    {
        TestHostKey hostKey = TestHostKey.Dsa();
        byte[] reply = [31, .. String(hostKey.Blob), .. String([0x80, 1]), .. String(hostKey.Sign([]))];
        TestDiagnostics.For(TestContext).Arrange("server public value (negative mpint)", "80 01");
        TestDiagnostics.For(TestContext).Bytes("KEXDH_REPLY", reply);

        await AssertKeyExchangeFailsAsync(Script("diffie-hellman-group14-sha256", hostKey, tamper: _ => [reply]));
    }

    [TestMethod]
    [DataRow("short", DisplayName = "one coordinate only")]
    [DataRow("compressed", DisplayName = "not uncompressed")]
    [DataRow("off", DisplayName = "not on the curve")]
    [DataRow("x", DisplayName = "x not below p")]
    [DataRow("y", DisplayName = "y not below p")]
    public async Task ExchangeKeysAsync_ServerPointNotOnTheCurve_FailsWithMinus8(string defect)
    {
        TestDiagnostics.For(TestContext).Arrange("defect", defect);
        TestHostKey hostKey = TestHostKey.Dsa();
        byte[] point = [0x04, .. TestHostKey.FixedNistP256.Q.X!, .. TestHostKey.FixedNistP256.Q.Y!];
        byte[] allOnes = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        byte[] bad = defect switch
        {
            "short" => point[..33],
            "compressed" => [0x02, .. point[1..]],
            "off" => [.. point[..^1], (byte)(point[^1] ^ 1)],
            "x" => [0x04, .. allOnes, .. point[33..]],
            _ => [.. point[..33], .. allOnes],
        };

        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", hostKey, tamper: _ => [TestKeyExchangeServer.EcdhReply(hostKey.Blob, bad, hostKey.Sign([]))]));
    }

    [TestMethod]
    [DataRow("Windows", "small", DisplayName = "Windows: a 1024-bit prime, under the 2048 asked for")]
    [DataRow("Windows", "large", DisplayName = "Windows: an 8192-bit prime, over the 4096 asked for")]
    [DataRow("OpenSSL", "small", DisplayName = "OpenSSL: a 1024-bit prime, under the 2048 asked for")]
    [DataRow("OpenSSL", "larger", DisplayName = "OpenSSL: an 8193-bit number, over the 8192 asked for")]
    [DataRow("OpenSSL", "generator", DisplayName = "OpenSSL: generator 1")]
    public async Task ExchangeKeysAsync_UnusableGroupExchangeGroup_FailsWithMinus8(string platform, string defect)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform preset", platform);
        diagnostics.Arrange("defect", defect);
        byte[] prime = defect switch
        {
            "small" => Cryptography.FiniteFieldDiffieHellmanGroup.Group2.Prime.ToArray(),
            "large" => Cryptography.FiniteFieldDiffieHellmanGroup.Group18.Prime.ToArray(),
            "larger" => [0x01, .. Enumerable.Repeat((byte)0xFF, 1024)],
            _ => Cryptography.FiniteFieldDiffieHellmanGroup.Group14.Prime.ToArray(),
        };
        byte[] group = [31, .. Mpint(prime), .. Mpint(defect == "generator" ? [1] : [2])];
        diagnostics.Bytes("server group message", group);

        await AssertKeyExchangeFailsAsync(Script(
            "diffie-hellman-group-exchange-sha256",
            TestHostKey.For("rsa-sha2-256"),
            tamper: payloads => [group, payloads[1]],
            preferences: Preset(platform)));
    }

    [TestMethod]
    [DataRow("Windows", "diffie-hellman-group-exchange-sha256", "22000008000000100000001000", DisplayName = "Windows, -sha256: (2048, 4096, 4096), measured (BL-564)")]
    [DataRow("Windows", "diffie-hellman-group-exchange-sha1", "22000008000000100000001000", DisplayName = "Windows, -sha1: (2048, 4096, 4096), measured (BL-564)")]
    [DataRow("OpenSSL", "diffie-hellman-group-exchange-sha256", "22000008000000100000002000", DisplayName = "OpenSSL, -sha256: (2048, 4096, 8192), measured (BL-888)")]
    [DataRow("OpenSSL", "diffie-hellman-group-exchange-sha1", "22000008000000100000002000", DisplayName = "OpenSSL, -sha1: (2048, 4096, 8192), measured (BL-888)")]
    public async Task ExchangeKeysAsync_GroupExchangeOnEachPreset_AsksForThePresetsSizesAndAcceptsAPrimeAtItsMaximum(string platform, string method, string request)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        SshAlgorithmPreferences preset = Preset(platform);
        Cryptography.FiniteFieldDiffieHellmanGroup largest = platform == "Windows"
            ? Cryptography.FiniteFieldDiffieHellmanGroup.Group16
            : Cryptography.FiniteFieldDiffieHellmanGroup.Group18;
        ScriptedExchange run;
        using (diagnostics.Phase("script the server"))
        {
            run = Script(method, TestHostKey.For("rsa-sha2-256"), preferences: preset, exchangedGroup: largest);
        }

        diagnostics.Arrange("platform preset", platform);
        diagnostics.Arrange("key exchange method", method);
        diagnostics.Arrange("expected request", request);

        SshKeyExchangeResult result;
        using (diagnostics.Phase("key exchange"))
        {
            result = await ExchangeAsync(run);
        }

        List<byte[]> written = WrittenPayloads(run.Connection.Written);
        diagnostics.Bytes("client group exchange request", written[1]);
        diagnostics.Act("server prime length", largest.PrimeLength);
        diagnostics.Assert("group exchange request", request, Convert.ToHexString(written[1]));
        diagnostics.Assert("prime length", (int)preset.GroupExchangeSizes.MaximumBits / 8, largest.PrimeLength);
        diagnostics.Diff("exchange hash", run.Server.ExchangeHash, result.ExchangeHash);
        Assert.AreEqual(request, Convert.ToHexString(written[1]), "the client's SSH_MSG_KEX_DH_GEX_REQUEST");
        Assert.AreEqual((int)preset.GroupExchangeSizes.MaximumBits / 8, largest.PrimeLength, "the server's prime is at the preset's maximum");
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
    }

    [TestMethod]
    [DataRow(SshMessageNumber.Disconnect, true)]
    [DataRow((byte)50, true)]
    [DataRow(SshMessageNumber.Disconnect, false)]
    [DataRow((byte)50, false)]
    public async Task ExchangeKeysAsync_UnexpectedMessageInsteadOfTheReply_FailsWithMinus8(byte messageNumber, bool strict)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("message number sent instead of the reply", messageNumber);
        diagnostics.Arrange("strict key exchange", strict);
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), strict, tamper: _ => [[messageNumber, 0, 0, 0, 0]]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_IgnoreDuringStrictKeyExchange_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("server sends IGNORE before the reply", "strict key exchange");
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), tamper: payloads => [[SshMessageNumber.Ignore, 0, 0, 0, 0], .. payloads]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_PeerClosesBeforeTheReply_FailsWithMinus8AsMeasured()
    {
        TestDiagnostics.For(TestContext).Arrange("server script", "KEXINIT, then closes before the reply");
        await AssertKeyExchangeFailsAsync(Script("diffie-hellman-group14-sha256", TestHostKey.Dsa(), tamper: _ => [], newKeys: false));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_PeerClosesBeforeNewKeys_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("server script", "KEXINIT and reply, then closes before NEWKEYS");
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), newKeys: false));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_PeerResetsTheConnectionBeforeTheReply_FailsWithMinus8AsAClose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] serverBytes = new SshServerScript()
            .Line(TestKeyExchangeServer.ServerIdentification)
            .KexInit(ServerKexInit("ecdh-sha2-nistp256", "ssh-dss", strict: true))
            .Bytes;
        diagnostics.Arrange("server bytes (identification and KEXINIT, then the connection resets)", serverBytes.Length);
        diagnostics.Bytes("server bytes", serverBytes);
        SshTransport transport = new(new ResettingConnection(false, serverBytes), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        SshTransferException failure;
        using (diagnostics.Phase("key exchange"))
        {
            failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
                async () => await transport.ExchangeKeysAsync(handshake, CancellationToken.None));
        }

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("message", KeyExchangeMethodFailed, failure.Message);
        Assert.AreEqual(KeyExchangeMethodFailed, failure.Message);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_AnotherMessageInsteadOfNewKeys_FailsWithMinus8()
    {
        TestDiagnostics.For(TestContext).Arrange("server sends instead of NEWKEYS", "KEXINIT");
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), tamper: payloads => [.. payloads, [SshMessageNumber.KeyExchangeInit]]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_Cancelled_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] serverBytes = new SshServerScript()
            .Line(TestKeyExchangeServer.ServerIdentification)
            .KexInit(ServerKexInit("ecdh-sha2-nistp256", "ssh-dss", strict: true))
            .Bytes;
        diagnostics.Arrange("cancellation token", "already cancelled");
        diagnostics.Bytes("server bytes", serverBytes);
        SshTransport transport = new(new ScriptedConnection(serverBytes, [0]), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await transport.ExchangeKeysAsync(handshake, new CancellationToken(canceled: true)));

        diagnostics.Act("exception type", cancelled.GetType().Name);
        diagnostics.Assert("exception type", nameof(OperationCanceledException), cancelled.GetType().Name);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ReExchangeKeysAsync_ServerSendsKexInitLater_RunsANewExchangeAndKeepsTheSessionIdentifier(bool strict)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("strict key exchange", strict);
        diagnostics.Arrange("first key exchange method", "ecdh-sha2-nistp256");
        diagnostics.Arrange("second key exchange method", "diffie-hellman-group14-sha256");
        TestHostKey hostKey = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        SshKexInit firstServerKexInit = ServerKexInit("ecdh-sha2-nistp256", hostKey.Algorithm, strict);
        SshKexInit secondServerKexInit = ServerKexInit("diffie-hellman-group14-sha256", hostKey.Algorithm, strict: false);
        TestEphemeralKeys keys = new();
        TestKeyExchangeServer first = TestKeyExchangeServer.Answer("ecdh-sha2-nistp256", hostKey, keys, ClientKexInit, firstServerKexInit.ToPayload());
        TestKeyExchangeServer second = TestKeyExchangeServer.Answer("diffie-hellman-group14-sha256", hostKey, keys, ClientKexInit, secondServerKexInit.ToPayload());
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(firstServerKexInit);
        first.ServerPayloads.ForEach(payload => script.Packet(payload));
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(ctr, first.Keys(first.ExchangeHash)), strict)
            .Packet(SshMessageNumber.Ignore, 0, 0, 0, 0);
        second.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys);
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        SshKeyExchangeResult firstResult = await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);

        SshKeyExchangeResult secondResult;
        using (diagnostics.Phase("re-exchange"))
        {
            secondResult = await transport.ReExchangeKeysAsync(secondServerKexInit.ToPayload(), CancellationToken.None);
        }

        diagnostics.Act("second key exchange", secondResult.Algorithms.KeyExchange);
        diagnostics.Bytes("second exchange hash", secondResult.ExchangeHash);
        diagnostics.Assert("second key exchange", "diffie-hellman-group14-sha256", secondResult.Algorithms.KeyExchange);
        diagnostics.Diff("second exchange hash", second.ExchangeHash, secondResult.ExchangeHash);
        diagnostics.Diff("session identifier", firstResult.ExchangeHash, secondResult.SessionIdentifier);
        Assert.AreEqual("diffie-hellman-group14-sha256", secondResult.Algorithms.KeyExchange);
        CollectionAssert.AreEqual(second.ExchangeHash, secondResult.ExchangeHash);
        CollectionAssert.AreEqual(firstResult.ExchangeHash, secondResult.SessionIdentifier);
        CollectionAssert.AreEqual(
            second.DeriveKey('C', 32, first.ExchangeHash),
            secondResult.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyClientToServer, 32));
        List<byte[]> written = await SshClientTranscript.PayloadsAsync(connection.Written, strict, SshPacketProtections.ForClientToServer(ctr, first.Keys(first.ExchangeHash)));
        diagnostics.Diff("client KEXINIT", ClientKexInit, written[3]);
        diagnostics.Diff("client key exchange message", second.ClientPayloads[0], written[4]);
        diagnostics.Assert("writer sequence number", strict ? 0u : 6u, transport.PacketWriter.SequenceNumber);
        diagnostics.Assert("reader sequence number", strict ? 0u : 6u, transport.PacketReader.SequenceNumber);
        CollectionAssert.AreEqual(ClientKexInit, written[3], "the client answers with its own KEXINIT, encrypted with the first keys");
        CollectionAssert.AreEqual(second.ClientPayloads[0], written[4]);
        Assert.AreEqual(strict ? 0u : 6u, transport.PacketWriter.SequenceNumber, "strict key exchange restarts at every NEWKEYS");
        Assert.AreEqual(strict ? 0u : 6u, transport.PacketReader.SequenceNumber);
    }

    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "MAC-then-encrypt")]
    [DataRow("aes256-ctr", "hmac-sha2-512-etm@openssh.com", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "encrypt-then-MAC")]
    [DataRow("aes128-ctr", "hmac-sha1-etm@openssh.com", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "hmac-sha1-etm")]
    [DataRow("aes128-ctr", "hmac-md5-etm@openssh.com", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "hmac-md5-etm")]
    [DataRow("aes256-gcm@openssh.com", null, -1, "Failure establishing ssh session: -12, Unable to exchange encryption keys", DisplayName = "AES-GCM")]
    [DataRow("chacha20-poly1305@openssh.com", null, -1, "Failure establishing ssh session: -12, Unable to exchange encryption keys", DisplayName = "ChaCha20-Poly1305 tag")]
    [DataRow("chacha20-poly1305@openssh.com", null, 3, KeyExchangeMethodFailed, DisplayName = "ChaCha20-Poly1305 length, off the block size")]
    [DataRow("chacha20-poly1305@openssh.com", null, 0, "Failure establishing ssh session: -41, Unable to exchange encryption keys", DisplayName = "ChaCha20-Poly1305 length, over the maximum")]
    [DataRow("aes128-ctr", "hmac-sha2-256", 0, "Failure establishing ssh session: -41, Unable to exchange encryption keys", DisplayName = "aes128-ctr length, over the maximum")]
    public async Task ReExchangeKeysAsync_ServerPacketFailsItsCheck_EndsTheSessionWithLibssh2sCode(string cipher, string? mac, int alteredByte, string expectedMessage)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("cipher", cipher);
        diagnostics.Arrange("mac", mac ?? "(none)");
        diagnostics.Arrange("altered byte (-1 is the last)", alteredByte);
        diagnostics.Arrange("expected message", expectedMessage);
        TestHostKey hostKey = TestHostKey.Dsa();
        SshKexInit serverKexInit = ServerKexInit("ecdh-sha2-nistp256", hostKey.Algorithm, strict: true) with
        {
            CipherClientToServer = [cipher],
            CipherServerToClient = [cipher],
            MacClientToServer = [mac ?? "hmac-sha2-256"],
            MacServerToClient = [mac ?? "hmac-sha2-256"],
        };
        TestEphemeralKeys keys = new();
        TestKeyExchangeServer first = TestKeyExchangeServer.Answer("ecdh-sha2-nistp256", hostKey, keys, ClientKexInit, serverKexInit.ToPayload());
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(serverKexInit);
        first.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), first.Keys(first.ExchangeHash)), resetSequenceNumber: true)
            .Packet([SshMessageNumber.Ignore, 0, 0, 0, 0], sealedPacket => sealedPacket[alteredByte < 0 ? sealedPacket.Length - 1 : alteredByte] ^= 0x01);
        SshTransport transport = new(new ScriptedConnection(script.Bytes), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        byte[] secondServerKexInit = ServerKexInit("diffie-hellman-group14-sha256", hostKey.Algorithm, strict: false).ToPayload();

        SshTransferException failure;
        using (diagnostics.Phase("re-exchange"))
        {
            failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
                async () => await transport.ReExchangeKeysAsync(secondServerKexInit, CancellationToken.None));
        }

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        diagnostics.Assert("message", expectedMessage, failure.Message);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(expectedMessage, failure.Message);
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_BeforeAnyExchange_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exchanges run so far", 0);
        SshTransport transport = new(new ScriptedConnection(), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await transport.ReExchangeKeysAsync(ClientKexInit, CancellationToken.None));

        diagnostics.Act("exception type", failure.GetType().Name);
        diagnostics.Act("exception message", failure.Message);
        diagnostics.Assert("exception type", nameof(InvalidOperationException), failure.GetType().Name);
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_MalformedKexInit_FailsWithMinus8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa());
        await ExchangeAsync(run);
        byte[] malformed = [SshMessageNumber.KeyExchangeInit, 1, 2, 3];
        diagnostics.Arrange("malformed KEXINIT", "message number then 3 bytes");
        diagnostics.Bytes("malformed KEXINIT", malformed);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await run.Transport.ReExchangeKeysAsync(malformed, CancellationToken.None));

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("message", KeyExchangeMethodFailed, failure.Message);
        Assert.AreEqual(KeyExchangeMethodFailed, failure.Message);
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_NoSharedAlgorithm_FailsWithMinus5()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa());
        await ExchangeAsync(run);
        byte[] unshared = ServerKexInit("unknown-kex@example.com", "ssh-dss", strict: false).ToPayload();
        diagnostics.Arrange("server key exchange offer", "unknown-kex@example.com");
        diagnostics.Bytes("second server KEXINIT", unshared);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await run.Transport.ReExchangeKeysAsync(unshared, CancellationToken.None));

        const string expectedMessage = "Failure establishing ssh session: -5, Unable to exchange encryption keys";
        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("message", expectedMessage, failure.Message);
        Assert.AreEqual("Failure establishing ssh session: -5, Unable to exchange encryption keys", failure.Message);
    }

    [TestMethod]
    public void Create_MethodNotImplemented_ThrowsNotSupported()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key exchange method", "unknown-kex@example.com");

        NotSupportedException failure = Assert.ThrowsExactly<NotSupportedException>(() => SshKeyExchangeMethods.Create("unknown-kex@example.com", new TestEphemeralKeys(), SshGroupExchangeSizes.OpenSslReference));

        diagnostics.Act("exception type", failure.GetType().Name);
        diagnostics.Act("exception message", failure.Message);
        diagnostics.Assert("exception type", nameof(NotSupportedException), failure.GetType().Name);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_HostKeyNotImplemented_ThrowsNotSupported()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string hostKeyName = "unknown-host-key@example.com";
        diagnostics.Arrange("host key algorithm", hostKeyName);
        SshAlgorithmCatalogue withUnimplemented = new([.. SshAlgorithmPreferences.Full.KeyExchange, "ssh-dss", hostKeyName, "aes128-ctr", "hmac-sha2-256", "none"]);
        foreach ((string method, string hostKey) in new[] { ("ecdh-sha2-nistp256", hostKeyName) })
        {
            byte[] serverBytes = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(ServerKexInit(method, hostKey, strict: false)).Bytes;
            SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.Full with { ServerHostKey = [hostKeyName] }, withUnimplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
            SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

            NotSupportedException failure = await Assert.ThrowsExactlyAsync<NotSupportedException>(
                async () => await transport.ExchangeKeysAsync(handshake, CancellationToken.None));

            diagnostics.Act("exception type", failure.GetType().Name);
            diagnostics.Act("exception message", failure.Message);
            diagnostics.Assert("exception type", nameof(NotSupportedException), failure.GetType().Name);
        }
    }

    private const string PinnedP256ExchangeHash = "B7430B6960F34567154E732736A9C0A2CD5308D044A7984B4A43A367C96068D3";

    private static readonly string[] PinnedP256Keys =
    [
        "27408378DBEDA04267A661FB6260E9BC",
        "55E7BF5CD71DFC3682B787244DC616C3",
        "522B03B5ACBE7E587F3DA153979553E2161359FE78CDE7E643AE286A09150457",
        "52880EA49E2C29265B459F10B0BCA5B69FEC81ABDFF2EE303486BAEEE0F89833",
        "6897DF7A66123460AD78C1D675F6E87DE9D50C5730FE7D59931B54C78302EDF8FFF7E967A2EAC5EBD6621257ECD496AAA748D8A3AF56FF24A81868C3EED9D3A7",
        "0A898B744EAC47A319035EE81A181061843480BAF5AF7693962CDA855481065469D21AE6E5B946F33FDAC42158A085AD3D06E32C696A8184207BA388FA56E6A4",
    ];

    private const string PinnedCurve25519ExchangeHash = "0C524D421BAD267BDD2DF593FD5DBC7100E06C650BA142E344F746B7DB02D4EF";

    private static readonly string[] PinnedCurve25519Keys =
    [
        "7068C2B58B62580D73423BF4C5712CC8",
        "AD20B3F9CC5D76A40A93E6BFA4CDD365",
        "B8EDAFDC83BB559D68E733066ADC979AE6C93BD9D23B2839757657D8D4BDD19A",
        "A4DEA14CE464A997ACBBE58A7D61BD98052EBDF936612E808E6D33131C7C2F7B",
        "401909CFF74C73E151EB19A7E962511143A08EBC283BDE13AD09A298F4F80D9DCBFFAC96850B86B38F4E7F87A0DF6DD31E3B1D6E0C542C0774BF25D09E4D0BE7",
        "FB6E6D39F7443A0E23CDBE894C3AF331D0DD3C4EF6B4C3067BB9AD241D600A1DE34E8C28704BAFF174CFB31AFE359FF8203AD6C1EEBF0BC8F7A7FCE151E5B7D5",
    ];

    private const string PinnedGroup14ExchangeHash = "E00B7ACDC1E39CF85CCB4C1A94907F329E83EB15471197304BA94883DC661243";

    private static SshAlgorithmPreferences Preset(string platform) =>
        platform == "Windows" ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;

    private static byte[] ClientKexInitFor(SshAlgorithmPreferences preferences) =>
        SshKexInit.ForClient(preferences, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();

    private static SshKexInit ServerKexInit(string method, string hostKey, bool strict) =>
        SshServerScript.OpenSshKexInit(kexInit => kexInit with
        {
            KeyExchange = strict ? [method, StrictServer] : [method],
            ServerHostKey = [hostKey],
            CipherClientToServer = ["aes128-ctr"],
            CipherServerToClient = ["aes128-ctr"],
            MacClientToServer = ["hmac-sha2-256"],
            MacServerToClient = ["hmac-sha2-256"],
        });

    private static ScriptedExchange Script(
        string method,
        TestHostKey hostKey,
        bool strict = true,
        string? offeredHostKey = null,
        Func<List<byte[]>, List<byte[]>>? tamper = null,
        bool newKeys = true,
        SshKexInit? serverKexInit = null,
        SshAlgorithmPreferences? preferences = null,
        Cryptography.FiniteFieldDiffieHellmanGroup? exchangedGroup = null)
    {
        TestEphemeralKeys keys = new();
        SshKexInit server = serverKexInit ?? ServerKexInit(method, offeredHostKey ?? hostKey.Algorithm, strict);
        SshAlgorithmPreferences preset = preferences ?? SshAlgorithmPreferences.Full;
        TestKeyExchangeServer answer = TestKeyExchangeServer.Answer(method, hostKey, keys, ClientKexInitFor(preset), server.ToPayload(), preset.GroupExchangeSizes, exchangedGroup);
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(server);
        List<byte[]> payloads = [.. answer.ServerPayloads.Select(payload => payload.ToArray())];
        (tamper is null ? payloads : tamper(payloads)).ForEach(payload => script.Packet(payload));
        if (newKeys)
        {
            script.Packet(SshMessageNumber.NewKeys);
        }

        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, preset, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        return new ScriptedExchange(transport, connection, answer);
    }

    private static async Task<SshKeyExchangeResult> ExchangeAsync(ScriptedExchange run)
    {
        SshNegotiatedHandshake handshake = await run.Transport.NegotiateAlgorithmsAsync(CancellationToken.None);
        return await run.Transport.ExchangeKeysAsync(handshake, CancellationToken.None);
    }

    private async Task AssertKeyExchangeFailsAsync(ScriptedExchange run)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected exit code", CurlExitCode.FailedInit);
        diagnostics.Arrange("expected message", KeyExchangeMethodFailed);
        SshNegotiatedHandshake handshake = await run.Transport.NegotiateAlgorithmsAsync(CancellationToken.None);
        diagnostics.Arrange("negotiated key exchange", handshake.Algorithms.KeyExchange);

        SshTransferException failure;
        using (diagnostics.Phase("key exchange"))
        {
            failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
                async () => await run.Transport.ExchangeKeysAsync(handshake, CancellationToken.None));
        }

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        diagnostics.Assert("message", KeyExchangeMethodFailed, failure.Message);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(KeyExchangeMethodFailed, failure.Message);
    }

    private sealed record ScriptedExchange(SshTransport Transport, ScriptedConnection Connection, TestKeyExchangeServer Server);
}
