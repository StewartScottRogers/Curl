using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Transport;

public sealed partial class SshTransportTests
{
    /// <summary>Measured 2026-09-29 (BL-564) for every failure inside the key exchange.</summary>
    private const string KeyExchangeMethodFailed = "Failure establishing ssh session: -8, Unable to exchange encryption keys";

    private const string StrictServer = "kex-strict-s-v00@openssh.com";

    private static byte[] ClientKexInit =>
        SshKexInit.ForClient(SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();

    [TestMethod]
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
        ScriptedExchange run = Script(method, TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));

        SshKeyExchangeResult result = await ExchangeAsync(run);

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
        Assert.HasCount(expected.Count, written);
        for (int index = 0; index < expected.Count; index++)
        {
            CollectionAssert.AreEqual(expected[index], written[index], $"client message {index}");
        }

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
    public async Task ExchangeKeysAsync_EachHostKeyAlgorithm_VerifiesTheServersSignature(string hostKeyAlgorithm)
    {
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.For(hostKeyAlgorithm));

        SshKeyExchangeResult result = await ExchangeAsync(run);

        Assert.AreEqual(hostKeyAlgorithm, result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
    }

    [TestMethod]
    public void SessionIdentifier_BeforeTheFirstExchange_Throws()
    {
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));

        Assert.ThrowsExactly<InvalidOperationException>(() => run.Transport.SessionIdentifier);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedP256Keys_PinsTheExchangeHashAndTheSixKeys()
    {
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256));

        SshKeyExchangeResult result = await ExchangeAsync(run);

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
        Assert.AreEqual(string.Join(",", PinnedP256Keys), string.Join(",", keys));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedGroup14ExponentsAndDsaKey_PinsTheExchangeHash()
    {
        ScriptedExchange run = Script("diffie-hellman-group14-sha256", TestHostKey.Dsa());

        SshKeyExchangeResult result = await ExchangeAsync(run);

        Assert.AreEqual(PinnedGroup14ExchangeHash, Convert.ToHexString(result.ExchangeHash));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_FixedX25519KeysAndEd25519HostKey_PinsTheExchangeHashAndTheSixKeys()
    {
        ScriptedExchange run = Script("curve25519-sha256", TestHostKey.Ed25519());

        SshKeyExchangeResult result = await ExchangeAsync(run);

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
        Assert.AreEqual(string.Join(",", PinnedCurve25519Keys), string.Join(",", keys));
    }

    [TestMethod]
    [DataRow("00", DisplayName = "u = 0 gives an all-zero secret")]
    [DataRow("01", DisplayName = "u = 1 gives an all-zero secret")]
    [DataRow("short", DisplayName = "31 bytes")]
    [DataRow("long", DisplayName = "33 bytes")]
    public async Task ExchangeKeysAsync_UnusableX25519ServerKey_FailsWithMinus8(string defect)
    {
        TestHostKey hostKey = TestHostKey.Ed25519();
        byte[] serverKey = defect switch
        {
            "short" => new byte[31],
            "long" => new byte[33],
            _ => [.. Convert.FromHexString(defect), .. new byte[31]],
        };

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", hostKey, tamper: _ => [TestKeyExchangeServer.EcdhReply(hostKey.Blob, serverKey, hostKey.Sign([]))]));
    }

    [TestMethod]
    [DataRow("key", DisplayName = "a 31-byte public key")]
    [DataRow("truncated", DisplayName = "no public key after the name")]
    [DataRow("signature", DisplayName = "a 63-byte signature")]
    public async Task ExchangeKeysAsync_MalformedEd25519HostKeyOrSignature_FailsWithMinus8(string defect)
    {
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
        ScriptedExchange run = Script(
            "diffie-hellman-group14-sha1",
            TestHostKey.Dsa(),
            strict: false,
            tamper: payloads => [[SshMessageNumber.Ignore, 0, 0, 0, 0], [SshMessageNumber.Debug, 0, 0, 0, 0, 0, 0, 0, 0, 0], [SshMessageNumber.Unimplemented, 0, 0, 0, 0], .. payloads]);

        SshKeyExchangeResult result = await ExchangeAsync(run);

        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
        Assert.AreEqual(3u, run.Transport.PacketWriter.SequenceNumber, "KEXINIT, KEXDH_INIT, NEWKEYS");
        Assert.AreEqual(6u, run.Transport.PacketReader.SequenceNumber, "KEXINIT, IGNORE, DEBUG, UNIMPLEMENTED, KEXDH_REPLY, NEWKEYS");
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_WrongServerGuess_DiscardsTheGuessedPacket()
    {
        SshKexInit server = ServerKexInit("ecdh-sha2-nistp256", "ssh-dss", strict: false) with
        {
            KeyExchange = ["diffie-hellman-group14-sha256", "ecdh-sha2-nistp256"],
            FirstKexPacketFollows = true,
        };
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), serverKexInit: server, tamper: payloads => [[30, 0, 0, 0, 0], .. payloads]);

        SshKeyExchangeResult result = await ExchangeAsync(run);

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
    public async Task ExchangeKeysAsync_BadSignature_FailsWithMinus8AsMeasured(string hostKeyAlgorithm)
    {
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
        ScriptedExchange run = Script("diffie-hellman-group14-sha256", TestHostKey.For("rsa-sha2-512"), offeredHostKey: "rsa-sha2-256");

        await AssertKeyExchangeFailsAsync(run);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_HostKeyOfAnotherType_FailsWithMinus8()
    {
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
        TestHostKey valid = TestHostKey.For("rsa-sha2-256");
        TestHostKey broken = valid with { Sign = h => Join(Name("rsa-sha2-256"), String(new byte[257])) };

        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", broken));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_RsaHostKeyThePlatformRefuses_FailsWithMinus8()
    {
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
    [DataRow("small", DisplayName = "a 1024-bit prime, under the 2048 asked for")]
    [DataRow("large", DisplayName = "an 8192-bit prime, over the 4096 asked for")]
    [DataRow("generator", DisplayName = "generator 1")]
    public async Task ExchangeKeysAsync_UnusableGroupExchangeGroup_FailsWithMinus8(string defect)
    {
        byte[] group = defect switch
        {
            "small" => [31, .. Mpint(Cryptography.FiniteFieldDiffieHellmanGroup.Group2.Prime.ToArray()), .. Mpint([2])],
            "large" => [31, .. Mpint(Cryptography.FiniteFieldDiffieHellmanGroup.Group18.Prime.ToArray()), .. Mpint([2])],
            _ => [31, .. Mpint(Cryptography.FiniteFieldDiffieHellmanGroup.Group14.Prime.ToArray()), .. Mpint([1])],
        };

        await AssertKeyExchangeFailsAsync(Script("diffie-hellman-group-exchange-sha256", TestHostKey.Dsa(), tamper: payloads => [group, payloads[1]]));
    }

    [TestMethod]
    [DataRow(SshMessageNumber.Disconnect, true)]
    [DataRow((byte)50, true)]
    [DataRow(SshMessageNumber.Disconnect, false)]
    [DataRow((byte)50, false)]
    public async Task ExchangeKeysAsync_UnexpectedMessageInsteadOfTheReply_FailsWithMinus8(byte messageNumber, bool strict)
    {
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), strict, tamper: _ => [[messageNumber, 0, 0, 0, 0]]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_IgnoreDuringStrictKeyExchange_FailsWithMinus8()
    {
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), tamper: payloads => [[SshMessageNumber.Ignore, 0, 0, 0, 0], .. payloads]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_PeerClosesBeforeTheReply_FailsWithMinus8AsMeasured()
    {
        await AssertKeyExchangeFailsAsync(Script("diffie-hellman-group14-sha256", TestHostKey.Dsa(), tamper: _ => [], newKeys: false));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_PeerClosesBeforeNewKeys_FailsWithMinus8()
    {
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), newKeys: false));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_AnotherMessageInsteadOfNewKeys_FailsWithMinus8()
    {
        await AssertKeyExchangeFailsAsync(Script("ecdh-sha2-nistp256", TestHostKey.Dsa(), tamper: payloads => [.. payloads, [SshMessageNumber.KeyExchangeInit]]));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_Cancelled_Throws()
    {
        byte[] serverBytes = new SshServerScript()
            .Line(TestKeyExchangeServer.ServerIdentification)
            .KexInit(ServerKexInit("ecdh-sha2-nistp256", "ssh-dss", strict: true))
            .Bytes;
        SshTransport transport = new(new ScriptedConnection(serverBytes, [0]), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await transport.ExchangeKeysAsync(handshake, new CancellationToken(canceled: true)));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ReExchangeKeysAsync_ServerSendsKexInitLater_RunsANewExchangeAndKeepsTheSessionIdentifier(bool strict)
    {
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

        SshKeyExchangeResult secondResult = await transport.ReExchangeKeysAsync(secondServerKexInit.ToPayload(), CancellationToken.None);

        Assert.AreEqual("diffie-hellman-group14-sha256", secondResult.Algorithms.KeyExchange);
        CollectionAssert.AreEqual(second.ExchangeHash, secondResult.ExchangeHash);
        CollectionAssert.AreEqual(firstResult.ExchangeHash, secondResult.SessionIdentifier);
        CollectionAssert.AreEqual(
            second.DeriveKey('C', 32, first.ExchangeHash),
            secondResult.Keys.DeriveKey(SshKeyPurpose.EncryptionKeyClientToServer, 32));
        List<byte[]> written = await SshClientTranscript.PayloadsAsync(connection.Written, strict, SshPacketProtections.ForClientToServer(ctr, first.Keys(first.ExchangeHash)));
        CollectionAssert.AreEqual(ClientKexInit, written[3], "the client answers with its own KEXINIT, encrypted with the first keys");
        CollectionAssert.AreEqual(second.ClientPayloads[0], written[4]);
        Assert.AreEqual(strict ? 0u : 6u, transport.PacketWriter.SequenceNumber, "strict key exchange restarts at every NEWKEYS");
        Assert.AreEqual(strict ? 0u : 6u, transport.PacketReader.SequenceNumber);
    }

    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "MAC-then-encrypt")]
    [DataRow("aes256-ctr", "hmac-sha2-512-etm@openssh.com", -1, "Failure establishing ssh session: -4, Unable to exchange encryption keys", DisplayName = "encrypt-then-MAC")]
    [DataRow("aes256-gcm@openssh.com", null, -1, "Failure establishing ssh session: -12, Unable to exchange encryption keys", DisplayName = "AES-GCM")]
    [DataRow("chacha20-poly1305@openssh.com", null, -1, "Failure establishing ssh session: -12, Unable to exchange encryption keys", DisplayName = "ChaCha20-Poly1305 tag")]
    [DataRow("chacha20-poly1305@openssh.com", null, 3, KeyExchangeMethodFailed, DisplayName = "ChaCha20-Poly1305 length, off the block size")]
    public async Task ReExchangeKeysAsync_ServerPacketFailsItsCheck_EndsTheSessionWithLibssh2sCode(string cipher, string? mac, int alteredByte, string expectedMessage)
    {
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

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.ReExchangeKeysAsync(secondServerKexInit, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(expectedMessage, failure.Message);
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_BeforeAnyExchange_Throws()
    {
        SshTransport transport = new(new ScriptedConnection(), SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await transport.ReExchangeKeysAsync(ClientKexInit, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_MalformedKexInit_FailsWithMinus8()
    {
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa());
        await ExchangeAsync(run);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await run.Transport.ReExchangeKeysAsync([SshMessageNumber.KeyExchangeInit, 1, 2, 3], CancellationToken.None));

        Assert.AreEqual(KeyExchangeMethodFailed, failure.Message);
    }

    [TestMethod]
    public async Task ReExchangeKeysAsync_NoSharedAlgorithm_FailsWithMinus5()
    {
        ScriptedExchange run = Script("ecdh-sha2-nistp256", TestHostKey.Dsa());
        await ExchangeAsync(run);
        byte[] unshared = ServerKexInit("unknown-kex@example.com", "ssh-dss", strict: false).ToPayload();

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await run.Transport.ReExchangeKeysAsync(unshared, CancellationToken.None));

        Assert.AreEqual("Failure establishing ssh session: -5, Unable to exchange encryption keys", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_MethodOrHostKeyNotImplemented_ThrowsNotSupported()
    {
        const string hostCertificate = "ssh-ed25519-cert-v01@openssh.com";
        SshAlgorithmCatalogue withUnimplemented = new([.. SshAlgorithmPreferences.Full.KeyExchange, "ssh-dss", hostCertificate, "aes128-ctr", "hmac-sha2-256", "none"]);
        foreach ((string method, string hostKey) in new[] { ("sntrup761x25519-sha512@openssh.com", "ssh-dss"), ("ecdh-sha2-nistp256", hostCertificate) })
        {
            byte[] serverBytes = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(ServerKexInit(method, hostKey, strict: false)).Bytes;
            SshTransport transport = new(new ScriptedConnection(serverBytes), SshAlgorithmPreferences.Full, withUnimplemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
            SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

            await Assert.ThrowsExactlyAsync<NotSupportedException>(
                async () => await transport.ExchangeKeysAsync(handshake, CancellationToken.None));
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
        SshKexInit? serverKexInit = null)
    {
        TestEphemeralKeys keys = new();
        SshKexInit server = serverKexInit ?? ServerKexInit(method, offeredHostKey ?? hostKey.Algorithm, strict);
        TestKeyExchangeServer answer = TestKeyExchangeServer.Answer(method, hostKey, keys, ClientKexInit, server.ToPayload());
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(server);
        List<byte[]> payloads = [.. answer.ServerPayloads.Select(payload => payload.ToArray())];
        (tamper is null ? payloads : tamper(payloads)).ForEach(payload => script.Packet(payload));
        if (newKeys)
        {
            script.Packet(SshMessageNumber.NewKeys);
        }

        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        return new ScriptedExchange(transport, connection, answer);
    }

    private static async Task<SshKeyExchangeResult> ExchangeAsync(ScriptedExchange run)
    {
        SshNegotiatedHandshake handshake = await run.Transport.NegotiateAlgorithmsAsync(CancellationToken.None);
        return await run.Transport.ExchangeKeysAsync(handshake, CancellationToken.None);
    }

    private static async Task AssertKeyExchangeFailsAsync(ScriptedExchange run)
    {
        SshNegotiatedHandshake handshake = await run.Transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await run.Transport.ExchangeKeysAsync(handshake, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(KeyExchangeMethodFailed, failure.Message);
    }

    private sealed record ScriptedExchange(SshTransport Transport, ScriptedConnection Connection, TestKeyExchangeServer Server);
}
