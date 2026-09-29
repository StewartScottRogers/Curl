using Curl.Tls;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicInitialSecrets" />, <see cref="QuicPacketKeys" />,
/// <see cref="QuicHeaderProtection" /> and <see cref="QuicPacketProtection" /> against
/// every sample of RFC 9001 Appendix A, drop tampered packets, and move through key
/// phases as RFC 9001 section 6 describes.
/// </summary>
[TestClass]
public sealed class QuicPacketProtectionTests
{
    /// <summary>RFC 9001 Appendix A: the client-chosen Destination Connection ID.</summary>
    private const string ClientDestinationConnectionId = "8394c8f03e515708";

    /// <summary>RFC 9001 Appendix A.2: the CRYPTO frame carrying the ClientHello.</summary>
    private const string ClientInitialCryptoFrame =
        """
        060040f1010000ed0303ebf8fa56f129 39b9584a3896472ec40bb863cfd3e868
        04fe3a47f06a2b69484c000004130113 02010000c000000010000e00000b6578
        616d706c652e636f6dff01000100000a 00080006001d00170018001000070005
        04616c706e0005000501000000000033 00260024001d00209370b2c9caa47fba
        baf4559fedba753de171fa71f50f1ce1 5d43e994ec74d748002b000302030400
        0d0010000e0403050306030203080408 050806002d00020101001c0002400100
        3900320408ffffffffffffffff050480 00ffff07048000ffff08011001048000
        75300901100f088394c8f03e51570806 048000ffff
        """;

    /// <summary>RFC 9001 Appendix A.2: the protected client Initial.</summary>
    private const string ProtectedClientInitial =
        """
        c000000001088394c8f03e5157080000 449e7b9aec34d1b1c98dd7689fb8ec11
        d242b123dc9bd8bab936b47d92ec356c 0bab7df5976d27cd449f63300099f399
        1c260ec4c60d17b31f8429157bb35a12 82a643a8d2262cad67500cadb8e7378c
        8eb7539ec4d4905fed1bee1fc8aafba1 7c750e2c7ace01e6005f80fcb7df6212
        30c83711b39343fa028cea7f7fb5ff89 eac2308249a02252155e2347b63d58c5
        457afd84d05dfffdb20392844ae81215 4682e9cf012f9021a6f0be17ddd0c208
        4dce25ff9b06cde535d0f920a2db1bf3 62c23e596d11a4f5a6cf3948838a3aec
        4e15daf8500a6ef69ec4e3feb6b1d98e 610ac8b7ec3faf6ad760b7bad1db4ba3
        485e8a94dc250ae3fdb41ed15fb6a8e5 eba0fc3dd60bc8e30c5c4287e53805db
        059ae0648db2f64264ed5e39be2e20d8 2df566da8dd5998ccabdae053060ae6c
        7b4378e846d29f37ed7b4ea9ec5d82e7 961b7f25a9323851f681d582363aa5f8
        9937f5a67258bf63ad6f1a0b1d96dbd4 faddfcefc5266ba6611722395c906556
        be52afe3f565636ad1b17d508b73d874 3eeb524be22b3dcbc2c7468d54119c74
        68449a13d8e3b95811a198f3491de3e7 fe942b330407abf82a4ed7c1b311663a
        c69890f4157015853d91e923037c227a 33cdd5ec281ca3f79c44546b9d90ca00
        f064c99e3dd97911d39fe9c5d0b23a22 9a234cb36186c4819e8b9c5927726632
        291d6a418211cc2962e20fe47feb3edf 330f2c603a9d48c0fcb5699dbfe58964
        25c5bac4aee82e57a85aaf4e2513e4f0 5796b07ba2ee47d80506f8d2c25e50fd
        14de71e6c418559302f939b0e1abd576 f279c4b2e0feb85c1f28ff18f58891ff
        ef132eef2fa09346aee33c28eb130ff2 8f5b766953334113211996d20011a198
        e3fc433f9f2541010ae17c1bf202580f 6047472fb36857fe843b19f5984009dd
        c324044e847a4f4a0ab34f719595de37 252d6235365e9b84392b061085349d73
        203a4a13e96f5432ec0fd4a1ee65accd d5e3904df54c1da510b0ff20dcc0c77f
        cb2c0e0eb605cb0504db87632cf3d8b4 dae6e705769d1de354270123cb11450e
        fc60ac47683d7b8d0f811365565fd98c 4c8eb936bcab8d069fc33bd801b03ade
        a2e1fbc5aa463d08ca19896d2bf59a07 1b851e6c239052172f296bfb5e724047
        90a2181014f3b94a4e97d117b4381303 68cc39dbb2d198065ae3986547926cd2
        162f40a29f0c3c8745c0f50fba3852e5 66d44575c29d39a03f0cda721984b6f4
        40591f355e12d439ff150aab7613499d bd49adabc8676eef023b15b65bfc5ca0
        6948109f23f350db82123535eb8a7433 bdabcb909271a6ecbcb58b936a88cd4e
        8f2e6ff5800175f113253d8fa9ca8885 c2f552e657dc603f252e1a8e308f76f0
        be79e2fb8f5d5fbbe2e30ecadd220723 c8c0aea8078cdfcb3868263ff8f09400
        54da48781893a7e49ad5aff4af300cd8 04a6b6279ab3ff3afb64491c85194aab
        760d58a606654f9f4400e8b38591356f bf6425aca26dc85244259ff2b19c41b9
        f96f3ca9ec1dde434da7d2d392b905dd f3d1f9af93d1af5950bd493f5aa731b4
        056df31bd267b6b90a079831aaf579be 0a39013137aac6d404f518cfd4684064
        7e78bfe706ca4cf5e9c5453e9f7cfd2b 8b4c8d169a44e55c88d4a9a7f9474241
        e221af44860018ab0856972e194cd934
        """;

