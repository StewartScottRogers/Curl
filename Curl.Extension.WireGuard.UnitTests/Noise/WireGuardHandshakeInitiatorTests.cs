using Curl.Cryptography;
using Curl.Extension.WireGuard.Configuration;
using Curl.Extension.WireGuard.Messages;
using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

[TestClass]
public sealed class WireGuardHandshakeInitiatorTests
{
    private static readonly byte[] InitiatorPrivateKey = WireGuardTestResponder.NewKey(1);

    private static readonly byte[] ResponderPrivateKey = WireGuardTestResponder.NewKey(2);

    private static readonly byte[] ResponderEphemeralKey = WireGuardTestResponder.NewKey(3);

    [TestMethod]
    public void CreateInitiation_WithoutCookie_TheResponderAcceptsItAndMac2IsZero()
    {
        var (initiator, responder, _) = CreatePair();

        var initiation = initiator.CreateInitiation();

        Assert.HasCount(HandshakeInitiationMessage.Size, initiation);
        Assert.IsTrue(responder.TryConsumeInitiation(initiation));
        Assert.IsFalse(initiation.AsSpan(132).ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TryConsumeResponse_FromTheResponder_BothSidesDeriveTheSameTransportKeys(bool withPresharedKey)
    {
        var presharedKey = withPresharedKey ? WireGuardTestResponder.NewKey(9) : null;
        var (initiator, responder, _) = CreatePair(presharedKey);
        Assert.IsTrue(responder.TryConsumeInitiation(initiator.CreateInitiation()));

        var completed = initiator.TryConsumeResponse(responder.CreateResponse(ResponderEphemeralKey), out var session);

        Assert.IsTrue(completed);
        using (session)
        {
            Assert.AreEqual(WireGuardTestResponder.ResponderIndex, session!.RemoteIndex);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, responder.OpenTransport(session.Seal([1, 2, 3])));
            Assert.IsTrue(session.TryOpen(responder.SealTransport(session.LocalIndex, 0, new byte[16]), out var packet));
            CollectionAssert.AreEqual(new byte[16], packet);
        }
    }

    [TestMethod]
    public void TryConsumeResponse_PresharedKeysDiffer_IsFalse()
    {
        var (initiator, _, _) = CreatePair();
        var responder = new WireGuardTestResponder(ResponderPrivateKey, PublicKey(InitiatorPrivateKey), WireGuardTestResponder.NewKey(9));
        Assert.IsTrue(responder.TryConsumeInitiation(initiator.CreateInitiation()));

        Assert.IsFalse(initiator.TryConsumeResponse(responder.CreateResponse(ResponderEphemeralKey), out var session));
        Assert.IsNull(session);
    }

    [TestMethod]
    public void TryConsumeResponse_BadMac1_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        responder.TryConsumeInitiation(initiator.CreateInitiation());
        var response = responder.CreateResponse(ResponderEphemeralKey);
        response[60] ^= 1;

        Assert.IsFalse(initiator.TryConsumeResponse(response, out _));
    }

    [TestMethod]
    public void TryConsumeResponse_ResponderHoldsAnotherStaticKey_IsFalse()
    {
        var (_, responder, clock) = CreatePair();
        var impostor = new WireGuardTestResponder(WireGuardTestResponder.NewKey(4), PublicKey(InitiatorPrivateKey), new byte[32]);
        var initiator = new WireGuardHandshakeInitiator(InitiatorPrivateKey, responder.StaticPublicKey, [], clock, new SequenceRandomNumberGenerator(7));
        var initiation = initiator.CreateInitiation();

        Assert.IsFalse(impostor.TryConsumeInitiation(initiation));
        Assert.IsFalse(initiator.TryConsumeResponse(impostor.CreateResponse(ResponderEphemeralKey), out _));
    }

    [TestMethod]
    public void TryConsumeInitiation_ReplayedTimestamp_TheResponderRejectsItUntilTheClockMoves()
    {
        var (initiator, responder, clock) = CreatePair();
        var initiation = initiator.CreateInitiation();
        Assert.IsTrue(responder.TryConsumeInitiation(initiation));

        Assert.IsFalse(responder.TryConsumeInitiation(initiation));
        Assert.IsFalse(responder.TryConsumeInitiation(initiator.CreateInitiation()));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsTrue(responder.TryConsumeInitiation(initiator.CreateInitiation()));
    }

