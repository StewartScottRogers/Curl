using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <see cref="SshUserAuthentication" /> against an in-memory peer: the client's
/// messages byte for byte and the outcome of each case measured 2026-09-29 with curl
/// 8.21.0 (libssh2 1.11.1, WinCNG) against a loopback server built from this library
/// (BL-567, ADR-0215).
/// </summary>
[TestClass]
public sealed partial class SshUserAuthenticationTests
{
    private const string ServiceRequestFailedPrefix = "Failure establishing ssh session: ";

    private static readonly NetworkCredential Tester = new("tester", "secret");

    private static readonly byte[] Success = [SshAuthenticationMessageNumber.Success];

    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["none"]));

    private static byte[] ClientKexInit =>
        SshKexInit.ForClient(SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33)).ToPayload();

    /// <summary>Gets or sets the running test's context, which its diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RequestServiceAsync_ServerAccepts_SendsTheServiceRequest()
    {
        Peer peer = Script([SshMessageNumber.ServiceAccept, .. Name("ssh-userauth")]);

        await peer.Authentication.RequestServiceAsync(CancellationToken.None);

        AssertWritten(peer, [SshMessageNumber.ServiceRequest, .. Name("ssh-userauth")]);
    }

    [TestMethod]
    public async Task RequestServiceAsync_OtherMessagesBeforeTheAcceptance_SkipsThem()
    {
        Peer peer = Script(
            [SshMessageNumber.Ignore, 0, 0, 0, 0],
            Failure("password"),
            [SshMessageNumber.ServiceAccept, .. Name("ssh-userauth"), 0xFF]);

        await peer.Authentication.RequestServiceAsync(CancellationToken.None);

        AssertWritten(peer, [SshMessageNumber.ServiceRequest, .. Name("ssh-userauth")]);
    }

    [TestMethod]
    public async Task RequestServiceAsync_PeerCloses_FailsWithMinus43AsMeasured()
    {
        Peer peer = Script(Failure("password"));

        await AssertServiceRequestFailsAsync(peer, "-43, Failed to get response to ssh-userauth request");
    }

    [TestMethod]
    [DataRow(0xFFFFFFFFu, "-41", DisplayName = "the largest length")]
    [DataRow(0u, "-12", DisplayName = "zero length")]
    [DataRow(13u, "-43", DisplayName = "off the block size")]
    public async Task RequestServiceAsync_UnprotectedPacketLengthBroken_FailsWithLibssh2sCode(uint packetLength, string expectedCode)
    {
        SshServerScript script = new SshServerScript().RawPacket(packetLength, 4, [0, 0, 0, 0]);
        Peer peer = Connect(script.Bytes, Encoding.UTF8);

        await AssertServiceRequestFailsAsync(peer, $"{expectedCode}, Failed to get response to ssh-userauth request");
    }

    // Measured 2026-10-01 (BL-1081) on both reference builds by flipping bits of the
    // encrypted length of SERVICE_ACCEPT, whose packet_length is 28 under aes128-ctr, 32
    // under AES-GCM and 24 under chacha20-poly1305: the top bit gives -41 and zeroing it
    // -12. A length off the block size aborts libssh2 or hangs it until -m, so Curl keeps
    // -43 for it (ADR-0206).
    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256", 0, (byte)0x80, "-41", DisplayName = "aes128-ctr, over the maximum, as measured")]
    [DataRow("aes128-gcm@openssh.com", null, 0, (byte)0x80, "-41", DisplayName = "AES-GCM, over the maximum, as measured")]
    [DataRow("chacha20-poly1305@openssh.com", null, 0, (byte)0x80, "-41", DisplayName = "ChaCha20-Poly1305, over the maximum, as measured")]
    [DataRow("aes128-ctr", "hmac-sha2-256", 3, (byte)28, "-12", DisplayName = "aes128-ctr, zero length, as measured")]
    [DataRow("aes128-gcm@openssh.com", null, 3, (byte)32, "-12", DisplayName = "AES-GCM, zero length, as measured")]
    [DataRow("chacha20-poly1305@openssh.com", null, 3, (byte)24, "-12", DisplayName = "ChaCha20-Poly1305, zero length, as measured")]
    [DataRow("aes128-ctr", "hmac-sha2-256", 3, (byte)1, "-43", DisplayName = "aes128-ctr, off the block size")]
    public async Task RequestServiceAsync_AnswersLengthBroken_FailsWithLibssh2sCode(string cipher, string? mac, int lengthByte, byte flippedBits, string expectedCode)
    {
        SshNegotiatedAlgorithms algorithms = SshTestAlgorithms.With(cipher, mac);
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, [1, 2, 3], [4, 5, 6], [4, 5, 6]);
        byte[] serverBytes = new SshServerScript()
            .Protect(SshPacketProtections.ForServerToClient(algorithms, keys), resetSequenceNumber: true)
            .Packet([SshMessageNumber.ServiceAccept, .. Name("ssh-userauth")], sealedPacket => sealedPacket[lengthByte] ^= flippedBits)
            .Bytes;
        Peer peer = Connect(serverBytes, Encoding.UTF8);
        peer.Transport.PacketReader.ChangeProtection(SshPacketProtections.ForServerToClient(algorithms, keys));

        await AssertServiceRequestFailsAsync(peer, $"{expectedCode}, Failed to get response to ssh-userauth request");
    }

    [TestMethod]
    public async Task RequestServiceAsync_PeerDisconnects_FailsWithMinus13AsMeasured()
    {
        Peer peer = Script(Disconnect());

        await AssertServiceRequestFailsAsync(peer, "-13, Failed to get response to ssh-userauth request");
    }

    [TestMethod]
    public async Task RequestServiceAsync_AnswerFailsItsMac_FailsWithLibssh2sMinus4()
    {
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, [1, 2, 3], [4, 5, 6], [4, 5, 6]);
        byte[] serverBytes = new SshServerScript()
            .Protect(SshPacketProtections.ForServerToClient(ctr, keys), resetSequenceNumber: true)
            .Packet([SshMessageNumber.ServiceAccept, .. Name("ssh-userauth")], sealedPacket => sealedPacket[^1] ^= 0x01)
            .Bytes;
        Peer peer = Connect(serverBytes, Encoding.UTF8);
        peer.Transport.PacketReader.ChangeProtection(SshPacketProtections.ForServerToClient(ctr, keys));

        await AssertServiceRequestFailsAsync(peer, "-4, Failed to get response to ssh-userauth request");
    }

    [TestMethod]
    [DataRow("0600", "-14, Unexpected packet length", DisplayName = "shorter than five bytes, as measured")]
    [DataRow("06000000", "-14, Unexpected packet length", DisplayName = "four bytes")]
    [DataRow("060000000E7373682D636F6E6E656374696F6E", "-14, Invalid response received from server", DisplayName = "ssh-connection, as measured")]
    [DataRow("060000000C7373682D7573657261757458", "-14, Invalid response received from server", DisplayName = "twelve bytes, another name")]
    [DataRow("060000000C737368", "-14, Invalid response received from server", DisplayName = "name cut short")]
    public async Task RequestServiceAsync_MalformedOrWrongAcceptance_FailsWithMinus14(string acceptHex, string expected)
    {
        Peer peer = Script(Convert.FromHexString(acceptHex));

        await AssertServiceRequestFailsAsync(peer, expected);
    }

    [TestMethod]
    public async Task RequestServiceAsync_Cancelled_Throws()
    {
        Peer peer = Script([SshMessageNumber.ServiceAccept, .. Name("ssh-userauth")]);

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await peer.Authentication.RequestServiceAsync(new CancellationToken(canceled: true)));

        Diagnostics.Act("exception", cancelled.GetType().Name);
        Diagnostics.Assert("exception", nameof(OperationCanceledException), cancelled.GetType().Name);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RightPassword_SendsNoneThenPasswordAndSucceedsAsMeasured()
    {
        Peer peer = Script(Failure("publickey,password,keyboard-interactive"), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_WrongPasswordWithKeyboardInteractiveOffered_AnswersThePromptThenLoginDeniedAsMeasured()
    {
        string methods = "publickey,password,keyboard-interactive";
        Peer peer = Script(Failure(methods), Failure(methods), InfoRequest("Password: "), Failure(methods));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(new NetworkCredential("tester", "wrong"), CancellationToken.None));

        AssertLoginDenied(failure);
        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "wrong"), KeyboardInteractiveRequest("tester"), InfoResponse("wrong"));
    }

    [TestMethod]
    [DataRow("publickey,keyboard-interactive", DisplayName = "PasswordAuthentication no, as measured")]
    [DataRow("keyboard-interactive", DisplayName = "keyboard-interactive alone, as measured")]
    public async Task AuthenticateAsync_PasswordNotOffered_AuthenticatesWithKeyboardInteractiveAlone(string methods)
    {
        Peer peer = Script(Failure(methods), InfoRequest("Password: "), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), KeyboardInteractiveRequest("tester"), InfoResponse("secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_PasswordNotOfferedAndKeyboardInteractiveFails_LoginDeniedAsMeasured()
    {
        Peer peer = Script(Failure("publickey,keyboard-interactive"), InfoRequest("Password: "), Failure("publickey,keyboard-interactive"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertLoginDenied(failure);
    }

    [TestMethod]
    public async Task AuthenticateAsync_WrongPasswordAndOnlyPasswordOffered_AuthenticationFailureAsMeasured()
    {
        Peer peer = Script(Failure("password"), Failure("password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(new NetworkCredential("tester", "wrong"), CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "wrong"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoCredentials_SendsAnEmptyUserAndPasswordAsMeasured()
    {
        Peer peer = Script(Failure("password,keyboard-interactive"), Failure("password,keyboard-interactive"), InfoRequest("Password: "), Failure("password,keyboard-interactive"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(null, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertWritten(peer, NoneRequest(string.Empty), PasswordRequest(string.Empty, string.Empty), KeyboardInteractiveRequest(string.Empty), InfoResponse(string.Empty));
    }

    [TestMethod]
    public async Task AuthenticateAsync_BannersBeforeAnswers_SkipsThemAsMeasured()
    {
        Peer peer = Script(Banner("Welcome\r\n"), Failure("password,keyboard-interactive"), Banner("Password banner\r\n"), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_IgnoreAndUnknownMessagesBeforeAnAnswer_SkipsThemAsMeasured()
    {
        Peer peer = Script(Failure("password,keyboard-interactive"), [SshMessageNumber.Ignore, .. Name("ignored")], [99, 1, 2, 3], Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_PartialSuccessWithKeyboardInteractiveInTheFirstList_TriesItNextAsMeasured()
    {
        Peer peer = Script(Failure("password,keyboard-interactive"), Failure("keyboard-interactive", partial: true), InfoRequest("Password: "), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"), KeyboardInteractiveRequest("tester"), InfoResponse("secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_PartialSuccessWithKeyboardInteractiveOnlyInTheNewList_AuthenticationFailureAsMeasured()
    {
        Peer peer = Script(Failure("password"), Failure("keyboard-interactive", partial: true));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoneSucceeds_SendsNothingMoreAsMeasured()
    {
        Peer peer = Script(Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"));
    }

    [TestMethod]
    [DataRow("publickey", DisplayName = "publickey alone, as measured")]
    [DataRow("gssapi-with-mic,hostbased", DisplayName = "gssapi-with-mic and hostbased, as measured")]
    [DataRow("", DisplayName = "an empty list, as measured")]
    public async Task AuthenticateAsync_NeitherPasswordNorKeyboardInteractiveOffered_AuthenticationFailureWithoutAnAttempt(string methods)
    {
        Peer peer = Script(Failure(methods));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertWritten(peer, NoneRequest("tester"));
    }

    [TestMethod]
    [DataRow("", DisplayName = "the peer closes, as measured")]
    [DataRow("0100000002000000036279650000000000", DisplayName = "the peer disconnects, as measured")]
    [DataRow("330000", DisplayName = "a failure too short for its list, as measured")]
    public async Task AuthenticateAsync_AnswerToNoneUnreadable_ErrorInTheSshLayer(string answerHex)
    {
        Peer peer = answerHex.Length == 0 ? Script() : Script(Convert.FromHexString(answerHex));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.Ssh, "Error in the SSH layer", failure);
        Assert.AreEqual(CurlExitCode.Ssh, failure.ExitCode);
        Assert.AreEqual("Error in the SSH layer", failure.Message);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PasswordChangeRequest_FailsThePasswordAndGoesOnAsMeasured()
    {
        Peer peer = Script(
            Failure("password,keyboard-interactive"),
            [SshAuthenticationMessageNumber.PasswordChangeRequest, .. Name("expired"), .. Name(string.Empty)],
            InfoRequest("Password: "),
            Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), PasswordRequest("tester", "secret"), KeyboardInteractiveRequest("tester"), InfoResponse("secret"));
    }

    [TestMethod]
    [DataRow("password,keyboard-interactive", true, DisplayName = "closes, keyboard-interactive next, as measured")]
    [DataRow("password", false, DisplayName = "closes, nothing next, as measured")]
    public async Task AuthenticateAsync_PeerClosesAfterThePassword_FailsAsCurlDoes(string methods, bool keyboardInteractiveTried)
    {
        Peer peer = Script(Failure(methods));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.LoginDenied, keyboardInteractiveTried ? "Login denied" : "Authentication failure", failure);
        Assert.AreEqual(keyboardInteractiveTried ? "Login denied" : "Authentication failure", failure.Message);
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
    }

    [TestMethod]
    [DataRow(0u, DisplayName = "zero length")]
    [DataRow(uint.MaxValue, DisplayName = "over the maximum")]
    public async Task AuthenticateAsync_AnswerToThePasswordHasABrokenLength_FailsAsAClose(uint packetLength)
    {
        byte[] serverBytes = [.. Frame(Failure("password")), .. new SshServerScript().RawPacket(packetLength, 4, [0, 0, 0, 0]).Bytes];
        Peer peer = Connect(serverBytes, Encoding.UTF8);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.LoginDenied, "Authentication failure", failure);
        Assert.AreEqual("Authentication failure", failure.Message);
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PeerDisconnectsAfterThePassword_LoginDeniedAsMeasured()
    {
        Peer peer = Script(Failure("password,keyboard-interactive"), Disconnect());

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertLoginDenied(failure);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PasswordAnswerFailsItsMac_FailsThePassword()
    {
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        SshKeyDerivation keys = new(HashAlgorithmName.SHA256, [1, 2, 3], [4, 5, 6], [4, 5, 6]);
        byte[] serverBytes = new SshServerScript()
            .Protect(SshPacketProtections.ForServerToClient(ctr, keys), resetSequenceNumber: true)
            .Packet(Failure("password"))
            .Packet(Success, sealedPacket => sealedPacket[^1] ^= 0x01)
            .Bytes;
        Peer peer = Connect(serverBytes, Encoding.UTF8);
        peer.Transport.PacketReader.ChangeProtection(SshPacketProtections.ForServerToClient(ctr, keys));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertAuthenticationFailure(failure);
    }

    [TestMethod]
    [DataRow(0, DisplayName = "no prompt, as measured")]
    [DataRow(2, DisplayName = "two prompts, as measured")]
    [DataRow(100, DisplayName = "one hundred prompts, as measured")]
    public async Task AuthenticateAsync_RoundWithOtherThanOnePrompt_AnswersEveryPromptWithAnEmptyString(int promptCount)
    {
        string[] prompts = [.. Enumerable.Range(0, promptCount).Select(index => $"p{index}")];
        Peer peer = Script(Failure("keyboard-interactive"), InfoRequest(prompts), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), KeyboardInteractiveRequest("tester"), InfoResponse([.. prompts.Select(_ => string.Empty)]));
    }

    [TestMethod]
    public async Task AuthenticateAsync_RoundWith101Prompts_LoginDeniedWithoutAnAnswerAsMeasured()
    {
        Peer peer = Script(Failure("keyboard-interactive"), InfoRequest([.. Enumerable.Range(0, 101).Select(index => $"p{index}")]));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertWritten(peer, NoneRequest("tester"), KeyboardInteractiveRequest("tester"));
    }

    [TestMethod]
    public async Task AuthenticateAsync_TwoRounds_AnswersThePasswordToEachAsMeasured()
    {
        Peer peer = Script(Failure("keyboard-interactive"), InfoRequest("First: "), InfoRequest("Second: "), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertWritten(peer, NoneRequest("tester"), KeyboardInteractiveRequest("tester"), InfoResponse("secret"), InfoResponse("secret"));
    }

    [TestMethod]
    [DataRow("3C0000000401", DisplayName = "cut short in its name, as measured")]
    [DataRow("3C000000000000000000000000000000010000000170", DisplayName = "cut short in its echo flag")]
    public async Task AuthenticateAsync_MalformedInfoRequest_LoginDeniedWithoutAnAnswer(string infoRequestHex)
    {
        Peer peer = Script(Failure("keyboard-interactive"), Convert.FromHexString(infoRequestHex));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertWritten(peer, NoneRequest("tester"), KeyboardInteractiveRequest("tester"));
    }

    [TestMethod]
    [DataRow("latin1", "74E973746572", "73E963726574", DisplayName = "the Windows reference's ANSI code page, as measured")]
    [DataRow("utf-8", "74C3A973746572", "73C3A963726574", DisplayName = "UTF-8, as curl on Linux and macOS sends it")]
    public async Task AuthenticateAsync_NonAsciiCredentials_EncodesThemWithTheGivenEncoding(string encodingName, string userHex, string passwordHex)
    {
        Peer peer = Connect(Frame(Failure("password"), Success), Encoding.GetEncoding(encodingName));

        await peer.Authentication.AuthenticateAsync(new NetworkCredential("téster", "sécret"), CancellationToken.None);

        byte[] expected = Join(
            [SshAuthenticationMessageNumber.Request],
            String(Convert.FromHexString(userHex)),
            Name("ssh-connection"),
            Name("password"),
            [0],
            String(Convert.FromHexString(passwordHex)));
        Diagnostics.Diff("password request", expected, WrittenPayloads(peer)[1]);
        CollectionAssert.AreEqual(expected, ClientPayloads(peer.Connection.Written)[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Cancelled_Throws()
    {
        Peer peer = Script(Success);

        OperationCanceledException cancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, new CancellationToken(canceled: true)));

        Diagnostics.Act("exception", cancelled.GetType().Name);
        Diagnostics.Assert("exception", nameof(OperationCanceledException), cancelled.GetType().Name);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ServerStartsAKeyReExchange_AnswersItAndCarriesOn()
    {
        TestHostKey hostKey = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        SshKexInit firstKexInit = ServerKexInit("ecdh-sha2-nistp256", hostKey.Algorithm);
        SshKexInit secondKexInit = ServerKexInit("diffie-hellman-group14-sha256", hostKey.Algorithm);
        TestEphemeralKeys keys = new();
        TestKeyExchangeServer first = TestKeyExchangeServer.Answer("ecdh-sha2-nistp256", hostKey, keys, ClientKexInit, firstKexInit.ToPayload());
        TestKeyExchangeServer second = TestKeyExchangeServer.Answer("diffie-hellman-group14-sha256", hostKey, keys, ClientKexInit, secondKexInit.ToPayload());
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(firstKexInit);
        first.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(ctr, first.Keys(first.ExchangeHash)), resetSequenceNumber: false)
            .KexInit(secondKexInit);
        second.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys)
            .Protect(SshPacketProtections.ForServerToClient(ctr, second.Keys(first.ExchangeHash)), resetSequenceNumber: false)
            .Packet(Success);
        Diagnostics.Arrange("key exchanges", "ecdh-sha2-nistp256, then the server's re-exchange with diffie-hellman-group14-sha256");
        Diagnostics.Bytes("server bytes", script.Bytes);
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        using (Diagnostics.Phase("first key exchange"))
        {
            await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        }

        SshUserAuthentication authentication = new(transport, Encoding.UTF8);

        using (Diagnostics.Phase("authentication with re-exchange"))
        {
            await authentication.AuthenticateAsync(Tester, CancellationToken.None);
        }

        List<byte[]> written = await SshClientTranscript.PayloadsAsync(
            connection.Written,
            false,
            SshPacketProtections.ForClientToServer(ctr, first.Keys(first.ExchangeHash)),
            SshPacketProtections.ForClientToServer(ctr, second.Keys(first.ExchangeHash)));
        Diagnostics.ActMessages("client messages", written);
        Diagnostics.Assert("client message count", 7, written.Count);
        Diagnostics.Diff("client message 3", NoneRequest("tester"), written.Count > 3 ? written[3] : []);
        Diagnostics.Diff("client message 4", ClientKexInit, written.Count > 4 ? written[4] : []);
        Diagnostics.Diff("client message 5", second.ClientPayloads[0], written.Count > 5 ? written[5] : []);
        Assert.HasCount(7, written);
        CollectionAssert.AreEqual(NoneRequest("tester"), written[3]);
        CollectionAssert.AreEqual(ClientKexInit, written[4], "the client answers the server's KEXINIT with its own");
        CollectionAssert.AreEqual(second.ClientPayloads[0], written[5]);
    }

    private static SshKexInit ServerKexInit(string method, string hostKey) =>
        SshServerScript.OpenSshKexInit(kexInit => kexInit with
        {
            KeyExchange = [method],
            ServerHostKey = [hostKey],
            CipherClientToServer = ["aes128-ctr"],
            CipherServerToClient = ["aes128-ctr"],
            MacClientToServer = ["hmac-sha2-256"],
            MacServerToClient = ["hmac-sha2-256"],
        });

    private static byte[] NoneRequest(string user) => Join([SshAuthenticationMessageNumber.Request], Utf8(user), Name("ssh-connection"), Name("none"));

    private static byte[] PasswordRequest(string user, string password) =>
        Join([SshAuthenticationMessageNumber.Request], Utf8(user), Name("ssh-connection"), Name("password"), [0], Utf8(password));

    private static byte[] KeyboardInteractiveRequest(string user) =>
        Join([SshAuthenticationMessageNumber.Request], Utf8(user), Name("ssh-connection"), Name("keyboard-interactive"), Name(string.Empty), Name(string.Empty));

    private static byte[] InfoResponse(params string[] answers) =>
        Join([[SshAuthenticationMessageNumber.InfoResponse], UInt32((uint)answers.Length), .. answers.Select(Utf8)]);

    private static byte[] InfoRequest(params string[] prompts) =>
        Join([[SshAuthenticationMessageNumber.InfoRequest], Name("name"), Name("instruction"), Name(string.Empty), UInt32((uint)prompts.Length), .. prompts.Select(prompt => Join(Name(prompt), [0]))]);

    private static byte[] Failure(string methods, bool partial = false) =>
        [SshAuthenticationMessageNumber.Failure, .. Name(methods), partial ? (byte)1 : (byte)0];

    private static byte[] Banner(string text) => [SshAuthenticationMessageNumber.Banner, .. Name(text), .. Name(string.Empty)];

    private static byte[] Disconnect() => [SshMessageNumber.Disconnect, .. UInt32(2), .. Name("bye"), .. Name(string.Empty)];

    private static byte[] Utf8(string text) => String(Encoding.UTF8.GetBytes(text));

    private static byte[] Frame(params byte[][] payloads)
    {
        SshServerScript script = new();
        foreach (byte[] payload in payloads)
        {
            script.Packet(payload);
        }

        return script.Bytes;
    }

    private Peer Script(params byte[][] payloads)
    {
        Diagnostics.ArrangeMessages("server messages", payloads);
        return Connect(Frame(payloads), Encoding.UTF8);
    }

    private Peer Connect(byte[] serverBytes, Encoding credentialEncoding)
    {
        Diagnostics.Arrange("credential encoding", credentialEncoding.WebName);
        Diagnostics.Bytes("server bytes", serverBytes);
        ScriptedConnection connection = new(serverBytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), new TestEphemeralKeys());
        return new Peer(new SshUserAuthentication(transport, credentialEncoding), transport, connection);
    }

    // The unencrypted packets the client wrote, from the first byte: no identification line.
    private static List<byte[]> ClientPayloads(byte[] written)
    {
        List<byte[]> payloads = [];
        for (int position = 0; position < written.Length;)
        {
            int packetLength = (int)BinaryPrimitives.ReadUInt32BigEndian(written.AsSpan(position));
            payloads.Add(written.AsSpan(position + 5, packetLength - 1 - written[position + 4]).ToArray());
            position += 4 + packetLength;
        }

        return payloads;
    }

    // The test's ARRANGE, ACT, ASSERT and DIFF lines; shared by every partial of this class.
    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // The client's unencrypted messages, written as an ACT line.
    private List<byte[]> WrittenPayloads(Peer peer)
    {
        List<byte[]> written = ClientPayloads(peer.Connection.Written);
        Diagnostics.ActMessages("client messages", written);
        return written;
    }

    // Catches the failure the act throws and writes it as ACT lines.
    private async Task<SshTransferException> CatchFailureAsync(Func<Task> act)
    {
        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(act);
        Diagnostics.ActFailure(failure);
        return failure;
    }

    private void AssertWritten(Peer peer, params byte[][] expected)
    {
        List<byte[]> written = WrittenPayloads(peer);
        Diagnostics.DiffMessages(expected, written);
        Assert.HasCount(expected.Length, written);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], written[index], $"client message {index}");
        }
    }

    private async Task AssertServiceRequestFailsAsync(Peer peer, string expected)
    {
        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.RequestServiceAsync(CancellationToken.None));

        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.FailedInit, ServiceRequestFailedPrefix + expected, failure);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(ServiceRequestFailedPrefix + expected, failure.Message);
    }

    private void AssertLoginDenied(SshTransferException failure)
    {
        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.LoginDenied, "Login denied", failure);
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Login denied", failure.Message);
    }

    private void AssertAuthenticationFailure(SshTransferException failure)
    {
        Diagnostics.ActFailure(failure);
        Diagnostics.AssertFailure(CurlExitCode.LoginDenied, "Authentication failure", failure);
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Authentication failure", failure.Message);
    }

    private sealed record Peer(SshUserAuthentication Authentication, SshTransport Transport, ScriptedConnection Connection);
}
