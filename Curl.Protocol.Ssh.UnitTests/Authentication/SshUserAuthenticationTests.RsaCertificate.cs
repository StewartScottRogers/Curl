using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins the upgrade of an <c>ssh-rsa-cert-v01@openssh.com</c> identity from
/// <c>server-sig-algs</c> (BL-1036, ADR-0311), measured 2026-10-01 with an
/// <c>ssh-keygen -s</c> certificate against a loopback server built from this library:
/// curl 8.18.0 with libssh2 1.11.1 on OpenSSL upgrades it to the
/// <c>rsa-sha2-*-cert-v01@openssh.com</c> method, except for an OpenSSH 7.7 or older banner,
/// and curl 8.21.0 with libssh2 1.11.1 on WinCNG never does.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    private const string RsaCertificateType = "ssh-rsa-cert-v01@openssh.com";

    private static readonly byte[] RsaCertificateBlob = Join(Name(RsaCertificateType), [7, 7]);

    [TestMethod]
    [DataRow("ssh-ed25519,rsa-sha2-512,rsa-sha2-256,ssh-rsa", "rsa-sha2-512", DisplayName = "rsa-sha2-512 named, as measured")]
    [DataRow("rsa-sha2-256", "rsa-sha2-256", DisplayName = "rsa-sha2-256 named, as measured")]
    [DataRow("ssh-rsa", "ssh-rsa", DisplayName = "ssh-rsa named")]
    public async Task AuthenticateAsync_RsaCertificateWithServerSigAlgsOnOpenSsl_SignsWithTheCertificateMethodAsMeasured(string serverSignatureAlgorithms, string plainMethod)
    {
        string method = plainMethod + "-cert-v01@openssh.com";
        byte[] signature = [4, 5];
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((RsaCertificateBlob, "cert")), SignResponse(plainMethod, signature)));
        KeyedPeer peer = await ConnectWithPresetAsync(
            SshAlgorithmPreferences.Full with { CryptographyBackend = SshAlgorithmPreferences.OpenSslBackend },
            agent,
            null,
            [ExtensionInfo(("server-sig-algs", serverSignatureAlgorithms)), Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk(method, RsaCertificateBlob), Success]);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", method, RsaCertificateBlob, signed: false), written[2]);
        CollectionAssert.AreEqual(ScriptedSshAgent.Frames(RequestIdentities, SignRequest(peer, RsaCertificateBlob, method, 0)), agent.Written, "a certificate method asks with flag 0");
        CollectionAssert.AreEqual(Join(PublicKeyRequest("tester", method, RsaCertificateBlob, signed: true), String(Join(Name(plainMethod), String(signature)))), written[3]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RsaCertificateWithNoRsaServerSigAlgOnOpenSsl_SendsNoRequestAsMeasured()
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((RsaCertificateBlob, "cert"))));
        KeyedPeer peer = await ConnectWithPresetAsync(
            SshAlgorithmPreferences.Full with { CryptographyBackend = SshAlgorithmPreferences.OpenSslBackend },
            agent,
            null,
            [ExtensionInfo(("server-sig-algs", "ssh-ed25519")), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods)]);

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password", "keyboard-interactive");
        AssertAgentLines(peer, TryingAgent, NoIdentityMatched);
    }

    [TestMethod]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, null, null, DisplayName = "OpenSSL, server-sig-algs absent, as measured")]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, "ssh-ed25519,rsa-sha2-512", "SSH-2.0-OpenSSH_7.7", DisplayName = "OpenSSL, an OpenSSH 7.7 banner, as measured")]
    [DataRow(SshAlgorithmPreferences.WinCngBackend, "ssh-ed25519,rsa-sha2-512,rsa-sha2-256,ssh-rsa", null, DisplayName = "WinCNG, rsa-sha2-512 named, as measured")]
    [DataRow(SshAlgorithmPreferences.WinCngBackend, "ssh-ed25519", null, DisplayName = "WinCNG, no RSA algorithm named, as measured")]
    public async Task AuthenticateAsync_RsaCertificateNotUpgraded_SignsAsItsOwnTypeAsMeasured(string backend, string? serverSignatureAlgorithms, string? banner)
    {
        byte[] signature = [4, 5];
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((RsaCertificateBlob, "cert")), SignResponse("ssh-rsa", signature)));
        byte[][] answers = [Failure(AllThreeMethods), Failure(AllThreeMethods), PublicKeyOk(RsaCertificateType, RsaCertificateBlob), Success];
        KeyedPeer peer = await ConnectWithPresetAsync(
            SshAlgorithmPreferences.Full with { CryptographyBackend = backend },
            agent,
            null,
            serverSignatureAlgorithms is null ? answers : [ExtensionInfo(("server-sig-algs", serverSignatureAlgorithms)), .. answers],
            banner ?? TestKeyExchangeServer.ServerIdentification);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", RsaCertificateType, RsaCertificateBlob, signed: false), written[2]);
        CollectionAssert.AreEqual(ScriptedSshAgent.Frames(RequestIdentities, SignRequest(peer, RsaCertificateBlob, RsaCertificateType, 0)), agent.Written);
        CollectionAssert.AreEqual(Join(PublicKeyRequest("tester", RsaCertificateType, RsaCertificateBlob, signed: true), String(Join(Name("ssh-rsa"), String(signature)))), written[3]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RsaCertificateWithAnOpenSsh78Banner_IsUpgradedAsMeasured()
    {
        ScriptedSshAgent agent = new(ScriptedSshAgent.Frames(IdentitiesAnswer((RsaCertificateBlob, "cert")), SignResponse("rsa-sha2-512", [4])));
        KeyedPeer peer = await ConnectWithPresetAsync(
            SshAlgorithmPreferences.Full with { CryptographyBackend = SshAlgorithmPreferences.OpenSslBackend },
            agent,
            null,
            [ExtensionInfo(("server-sig-algs", "ssh-ed25519,rsa-sha2-512")), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods), Failure(AllThreeMethods)],
            "SSH-2.0-OpenSSH_7.8");

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-512-cert-v01@openssh.com", RsaCertificateBlob, signed: false), written[2]);
    }
}