    [TestMethod]
    public void TryConsumeResponse_ReplayedResponse_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        responder.TryConsumeInitiation(initiator.CreateInitiation());
        var response = responder.CreateResponse(ResponderEphemeralKey);
        Assert.IsTrue(initiator.TryConsumeResponse(response, out var session));
        session.Dispose();

        Assert.IsFalse(initiator.TryConsumeResponse(response, out _));
    }

    [TestMethod]
    public void TryConsumeResponse_BeforeAnyInitiationOrNotAResponse_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        var other = CreatePair().Initiator;
        responder.TryConsumeInitiation(other.CreateInitiation());
        var response = responder.CreateResponse(ResponderEphemeralKey);

        Assert.IsFalse(initiator.TryConsumeResponse(response, out _));
        var initiation = initiator.CreateInitiation();
        Assert.IsFalse(initiator.TryConsumeResponse(initiation, out _));
    }

    [TestMethod]
    public void TryConsumeResponse_ForAnotherIndex_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        responder.TryConsumeInitiation(initiator.CreateInitiation());
        var response = responder.CreateResponse(ResponderEphemeralKey);
        initiator.CreateInitiation();

        Assert.IsFalse(initiator.TryConsumeResponse(response, out _));
    }

    [TestMethod]
    public void TryConsumeResponse_LowOrderEphemeralKey_IsFalse()
    {
        var (initiator, _, _) = CreatePair();
        HandshakeInitiationMessage.TryDecode(initiator.CreateInitiation(), out var initiation);
        var response = new HandshakeResponseMessage(5, initiation!.SenderIndex, new byte[32], new byte[16], new byte[16], new byte[16]).Encode();
        var mac1Key = new byte[32];
        WireGuardMessageAuthentication.DeriveMac1Key(PublicKey(InitiatorPrivateKey), mac1Key);
        WireGuardMessageAuthentication.WriteMac1(response, mac1Key);

        Assert.IsFalse(initiator.TryConsumeResponse(response, out _));
    }

    [TestMethod]
    public void CreateInitiation_AfterACookieReply_CarriesMac2UntilTheCookieIsOld()
    {
        var (initiator, responder, clock) = CreatePair();
        byte[] cookie = [.. Enumerable.Range(40, 16).Select(value => (byte)value)];
        var reply = responder.CreateCookieReply(initiator.CreateInitiation(), cookie, new byte[24]);

        Assert.IsTrue(initiator.TryConsumeCookieReply(reply));
        Assert.IsTrue(WireGuardMessageAuthentication.HasValidMac2(initiator.CreateInitiation(), cookie));
        clock.Advance(WireGuardHandshakeInitiator.CookieLifetime);
        Assert.IsFalse(initiator.CreateInitiation().AsSpan(132).ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void TryConsumeCookieReply_NotForTheWaitingInitiation_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        var firstInitiation = initiator.CreateInitiation();
        var reply = responder.CreateCookieReply(firstInitiation, new byte[16], new byte[24]);

        Assert.IsFalse(initiator.TryConsumeCookieReply(firstInitiation));
        initiator.CreateInitiation();
        Assert.IsFalse(initiator.TryConsumeCookieReply(reply));
        Assert.IsFalse(CreatePair().Initiator.TryConsumeCookieReply(reply));
    }

    [TestMethod]
    public void TryConsumeCookieReply_BadTag_IsFalse()
    {
        var (initiator, responder, _) = CreatePair();
        var reply = responder.CreateCookieReply(initiator.CreateInitiation(), new byte[16], new byte[24]);
        reply[^1] ^= 1;

        Assert.IsFalse(initiator.TryConsumeCookieReply(reply));
    }

    [TestMethod]
    public async Task ConnectAsync_CookieReplyThenResponse_ResendsWithMac2AndGivesTheSession()
    {
        var (initiator, responder, _) = CreatePair();
        byte[] cookie = [.. Enumerable.Repeat((byte)7, 16)];
        var channel = new ScriptedDatagramChannel(
            sent => [1, 2, 3],
            sent => responder.CreateCookieReply(sent[^1], cookie, new byte[24]),
            sent =>
            {
                Assert.IsTrue(responder.TryConsumeInitiation(sent[^1]));
                return responder.CreateResponse(ResponderEphemeralKey);
            });

        using var session = await initiator.ConnectAsync(channel, CancellationToken.None);

        Assert.HasCount(2, channel.Sent);
        Assert.IsTrue(WireGuardMessageAuthentication.HasValidMac2(channel.Sent[1], cookie));
        Assert.AreEqual(WireGuardTestResponder.ResponderIndex, session.RemoteIndex);
    }

    [TestMethod]
    public async Task ConnectAsync_NullChannel_Throws()
    {
        var (initiator, _, _) = CreatePair();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => initiator.ConnectAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void Constructor_LowOrderPeerKey_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new WireGuardHandshakeInitiator(InitiatorPrivateKey, new byte[32], [], new ManualTimeProvider(), new SequenceRandomNumberGenerator(1)));

        Assert.AreEqual("peerPublicKey", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_PresharedKeyNotThirtyTwoBytes_Throws()
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => new WireGuardHandshakeInitiator(InitiatorPrivateKey, PublicKey(ResponderPrivateKey), new byte[31], new ManualTimeProvider(), new SequenceRandomNumberGenerator(1)));

        Assert.AreEqual("presharedKey", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullClockOrRandom_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new WireGuardHandshakeInitiator(InitiatorPrivateKey, PublicKey(ResponderPrivateKey), [], null!, new SequenceRandomNumberGenerator(1)));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new WireGuardHandshakeInitiator(InitiatorPrivateKey, PublicKey(ResponderPrivateKey), [], new ManualTimeProvider(), null!));
    }

    [TestMethod]
    public void ForPeer_ConfigurationWithPresharedKey_CompletesTheHandshake()
    {
        var presharedKey = WireGuardTestResponder.NewKey(9);
        var responder = new WireGuardTestResponder(ResponderPrivateKey, PublicKey(InitiatorPrivateKey), presharedKey);
        var wireGuardInterface = new WireGuardInterface(InitiatorPrivateKey, [], [], null, null);
        var peer = new WireGuardPeer(responder.StaticPublicKey, presharedKey, null, [], null);
        var initiator = WireGuardHandshakeInitiator.ForPeer(wireGuardInterface, peer, new ManualTimeProvider(), new SequenceRandomNumberGenerator(3));
        Assert.IsTrue(responder.TryConsumeInitiation(initiator.CreateInitiation()));

        Assert.IsTrue(initiator.TryConsumeResponse(responder.CreateResponse(ResponderEphemeralKey), out var session));
        session.Dispose();
    }

    [TestMethod]
    public void ForPeer_NullInterfaceOrPeer_Throws()
    {
        var wireGuardInterface = new WireGuardInterface(InitiatorPrivateKey, [], [], null, null);
        var peer = new WireGuardPeer(PublicKey(ResponderPrivateKey), null, null, [], null);

        Assert.ThrowsExactly<ArgumentNullException>(() => WireGuardHandshakeInitiator.ForPeer(null!, peer, TimeProvider.System, new SequenceRandomNumberGenerator(1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => WireGuardHandshakeInitiator.ForPeer(wireGuardInterface, null!, TimeProvider.System, new SequenceRandomNumberGenerator(1)));
    }

    private static (WireGuardHandshakeInitiator Initiator, WireGuardTestResponder Responder, ManualTimeProvider Clock) CreatePair(byte[]? presharedKey = null)
    {
        var clock = new ManualTimeProvider();
        var responder = new WireGuardTestResponder(ResponderPrivateKey, PublicKey(InitiatorPrivateKey), presharedKey ?? new byte[32]);
        var initiator = new WireGuardHandshakeInitiator(InitiatorPrivateKey, responder.StaticPublicKey, presharedKey ?? [], clock, new SequenceRandomNumberGenerator(7));
        return (initiator, responder, clock);
    }

    private static byte[] PublicKey(byte[] privateKey)
    {
        var publicKey = new byte[32];
        X25519.ComputePublicKey(privateKey, publicKey);
        return publicKey;
    }

    private sealed class ScriptedDatagramChannel(params Func<List<byte[]>, byte[]>[] replies) : IWireGuardDatagramChannel
    {
        private int nextReply;

        public List<byte[]> Sent { get; } = [];

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            Sent.Add(datagram.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>>(replies[nextReply++](Sent));
    }
}
