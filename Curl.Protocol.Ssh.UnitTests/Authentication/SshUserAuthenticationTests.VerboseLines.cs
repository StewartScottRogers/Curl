using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="SshUserAuthentication" /> reports for each method,
/// as curl 8.21.0 wrote them against OpenSSH 10.2 on 2026-09-29 and as
/// <c>lib/vssh/libssh2.c</c> words those the reference server could not reach (BL-578,
/// ADR-0262).
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    private const string Offered = "* SSH: host offers authentication via: publickey,password";

    private const string TryingKey = "* SSH: trying private key file '/keys/id'";

    private const string TryingPublicKey = "* SSH: trying public key file '/keys/id.pub'";

    [TestMethod]
    public async Task AuthenticateAsync_RightPassword_ReportsTheListAndThePasswordLine()
    {
        (Peer peer, TranscriptTransferEvents events) = ScriptRecordingLines(Failure("publickey,password"), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertLines(events, Offered, "* SSH: initialized password authentication");
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoneSucceeds_ReportsTheUserAcceptedWithoutAuthentication()
    {
        (Peer peer, TranscriptTransferEvents events) = ScriptRecordingLines(Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertLines(events, "* SSH: user accepted with no authentication");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyboardInteractiveAfterPublickeyIsListed_ReportsTheAgentLinesThenTheKeyboardInteractiveLine()
    {
        (Peer peer, TranscriptTransferEvents events) = ScriptRecordingLines(Failure("publickey,keyboard-interactive"), InfoRequest("Password: "), Success);

        await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None);

        AssertLines(
            events,
            "* SSH: host offers authentication via: publickey,keyboard-interactive",
            "* SSH: trying publickey authentication via agent",
            "* SSH: failure connecting to agent",
            "* SSH: initialized keyboard interactive authentication");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyboardInteractiveWithoutPublickey_ReportsNoAgentLines()
    {
        (Peer peer, TranscriptTransferEvents events) = ScriptRecordingLines(Failure("keyboard-interactive"), InfoRequest("Password: "), Failure("keyboard-interactive"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(Tester, CancellationToken.None));

        AssertLines(events, "* SSH: host offers authentication via: keyboard-interactive");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyAccepted_ReportsTheKeyFilesAndPublickeyAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1, TestUserKeys.RsaPublicKeyFile), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        AssertLines(peer.Events, Offered, TryingPublicKey, TryingKey, "* SSH: authenticated via publickey");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyAcceptedWithoutASignature_ReportsPublickey()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey,password"), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        AssertLines(peer.Events, Offered, TryingKey, "* SSH: authenticated via publickey");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyNotAuthorized_ReportsTheCombinationInvalidAndTheAgentAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey,password"), Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertLines(
            peer.Events,
            Offered,
            TryingKey,
            "* SSH: publickey authentication denied: Username/PublicKey combination invalid",
            "* SSH: trying publickey authentication via agent",
            "* SSH: failure connecting to agent");
    }

    [TestMethod]
    public async Task AuthenticateAsync_SignatureRefused_ReportsTheInvalidSignatureAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        Diagnostics.ActLines(peer.Events.Transcript);
        Diagnostics.Assert("verbose line 2", "* SSH: publickey authentication denied: Invalid signature for supplied public key, or bad username/public key combination", peer.Events.Transcript[2]);
        Assert.AreEqual("* SSH: publickey authentication denied: Invalid signature for supplied public key, or bad username/public key combination", peer.Events.Transcript[2]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PrivateKeyMissingBesidePubkey_ReportsTheCallbackErrorAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(null, TestUserKeys.RsaPublicKeyFile), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertLines(
            peer.Events,
            Offered,
            TryingPublicKey,
            TryingKey,
            "* SSH: publickey authentication denied: Callback returned error",
            "* SSH: trying publickey authentication via agent",
            "* SSH: failure connecting to agent");
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoKeyReadable_ReportsReasonUnknownAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(null), Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        Diagnostics.ActLines(peer.Events.Transcript);
        Diagnostics.Assert("verbose line 2", "* SSH: publickey authentication denied: Reason unknown (-1)", peer.Events.Transcript[2]);
        Assert.AreEqual("* SSH: publickey authentication denied: Reason unknown (-1)", peer.Events.Transcript[2]);
    }

    private void AssertLines(TranscriptTransferEvents events, params string[] expected)
    {
        Diagnostics.ActLines(events.Transcript);
        Diagnostics.Diff("verbose lines", string.Join("\n", expected), string.Join("\n", events.Transcript));
        Assert.AreEqual(string.Join("\n", expected), string.Join("\n", events.Transcript));
    }

    private (Peer Peer, TranscriptTransferEvents Events) ScriptRecordingLines(params byte[][] payloads)
    {
        Diagnostics.ArrangeMessages("server messages", payloads);
        ScriptedConnection connection = new(Frame(payloads));
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), new TestEphemeralKeys());
        TranscriptTransferEvents events = new();
        return (new Peer(new SshUserAuthentication(transport, Encoding.UTF8, null, events), transport, connection), events);
    }
}