    /// <summary>RFC 9001 Appendix A.3: the server Initial's ACK and CRYPTO frames.</summary>
    private const string ServerInitialFrames =
        """
        02000000000600405a020000560303ee fce7f7b37ba1d1632e96677825ddf739
        88cfc79825df566dc5430b9a045a1200 130100002e00330024001d00209d3c94
        0d89690b84d08a60993c144eca684d10 81287c834d5311bcf32bb9da1a002b00
        020304
        """;

    /// <summary>RFC 9001 Appendix A.3: the protected server Initial.</summary>
    private const string ProtectedServerInitial =
        """
        cf000000010008f067a5502a4262b500 4075c0d95a482cd0991cd25b0aac406a
        5816b6394100f37a1c69797554780bb3 8cc5a99f5ede4cf73c3ec2493a1839b3
        dbcba3f6ea46c5b7684df3548e7ddeb9 c3bf9c73cc3f3bded74b562bfb19fb84
        022f8ef4cdd93795d77d06edbb7aaf2f 58891850abbdca3d20398c276456cbc4
        2158407dd074ee
        """;

    /// <summary>RFC 9001 Appendix A.4: the Retry packet.</summary>
    private const string RetryPacket = "ff000000010008f067a5502a4262b5746f6b656e04a265ba2eff4d829058fb3f0f2496ba";

    /// <summary>RFC 9001 Appendix A.5: the server's 1-RTT secret.</summary>
    private const string ChaChaSecret = "9ac312a7f877468ebe69422748ad00a15443f18203a07d6060f688f30f21632b";

    /// <summary>RFC 9001 Appendix A.5: the protected short-header packet.</summary>
    private const string ProtectedChaChaPacket = "4cfe4189655e5cd55c41f69080575d7999c25a5bfb";

    private const ulong ChaChaPacketNumber = 654360564;

    private static readonly byte[] ServerConnectionId = Hex("f067a5502a4262b5");

