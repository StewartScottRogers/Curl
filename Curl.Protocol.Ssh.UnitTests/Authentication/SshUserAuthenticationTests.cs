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
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <see cref="SshUserAuthentication" /> against an in-memory peer: the client's
/// messages byte for byte and the outcome of each case measured 2026-09-29 with curl
/// 8.21.0 (libssh2 1.11.1, WinCNG) against a loopback server built from this library
/// (BL-567, ADR-0214).
/// </summary>
[TestClass]
public sealed class SshUserAuthenticationTests
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
    public async Task RequestServiceAsync_PeerBreaksTheFraming_FailsWithMinus43()
    {
        SshServerScript script = new SshServerScript().RawPacket(uint.MaxValue, 4, [0, 0, 0, 0]);
        Peer peer = Connect(new ScriptedConnection(script.Bytes), Encoding.UTF8);

        await AssertServiceRequestFailsAsync(peer, "-43, Failed to get response to ssh-userauth request");
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
        Peer peer = Connect(new ScriptedConnection(serverBytes), Encoding.UTF8);
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

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await peer.Authentication.RequestServiceAsync(new CancellationToken(canceled: true)));
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

        Assert.AreEqual(keyboardInteractiveTried ? "Login denied" : "Authentication failure", failure.Message);
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
        Peer peer = Connect(new ScriptedConnection(serverBytes), Encoding.UTF8);
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
        Peer peer = Connect(new ScriptedConnection(Frame(Failure("password"), Success)), Encoding.GetEncoding(encodingName));

        await peer.Authentication.AuthenticateAsync(new NetworkCredential("téster", "sécret"), CancellationToken.None);

        byte[] expected = Join(
            [SshAuthenticationMessageNumber.Request],
            String(Convert.FromHexString(userHex)),
            Name("ssh-connection"),
            Name("password"),
            [0],
            String(Convert.FromHexString(passwordHex)));
        CollectionAssert.AreEqual(expected, ClientPayloads(peer.Connection.Written)[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Cancelled_Throws()
    {
        Peer peer = Script(Success);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await peer.Authentication.AuthenticateAsync(Tester, new CancellationToken(canceled: true)));
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
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), keys);
        await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        SshUserAuthentication authentication = new(transport, Encoding.UTF8);

        await authentication.AuthenticateAsync(Tester, CancellationToken.None);

        List<byte[]> written = await SshClientTranscript.PayloadsAsync(
            connection.Written,
            false,
            SshPacketProtections.ForClientToServer(ctr, first.Keys(first.ExchangeHash)),
            SshPacketProtections.ForClientToServer(ctr, second.Keys(first.ExchangeHash)));
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

    private static Peer Script(params byte[][] payloads) => Connect(new ScriptedConnection(Frame(payloads)), Encoding.UTF8);

    private static Peer Connect(ScriptedConnection connection, Encoding credentialEncoding)
    {
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

    private static void AssertWritten(Peer peer, params byte[][] expected)
    {
        List<byte[]> written = ClientPayloads(peer.Connection.Written);
        Assert.HasCount(expected.Length, written);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], written[index], $"client message {index}");
        }
    }

    private static async Task AssertServiceRequestFailsAsync(Peer peer, string expected)
    {
        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.RequestServiceAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(ServiceRequestFailedPrefix + expected, failure.Message);
    }

    private static void AssertLoginDenied(SshTransferException failure)
    {
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Login denied", failure.Message);
    }

    private static void AssertAuthenticationFailure(SshTransferException failure)
    {
        Assert.AreEqual(CurlExitCode.LoginDenied, failure.ExitCode);
        Assert.AreEqual("Authentication failure", failure.Message);
    }

    private sealed record Peer(SshUserAuthentication Authentication, SshTransport Transport, ScriptedConnection Connection);
}
