using System.Text;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the agent step (BL-902, ADR-0270) against a fake agent and the in-memory peer:
/// after <c>password</c> fails and before <c>keyboard-interactive</c>, curl lists the
/// agent's identities and tries each with the same question and signed request as a key
/// file's, the agent signing. The outcomes were measured 2026-09-30 with curl 8.21.0
/// (libssh2 1.11.1, WinCNG) and OpenSSH's <c>ssh-agent</c> against a loopback server built
/// from this library; the rest follow libssh2 1.11.1's <c>agent.c</c> and <c>userauth.c</c>.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    private const string AllThreeMethods = "publickey,password,keyboard-interactive";

    private const string TryingAgent = "* SSH: trying publickey authentication via agent";

    private const string NoIdentityMatched = "* SSH: no agent identity would match";

    private static readonly byte[] RequestIdentities = [SshAgentMessageNumber.RequestIdentities];

    [TestMethod]
    public async Task AuthenticateAsync_AgentHoldsTheAuthorizedKey_AsksThenSignsThroughTheAgentAsMeasured()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk("ssh-rsa", RsaBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "password", "publickey", "publickey");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: false), written[2]);
        AssertSigned(peer, written[3], "ssh-rsa", RsaBlob);
        Assert.HasCount(2, agent.Requests);
        CollectionAssert.AreEqual(RequestIdentities, agent.Requests[0]);
        CollectionAssert.AreEqual(SignRequest(peer, RsaBlob, "ssh-rsa", flags: 0), agent.Requests[1]);
        AssertAgentLines(peer, TryingAgent, "* SSH: agent authenticated user 'tester' with key 'k1-comment'");
    }

    [TestMethod]
    public async Task AuthenticateAsync_SecondIdentityAuthorized_AsksForEachInTheAgentsOrderAsMeasured()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment").Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk("ssh-ed25519", Ed25519Blob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "password", "publickey", "publickey", "publickey");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: false), written[2]);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-ed25519", Ed25519Blob, signed: false), written[3]);
        AssertSigned(peer, written[4], "ssh-ed25519", Ed25519Blob);
        Assert.AreEqual(1, agent.Connections);
        Assert.HasCount(2, agent.Requests, "one list, and a signature for the key the server accepted only");
        AssertAgentLines(peer, TryingAgent, "* SSH: agent authenticated user 'tester' with key 'k2-comment'");
    }

    [TestMethod]
    public async Task AuthenticateAsync_EveryIdentityRefused_GoesOnToKeyboardInteractiveAsMeasured()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment").Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "publickey", "publickey", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    public async Task AuthenticateAsync_AgentHoldsNoIdentity_ReportsNoIdentityMatchedAsMeasured()
    {
        InMemorySshAgent agent = new();
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoAgentAnswers_ReportsTheConnectFailureAsMeasured()
    {
        KeyedPeer peer = await ConnectWithAgentAsync(new UnreachableSshAgent(), null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, "* SSH: failure connecting to agent");
    }

    [TestMethod]
    [DataRow("05", DisplayName = "SSH_AGENT_FAILURE")]
    [DataRow("", DisplayName = "an empty answer")]
    public async Task AuthenticateAsync_IdentitiesCannotBeRead_ReportsTheRequestFailureAsMeasured(string answerHex)
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Convert.FromHexString(answerHex)));
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, "* SSH: failure requesting identities to agent");
        Assert.IsTrue(agent.WasDisposed, "the agent's connection is closed after the step");
    }

    [TestMethod]
    [DataRow("rsa-sha2-512,rsa-sha2-256,ssh-rsa,ssh-ed25519", "rsa-sha2-512", 4u, DisplayName = "rsa-sha2-512, flag 4, as measured")]
    [DataRow("rsa-sha2-256", "rsa-sha2-256", 2u, DisplayName = "rsa-sha2-256, flag 2, as measured")]
    [DataRow("ssh-rsa", "ssh-rsa", 0u, DisplayName = "ssh-rsa, no flag")]
    public async Task AuthenticateAsync_RsaIdentityWithServerSigAlgs_AsksTheAgentForThatAlgorithmsFlagAsMeasured(string serverSignatureAlgorithms, string algorithm, uint flags)
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, null, ExtensionInfo(("server-sig-algs", serverSignatureAlgorithms)), Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk(algorithm, RsaBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", algorithm, RsaBlob, signed: false), written[2]);
        AssertSigned(peer, written[3], algorithm, RsaBlob);
        CollectionAssert.AreEqual(SignRequest(peer, RsaBlob, algorithm, flags), agent.Requests[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RsaIdentityFindsNoAlgorithm_LeavesItsMethodSoNoLaterIdentityAsksAsMeasured()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment").Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, null, ExtensionInfo(("server-sig-algs", "ssh-ed25519")), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        Assert.HasCount(1, agent.Requests, "the list only: nothing is signed");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RsaKeyFileFindsNoAlgorithm_LeavesItsMethodForTheAgentsIdentities()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, Keys(TestUserKeys.RsaPkcs1), ExtensionInfo(("server-sig-algs", "ssh-ed25519")), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
    }

    [TestMethod]
    public async Task AuthenticateAsync_LaterIdentityAfterAnUpgradedRsaIdentity_StartsFromItsOwnType()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment").Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent,
            null,
            ExtensionInfo(("server-sig-algs", "rsa-sha2-256,ssh-ed25519")),
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            PublicKeyOk("ssh-ed25519", Ed25519Blob),
            Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-256", RsaBlob, signed: false), written[2]);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-ed25519", Ed25519Blob, signed: false), written[3]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_QuestionAnsweredWithSuccess_AuthenticatesWithoutASignature()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "publickey");
        Assert.HasCount(1, agent.Requests);
        AssertAgentLines(peer, TryingAgent, "* SSH: agent authenticated user 'tester' with key 'k1-comment'");
    }

    [TestMethod]
    public async Task AuthenticateAsync_SignedRequestRefused_TriesTheNextIdentityAsMeasured()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment").Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent,
            null,
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            PublicKeyOk("ssh-rsa", RsaBlob),
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "password", "publickey", "publickey", "publickey", "keyboard-interactive");
        AssertSigned(peer, written[3], "ssh-rsa", RsaBlob);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-ed25519", Ed25519Blob, signed: false), written[4]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PeerClosesAfterTheSignedRequest_FailsTheIdentityAndGoesOn()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.Ed25519OpenSsh, "k2-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk("ssh-ed25519", Ed25519Blob));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "publickey", "publickey", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    [DataRow("05", DisplayName = "SSH_AGENT_FAILURE")]
    [DataRow("0E00000000", DisplayName = "a sign response cut short before the method")]
    [DataRow("0E000000000000000B7373682D65643235353139", DisplayName = "the right method, no signature")]
    public async Task AuthenticateAsync_AgentCannotSign_FailsTheIdentityWithoutASignedRequest(string signAnswerHex)
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((Ed25519Blob, "k2-comment")), Convert.FromHexString(signAnswerHex)));
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk("ssh-ed25519", Ed25519Blob), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "publickey", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    public async Task AuthenticateAsync_AgentSignsWithAnotherMethod_AsksAgainWithTheKeysOwnTypeWithoutFlags()
    {
        byte[] signature = [1, 2, 3];
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(
            IdentitiesAnswer((RsaBlob, "k1-comment")),
            SignResponse("ssh-rsa", [9, 9]),
            SignResponse("ssh-rsa", signature)));
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent,
            null,
            ExtensionInfo(("server-sig-algs", "rsa-sha2-512")),
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            PublicKeyOk("rsa-sha2-512", RsaBlob),
            PublicKeyOk("ssh-rsa", RsaBlob),
            Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "password", "publickey", "publickey", "publickey");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-512", RsaBlob, signed: false), written[2]);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: false), written[3]);
        CollectionAssert.AreEqual(Join(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: true), String(Join(Name("ssh-rsa"), String(signature)))), written[4]);
        byte[] agentWritten = agent.Written;
        CollectionAssert.AreEqual(
            ScriptedSshAgent.Frames(RequestIdentities, SignRequest(peer, RsaBlob, "rsa-sha2-512", 4), SignRequest(peer, RsaBlob, "ssh-rsa", 0)),
            agentWritten);
    }

    [TestMethod]
    public async Task AuthenticateAsync_AgentSignsWithAnotherMethodTwice_FailsTheIdentity()
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((Ed25519Blob, "k2-comment")), SignResponse("ssh-rsa", [9]), SignResponse("ssh-rsa", [9])));
        KeyedPeer peer = await ConnectWithAgentAsync(
            agent,
            null,
            Failure(AllThreeMethods),
            Failure(AllThreeMethods),
            PublicKeyOk("ssh-ed25519", Ed25519Blob),
            PublicKeyOk("ssh-ed25519", Ed25519Blob),
            Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "publickey", "publickey", "keyboard-interactive");
    }

    [TestMethod]
    [DataRow("ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519", DisplayName = "the certificate's method")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519", "ssh-ed25519", DisplayName = "the plain method")]
    [DataRow("ecdsa-sha2-nistp256", "ecdsa-sha2-nistp256", "ecdsa-sha2-nistp256", DisplayName = "no certificate")]
    public async Task AuthenticateAsync_CertificateIdentity_SignsWithThePlainMethodAsLibssh2Does(string keyType, string agentMethod, string signatureMethod)
    {
        byte[] blob = Join(Name(keyType), [7, 7]);
        byte[] signature = [4, 5];
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((blob, "cert")), SignResponse(agentMethod, signature)));
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk(keyType, blob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(Join(PublicKeyRequest("tester", keyType, blob, signed: true), String(Join(Name(signatureMethod), String(signature)))), written[3]);
    }

    [TestMethod]
    [DataRow("sk-ssh-ed25519@openssh.com", DisplayName = "sk-ssh-ed25519")]
    [DataRow("sk-ecdsa-sha2-nistp256@openssh.com", DisplayName = "sk-ecdsa-sha2-nistp256")]
    [DataRow("sk-ssh-ed25519-cert-v01@openssh.com", DisplayName = "an sk-ssh-ed25519 certificate")]
    public async Task AuthenticateAsync_SecurityKeyIdentity_AppendsTheSignatureFieldsUnwrapped(string keyType)
    {
        string plainMethod = keyType.Replace("-cert-v01@openssh.com", "@openssh.com", StringComparison.Ordinal);
        byte[] blob = Join(Name(keyType), [7]);
        byte[] signature = [4, 5, 6];
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((blob, "sk")), SignResponse(plainMethod, signature)));
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk(keyType, blob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(Join(PublicKeyRequest("tester", keyType, blob, signed: true), String(Join(Name(plainMethod), signature))), written[3]);
    }

    [TestMethod]
    [DataRow("000000", DisplayName = "shorter than a length")]
    [DataRow("0000000B7373682D", DisplayName = "a key type overrunning the blob")]
    public async Task AuthenticateAsync_MalformedIdentityBlob_SkipsTheIdentityWithoutARequest(string blobHex)
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((Convert.FromHexString(blobHex), "bad"))));
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    [DataRow("6B312D636F6D6D656E74006869646465", "k1-comment", DisplayName = "a zero byte ends it, as a C string")]
    [DataRow("C3A9", "é", DisplayName = "UTF-8")]
    public async Task AuthenticateAsync_IdentityAuthenticates_ReportsItsCommentAsCurlPrintsIt(string commentHex, string shown)
    {
        byte[] comment = Convert.FromHexString(commentHex);
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(Join([SshAgentMessageNumber.IdentitiesAnswer], UInt32(1), String(RsaBlob), String(comment))));
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        AssertAgentLines(peer, TryingAgent, $"* SSH: agent authenticated user 'tester' with key '{shown}'");
    }

    [TestMethod]
    public async Task AuthenticateAsync_PasswordAuthenticates_NeverAsksTheAgent()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        Assert.AreEqual(0, agent.Connections);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PublickeyNotListed_NeverAsksTheAgent()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure("password,keyboard-interactive"), Failure("password,keyboard-interactive"), Failure("password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        Assert.AreEqual(0, agent.Connections);
        CollectionAssert.DoesNotContain(peer.Events.Transcript, TryingAgent);
    }

    [TestMethod]
    public async Task AuthenticateAsync_AgentAuthenticatesTheUser_ReportsTheUserNameNotItsBytes()
    {
        InMemorySshAgent agent = new InMemorySshAgent().Add(TestUserKeys.RsaPkcs1, "k1-comment");
        KeyedPeer peer = await ConnectWithAgentAsync(agent, null, Failure(AllThreeMethods), Failure(AllThreeMethods), Success);

        await peer.Authentication.AuthenticateAsync(null, CancellationToken.None);

        CollectionAssert.Contains(peer.Events.Transcript, "* SSH: agent authenticated user '' with key 'k1-comment'");
    }

    private static byte[] IdentitiesAnswer(params (byte[] Blob, string Comment)[] identities) =>
        Join([[SshAgentMessageNumber.IdentitiesAnswer], UInt32((uint)identities.Length), .. identities.Select(identity => Join(String(identity.Blob), Name(identity.Comment)))]);

    private static byte[] SignResponse(string method, byte[] signature) =>
        Join([SshAgentMessageNumber.SignResponse], String(Join(Name(method), String(signature))));

    // The agent's sign request: the key blob, the data the server checks, and the flags.
    private static byte[] SignRequest(KeyedPeer peer, byte[] blob, string algorithm, uint flags) =>
        Join([SshAgentMessageNumber.SignRequest], String(blob), String(Join(String(peer.SessionIdentifier), PublicKeyRequest("tester", algorithm, blob, signed: true))), UInt32(flags));

    private static void AssertAgentLines(KeyedPeer peer, params string[] expected)
    {
        List<string> agentLines = [.. peer.Events.Transcript.SkipWhile(line => line != TryingAgent).TakeWhile(line => !line.Contains("keyboard", StringComparison.Ordinal))];
        CollectionAssert.AreEqual(expected, agentLines, string.Join(Environment.NewLine, peer.Events.Transcript));
    }
}