    [TestMethod]
    public void DeriveSecrets_Rfc9001AppendixA1_ReproducesTheInitialSecrets()
    {
        var connectionId = Hex(ClientDestinationConnectionId);

        Assert.AreEqual("38762cf7f55934b34d179ae6a4c80cadccbb7f0a", Convert.ToHexStringLower(QuicInitialSecrets.InitialSalt));
        Assert.AreEqual("7db5df06e7a69e432496adedb00851923595221596ae2ae9fb8115c1e9ed0a44", HexOf(QuicInitialSecrets.DeriveInitialSecret(connectionId)));
        Assert.AreEqual("c00cf151ca5be075ed0ebfb5c80323c42d6b7db67881289af4008f1f6c357aea", HexOf(QuicInitialSecrets.DeriveClientInitialSecret(connectionId)));
        Assert.AreEqual("3c199828fd139efd216c155ad844cc81fb82fa8d7446fa7d78be803acdda951b", HexOf(QuicInitialSecrets.DeriveServerInitialSecret(connectionId)));
    }

    [TestMethod]
    [DataRow(true, "1f369613dd76d5467730efcbe3b1a22d", "fa044b2f42a3fd3b46fb255c", "9f50449e04a0e810283a1e9933adedd2")]
    [DataRow(false, "cf3a5331653c364c88f0f379b6067e37", "0ac1493ca1905853b0bba03e", "c206b8d9b9f0f37644430b490eeaa314")]
    public void Derive_Rfc9001AppendixA1InitialSecret_ReproducesKeyIvAndHeaderProtectionKey(bool client, string key, string iv, string headerProtectionKey)
    {
        var connectionId = Hex(ClientDestinationConnectionId);
        var secret = client ? QuicInitialSecrets.DeriveClientInitialSecret(connectionId) : QuicInitialSecrets.DeriveServerInitialSecret(connectionId);

        var keys = QuicPacketKeys.Derive(QuicInitialSecrets.CipherSuite, secret);

        Assert.AreEqual(key, HexOf(keys.Key));
        Assert.AreEqual(iv, HexOf(keys.Iv));
        Assert.AreEqual(headerProtectionKey, HexOf(keys.HeaderProtectionKey));
    }

    [TestMethod]
    public void Derive_Rfc9001AppendixA5Secret_ReproducesKeyIvHeaderProtectionKeyAndNextSecret()
    {
        var secret = Hex(ChaChaSecret);

        var keys = QuicPacketKeys.Derive(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);

        Assert.AreEqual("c6d98ff3441c3fe1b2182094f69caa2ed4b716b65488960a7a984979fb23e1c8", HexOf(keys.Key));
        Assert.AreEqual("e0459b3474bdd0e44a41c144", HexOf(keys.Iv));
        Assert.AreEqual("25a282b9e82f06f21f488917a4fc8f1b73573685608597d0efcb076b0ab7a7a4", HexOf(keys.HeaderProtectionKey));
        Assert.AreEqual("1223504755036d556342ee9361d253421a826c9ecdf3c7148684b36b714881f9", HexOf(QuicPacketKeys.DeriveNextSecret(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret)));
    }

    [TestMethod]
    public void Derive_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketKeys.Derive(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketKeys.Derive(QuicInitialSecrets.CipherSuite, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketKeys.DeriveNextSecret(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketKeys.DeriveNextSecret(QuicInitialSecrets.CipherSuite, null!));
    }

    [TestMethod]
    [DataRow("9f50449e04a0e810283a1e9933adedd2", "d1b1c98dd7689fb8ec11d242b123dc9b", "437b9aec36", 0x1301)]
    [DataRow("c206b8d9b9f0f37644430b490eeaa314", "2cd0991cd25b0aac406a5816b6394100", "2ec0d8356a", 0x1301)]
    [DataRow("25a282b9e82f06f21f488917a4fc8f1b73573685608597d0efcb076b0ab7a7a4", "5e5cd55c41f69080575d7999c25a5bfb", "aefefe7d03", 0x1303)]
    public void ComputeMask_Rfc9001AppendixASamples_ReproducesTheMask(string headerProtectionKey, string sample, string mask, int cipherSuite)
    {
        using var headerProtection = QuicHeaderProtection.Create(Tls13CipherSuite.Find((ushort)cipherSuite)!, Hex(headerProtectionKey));

        Assert.AreEqual(mask, HexOf(headerProtection.ComputeMask(Hex(sample))));
    }

    [TestMethod]
    public void ComputeMask_SampleNotSixteenBytes_Throws()
    {
        using var headerProtection = QuicHeaderProtection.Create(QuicInitialSecrets.CipherSuite, new byte[16]);

        Assert.ThrowsExactly<ArgumentException>(() => headerProtection.ComputeMask(new byte[15]));
    }

    [TestMethod]
    public void Create_NullKey_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicHeaderProtection.Create(QuicInitialSecrets.CipherSuite, null!));

