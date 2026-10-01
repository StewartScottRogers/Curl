using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins an Ed25519 or ECDSA <c>--key</c> on the WinCNG preset (BL-1098), measured
/// 2026-10-01 against OpenSSH 10.2's sshd: curl 8.21.0 (libssh2 1.11.1, WinCNG) asks the
/// question with a <c>--pubkey</c> file, gets <c>PK_OK</c> and fails the method with
/// "Callback returned error" before signing; without <c>--pubkey</c> it sends no
/// <c>publickey</c> request and reports "Reason unknown (-1)". curl 8.18.0 (libssh2 1.11.1,
/// OpenSSL) authenticates with the same keys, and so does the <c>Full</c> preset.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    [TestMethod]
    [DataRow(TestUserKeys.Ed25519OpenSsh, TestUserKeys.Ed25519PublicKeyFile, DisplayName = "Ed25519, as measured")]
    [DataRow(TestUserKeys.EcdsaP256OpenSsh, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "ECDSA P-256 openssh-key-v1, as measured")]
    [DataRow(TestUserKeys.EcdsaP256Sec1, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "ECDSA P-256 SEC 1 PEM, as measured")]
    [DataRow(TestUserKeys.EcdsaP384Sec1, TestUserKeys.EcdsaP384PublicKeyFile, DisplayName = "ECDSA P-384 SEC 1 PEM")]
    public async Task AuthenticateAsync_WinCngEd25519OrEcdsaKeyWithPubkey_AsksThenReportsTheCallbackErrorAsMeasured(string privateKeyText, string publicKeyText)
    {
        SshPublicKey publicKey = SshPublicKeyFile.Parse(publicKeyText).Key!;
        KeyedPeer peer = await ConnectWithBackendAsync(
            SshAlgorithmPreferences.WinCngBackend,
            Keys(privateKeyText, publicKeyText),
            ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)),
            Failure("publickey,password"),
            PublicKeyOk(publicKey.KeyType, publicKey.Blob),
            Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "publickey", "password");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", publicKey.KeyType, publicKey.Blob, signed: false), written[1]);
        AssertLines(peer.Events, Offered, TryingPublicKey, TryingKey, "* SSH: publickey authentication denied: Callback returned error", "* SSH: trying publickey authentication via agent", "* SSH: failure connecting to agent");
    }

    [TestMethod]
    [DataRow(TestUserKeys.Ed25519OpenSsh, DisplayName = "Ed25519, as measured")]
    [DataRow(TestUserKeys.Ed25519Pkcs8, DisplayName = "Ed25519 PKCS #8")]
    [DataRow(TestUserKeys.EcdsaP256OpenSsh, DisplayName = "ECDSA P-256 openssh-key-v1, as measured")]
    [DataRow(TestUserKeys.EcdsaP256Sec1, DisplayName = "ECDSA P-256 SEC 1 PEM, as measured")]
    [DataRow(TestUserKeys.EcdsaP256Pkcs8, DisplayName = "ECDSA P-256 PKCS #8, as measured")]
    public async Task AuthenticateAsync_WinCngEd25519OrEcdsaKeyWithoutPubkey_SendsNoPublicKeyRequestAndReportsReasonUnknownAsMeasured(string privateKeyText)
    {
        KeyedPeer peer = await ConnectWithBackendAsync(
            SshAlgorithmPreferences.WinCngBackend, Keys(privateKeyText), Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
        AssertLines(peer.Events, Offered, TryingKey, "* SSH: publickey authentication denied: Reason unknown (-1)", "* SSH: trying publickey authentication via agent", "* SSH: failure connecting to agent");
    }

    [TestMethod]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, TestUserKeys.Ed25519OpenSsh, TestUserKeys.Ed25519PublicKeyFile, DisplayName = "Ed25519 on OpenSSL, as measured")]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, TestUserKeys.EcdsaP256Sec1, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "ECDSA on OpenSSL, as measured")]
    [DataRow(null, TestUserKeys.Ed25519OpenSsh, TestUserKeys.Ed25519PublicKeyFile, DisplayName = "Ed25519 on Full")]
    [DataRow(null, TestUserKeys.EcdsaP256Sec1, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "ECDSA on Full")]
    [DataRow(SshAlgorithmPreferences.WinCngBackend, TestUserKeys.RsaPkcs1, TestUserKeys.RsaPublicKeyFile, DisplayName = "PKCS #1 RSA on WinCNG, as measured")]
    public async Task AuthenticateAsync_KeyTheBackendReads_SignsWithAndWithoutPubkey(string? backend, string privateKeyText, string publicKeyText)
    {
        foreach (string? pubkey in new[] { publicKeyText, null })
        {
            SshPublicKey publicKey = SshPublicKeyFile.Parse(publicKeyText).Key!;
            KeyedPeer peer = await ConnectWithPresetAsync(
                SshAlgorithmPreferences.Full with { CryptographyBackend = backend },
                null,
                Keys(privateKeyText, pubkey),
                [Failure("publickey,password"), PublicKeyOk(publicKey.KeyType, publicKey.Blob), Success]);

            await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

            List<byte[]> written = await AuthenticationMessagesAsync(peer);
            Assert.HasCount(3, written);
            Assert.AreEqual("* SSH: authenticated via publickey", peer.Events.Transcript[^1]);
        }
    }
}