    [TestMethod]
    public void Protect_Rfc9001AppendixA2ClientInitial_ReproducesTheProtectedPacket()
    {
        using var protection = QuicPacketProtection.CreateClientInitial(Hex(ClientDestinationConnectionId));

        var protectedPacket = protection.Protect(ClientInitial(), 2);

        Assert.AreEqual(HexOf(Hex(ProtectedClientInitial)), HexOf(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001AppendixA2ClientInitial_ReturnsThePlaintextPacket()
    {
        using var protection = QuicPacketProtection.CreateClientInitial(Hex(ClientDestinationConnectionId));

        var result = protection.Unprotect(Hex(ProtectedClientInitial), 0, null);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(2UL, result.PacketNumber);
        Assert.AreEqual(1200, result.Length);
        Assert.IsFalse(result.KeyPhaseChanged);
        var packet = (QuicLongHeaderPacket)result.Packet!;
        var expected = ClientInitial();
        Assert.AreEqual(QuicPacketType.Initial, packet.Type);
        Assert.AreEqual(4, packet.PacketNumberLength);
        Assert.AreEqual(2u, packet.TruncatedPacketNumber);
        Assert.AreEqual(ClientDestinationConnectionId, HexOf(packet.DestinationConnectionId));
        Assert.AreEqual(HexOf(expected.Payload), HexOf(packet.Payload));
    }

    [TestMethod]
    public void Protect_Rfc9001AppendixA3ServerInitial_ReproducesTheProtectedPacket()
    {
        using var protection = QuicPacketProtection.CreateServerInitial(Hex(ClientDestinationConnectionId));

        var protectedPacket = protection.Protect(ServerInitial(), 1);

        Assert.AreEqual(HexOf(Hex(ProtectedServerInitial)), HexOf(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001AppendixA3ServerInitialCoalescedWithMore_ReturnsThePlaintextPacketAndItsLength()
    {
        using var protection = QuicPacketProtection.CreateServerInitial(Hex(ClientDestinationConnectionId));
        var protectedPacket = Hex(ProtectedServerInitial);
        var datagram = protectedPacket.Concat(new byte[] { 0xe0, 0x00 }).ToArray();

        var result = protection.Unprotect(datagram, 0, 0);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(1UL, result.PacketNumber);
        Assert.AreEqual(protectedPacket.Length, result.Length);
        var packet = (QuicLongHeaderPacket)result.Packet!;
        Assert.AreEqual(HexOf(ServerConnectionId), HexOf(packet.SourceConnectionId));
        Assert.AreEqual(HexOf(Hex(ServerInitialFrames)), HexOf(packet.Payload));
    }

    [TestMethod]
    public void Unprotect_TamperedCiphertext_IsDroppedAsAuthenticationFailedWithItsLength()
    {
        using var protection = QuicPacketProtection.CreateClientInitial(Hex(ClientDestinationConnectionId));
        var tampered = Hex(ProtectedClientInitial);
        tampered[100] ^= 0x01;

        var result = protection.Unprotect(tampered, 0, null);

        Assert.AreEqual(new QuicUnprotectResult(QuicUnprotectStatus.DroppedAuthenticationFailed, null, 0, 1200, false), result);
    }

    [TestMethod]
    public void Unprotect_ServerInitialUnderTheClientKeys_IsDroppedAsAuthenticationFailed()
    {
        using var protection = QuicPacketProtection.CreateClientInitial(Hex(ClientDestinationConnectionId));

        var result = protection.Unprotect(Hex(ProtectedServerInitial), 0, null);

        Assert.AreEqual(QuicUnprotectStatus.DroppedAuthenticationFailed, result.Status);
        Assert.IsNull(result.Packet);
    }

    [TestMethod]
    public void Unprotect_PacketTooShortForTheSample_IsDropped()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));

        var result = protection.Unprotect(Hex(ProtectedChaChaPacket)[..20], 0, null);

        Assert.AreEqual(new QuicUnprotectResult(QuicUnprotectStatus.DroppedTooShortForSample, null, 0, 20, false), result);
    }

    [TestMethod]
    public void Protect_Rfc9001AppendixA5ChaCha20ShortHeader_ReproducesTheProtectedPacket()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));

        var protectedPacket = protection.Protect(ChaChaShortHeaderPacket(), ChaChaPacketNumber);

        Assert.AreEqual(ProtectedChaChaPacket, HexOf(protectedPacket));
    }

    [TestMethod]
    public void Unprotect_Rfc9001AppendixA5ChaCha20ShortHeader_ReturnsThePing()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));

        var result = protection.Unprotect(Hex(ProtectedChaChaPacket), 0, ChaChaPacketNumber - 1);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(ChaChaPacketNumber, result.PacketNumber);
        Assert.AreEqual(21, result.Length);
        var packet = (QuicShortHeaderPacket)result.Packet!;
        Assert.AreEqual(3, packet.PacketNumberLength);
        Assert.AreEqual(0xbff4u, packet.TruncatedPacketNumber);
        Assert.IsFalse(packet.KeyPhase);
        Assert.AreEqual("01", HexOf(packet.Payload));
    }

    [TestMethod]
    public void ProtectThenUnprotect_Aes256GcmShortHeader_RoundTrips()
    {
        var secret = Enumerable.Range(0, 48).Select(value => (byte)value).ToArray();
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.Aes256GcmSha384, secret);
        using var receiver = QuicPacketProtection.Create(Tls13CipherSuite.Aes256GcmSha384, secret);
        var packet = new QuicShortHeaderPacket(ServerConnectionId, 2, 0x1234, Hex("0100000000"), SpinBit: true);

        var result = receiver.Unprotect(sender.Protect(packet, 0x1234), ServerConnectionId.Length, 0x1200);

        Assert.AreEqual(QuicUnprotectStatus.Unprotected, result.Status);
        Assert.AreEqual(0x1234UL, result.PacketNumber);
        var received = (QuicShortHeaderPacket)result.Packet!;
        Assert.IsTrue(received.SpinBit);
        Assert.AreEqual(HexOf(ServerConnectionId), HexOf(received.DestinationConnectionId));
        Assert.AreEqual("0100000000", HexOf(received.Payload));
    }

    [TestMethod]
    public void Unprotect_AuthenticPacketWithAReservedBitSet_IsAProtocolViolation()
    {
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));
        using var receiver = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));
        var protectedPacket = sender.Protect(ChaChaShortHeaderPacket() with { ReservedBits = 1 }, ChaChaPacketNumber);

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, ErrorOf(() => receiver.Unprotect(protectedPacket, 0, ChaChaPacketNumber)));
    }

    [TestMethod]
    public void Unprotect_AuthenticLongHeaderWithAReservedBitSet_IsAProtocolViolation()
    {
        using var protection = QuicPacketProtection.CreateServerInitial(Hex(ClientDestinationConnectionId));
        var protectedPacket = protection.Protect(ServerInitial() with { ReservedBits = 2 }, 1);

        Assert.AreEqual(QuicTransportErrorCode.ProtocolViolation, ErrorOf(() => protection.Unprotect(protectedPacket, 0, null)));
    }

    [TestMethod]
    public void Protect_RetryOrVersionNegotiation_Throws()
    {
        using var protection = QuicPacketProtection.CreateClientInitial(Hex(ClientDestinationConnectionId));
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;

        Assert.ThrowsExactly<ArgumentException>(() => protection.Protect(retry, 0));
        Assert.ThrowsExactly<ArgumentException>(() => protection.Unprotect(Hex(RetryPacket), 0, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => protection.Protect(null!, 0));
    }

    [TestMethod]
    public void Protect_PacketNumberTheHeaderDoesNotCarry_Throws()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));

        Assert.ThrowsExactly<ArgumentException>(() => protection.Protect(ChaChaShortHeaderPacket(), ChaChaPacketNumber + 1));
    }

    [TestMethod]
    public void Protect_PayloadTooShortForTheSample_Throws()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));
        var packet = new QuicShortHeaderPacket(ReadOnlyMemory<byte>.Empty, 1, 5, Hex("0100"));

        Assert.ThrowsExactly<ArgumentException>(() => protection.Protect(packet, 5));
    }

    [TestMethod]
    public void Create_SuiteWithoutQuicProtection_Throws()
    {
        Assert.IsTrue(QuicPacketProtection.CanProtect(0x1301));
        Assert.IsTrue(QuicPacketProtection.CanProtect(0x1302));
        Assert.IsTrue(QuicPacketProtection.CanProtect(0x1303));
        Assert.IsFalse(QuicPacketProtection.CanProtect(0x1304));
        Assert.ThrowsExactly<ArgumentException>(() => QuicPacketProtection.Create(Tls13CipherSuite.Aes128CcmSha256, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketProtection.Create(null!, new byte[32]));
        Assert.ThrowsExactly<ArgumentNullException>(() => QuicPacketProtection.Create(QuicInitialSecrets.CipherSuite, null!));
    }

    [TestMethod]
    public void UpdateKeys_PeerMovesToTheNextPhase_ReceiverFollowsAndStillOpensReorderedOldPackets()
    {
        var secret = Hex(ChaChaSecret);
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        using var receiver = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        var first = sender.Protect(Ping(0), 0);
        var second = sender.Protect(Ping(1), 1);
        var third = sender.Protect(Ping(2), 2);
        sender.UpdateKeys();
        var fourth = sender.Protect(Ping(3), 3);

        Assert.IsFalse(receiver.Unprotect(first, 0, null).KeyPhaseChanged);
        Assert.IsFalse(receiver.Unprotect(third, 0, 0).KeyPhaseChanged);
        var updated = receiver.Unprotect(fourth, 0, 2);
        var reordered = receiver.Unprotect(second, 0, 3);

        Assert.IsTrue(sender.KeyPhase);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, updated.Status);
        Assert.IsTrue(updated.KeyPhaseChanged);
        Assert.IsTrue(((QuicShortHeaderPacket)updated.Packet!).KeyPhase);
        Assert.IsTrue(receiver.KeyPhase);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, reordered.Status);
        Assert.AreEqual(1UL, reordered.PacketNumber);
        Assert.IsFalse(reordered.KeyPhaseChanged);
        Assert.IsTrue(receiver.KeyPhase);
    }

    [TestMethod]
    public void UpdateKeys_PreviousKeysDiscarded_OldPacketsAreDroppedAndTheNextUpdateStillOpens()
    {
        var secret = Hex(ChaChaSecret);
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        using var receiver = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        var old = sender.Protect(Ping(0), 0);
        sender.UpdateKeys();
        var generationOne = sender.Protect(Ping(1), 1);
        sender.UpdateKeys();
        var generationTwo = sender.Protect(Ping(2), 2);

        Assert.IsTrue(receiver.Unprotect(generationOne, 0, null).KeyPhaseChanged);
        receiver.DiscardPreviousKeys();
        var dropped = receiver.Unprotect(old, 0, 1);
        var updated = receiver.Unprotect(generationTwo, 0, 1);

        Assert.AreEqual(QuicUnprotectStatus.DroppedAuthenticationFailed, dropped.Status);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, updated.Status);
        Assert.IsTrue(updated.KeyPhaseChanged);
        Assert.IsFalse(receiver.KeyPhase);
        Assert.IsFalse(sender.KeyPhase);
    }

    [TestMethod]
    public void UpdateKeys_LocalUpdateBeforeThePeer_OpensThePeersOldPhaseWithThePreviousKeys()
    {
        var secret = Hex(ChaChaSecret);
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        using var receiver = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        var old = sender.Protect(Ping(7), 7);
        receiver.UpdateKeys();
        receiver.UpdateKeys();
        receiver.UpdateKeys();

        var result = receiver.Unprotect(old, 0, null);

        Assert.AreEqual(QuicUnprotectStatus.DroppedAuthenticationFailed, result.Status);
        using var once = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, secret);
        once.UpdateKeys();
        var opened = once.Unprotect(old, 0, null);
        Assert.AreEqual(QuicUnprotectStatus.Unprotected, opened.Status);
        Assert.IsFalse(opened.KeyPhaseChanged);
        Assert.IsTrue(once.KeyPhase);
    }

    [TestMethod]
    public void Unprotect_ForgedPacketOfTheNextPhase_IsDroppedAndTheKeyPhaseStays()
    {
        using var sender = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));
        sender.UpdateKeys();
        var forged = sender.Protect(Ping(9), 9);
        forged[^1] ^= 0x01;
        var receiver = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));

        var result = receiver.Unprotect(forged, 0, null);

        Assert.AreEqual(QuicUnprotectStatus.DroppedAuthenticationFailed, result.Status);
        Assert.IsFalse(receiver.KeyPhase);
        receiver.Dispose();
    }

    [TestMethod]
    public void Protect_ShortHeaderAfterAnUpdate_CarriesTheCurrentKeyPhaseWhateverThePacketSays()
    {
        using var protection = QuicPacketProtection.Create(Tls13CipherSuite.ChaCha20Poly1305Sha256, Hex(ChaChaSecret));
        protection.UpdateKeys();
        protection.UpdateKeys();
        protection.DiscardPreviousKeys();
        protection.DiscardPreviousKeys();

        var protectedPacket = protection.Protect(Ping(4) with { KeyPhase = true }, 4);
        var result = protection.Unprotect(protectedPacket, 0, null);

        Assert.IsFalse(((QuicShortHeaderPacket)result.Packet!).KeyPhase);
    }

    [TestMethod]
    public void ComputeTag_Rfc9001AppendixA4Retry_ReproducesTheIntegrityTag()
    {
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;

        var tag = QuicRetryIntegrity.ComputeTag(Hex(ClientDestinationConnectionId), retry);

        Assert.AreEqual("04a265ba2eff4d829058fb3f0f2496ba", HexOf(tag));
        Assert.IsTrue(QuicRetryIntegrity.HasValidTag(Hex(ClientDestinationConnectionId), retry));
        Assert.AreEqual(RetryPacket, HexOf(QuicPacketCodec.Encode(retry with { RetryIntegrityTag = tag })));
    }

    [TestMethod]
    public void HasValidTag_TamperedTokenOrOtherConnectionId_IsFalse()
    {
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;

        Assert.IsFalse(QuicRetryIntegrity.HasValidTag(Hex(ClientDestinationConnectionId), retry with { RetryToken = Hex("746f6b656f") }));
        Assert.IsFalse(QuicRetryIntegrity.HasValidTag(Hex("8394c8f03e515709"), retry));
    }

    [TestMethod]
    public void ComputeTag_InvalidArguments_Throw()
    {
        var retry = (QuicRetryPacket)QuicPacketCodec.Decode(Hex(RetryPacket), 0).Packet;

        Assert.ThrowsExactly<ArgumentNullException>(() => QuicRetryIntegrity.ComputeTag([], null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicRetryIntegrity.ComputeTag(new byte[21], retry));
    }

    private static QuicLongHeaderPacket ClientInitial()
    {
        var crypto = Hex(ClientInitialCryptoFrame);
        var payload = crypto.Concat(new byte[1162 - crypto.Length]).ToArray();
        return new QuicLongHeaderPacket(QuicPacketType.Initial, QuicPacketCodec.Version1, Hex(ClientDestinationConnectionId), ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 4, 2, payload);
    }

    private static QuicLongHeaderPacket ServerInitial() =>
        new(QuicPacketType.Initial, QuicPacketCodec.Version1, ReadOnlyMemory<byte>.Empty, ServerConnectionId, ReadOnlyMemory<byte>.Empty, 2, 1, Hex(ServerInitialFrames));

    private static QuicShortHeaderPacket ChaChaShortHeaderPacket() =>
        new(ReadOnlyMemory<byte>.Empty, 3, 0xbff4, Hex("01"));

    private static QuicShortHeaderPacket Ping(uint packetNumber) =>
        new(ReadOnlyMemory<byte>.Empty, 4, packetNumber, Hex("01"));
}
