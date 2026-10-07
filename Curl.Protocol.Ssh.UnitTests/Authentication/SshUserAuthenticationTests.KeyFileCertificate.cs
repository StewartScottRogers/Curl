using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins a <c>--pubkey</c> OpenSSH certificate signed with its <c>--key</c> private key
/// (BL-1097), measured 2026-10-01 against OpenSSH 10.2's sshd trusting the certificates'
/// CA: curl 8.18.0 (libssh2 1.11.1, OpenSSL) and curl 8.21.0 (libssh2 1.11.1, WinCNG) send
/// the certificate blob under the certificate method, sign with the private key under the
/// plain method, are refused by the server when the certificate certifies another key, and
/// fail the method with "Callback returned error" when the private key is of another type.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    [TestMethod]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, "ssh-rsa-cert-v01@openssh.com", "rsa-sha2-512-cert-v01@openssh.com", "rsa-sha2-512", TestUserKeys.RsaPkcs1, TestUserKeys.RsaPublicKeyFile, DisplayName = "RSA on OpenSSL, as measured")]
    [DataRow(SshAlgorithmPreferences.WinCngBackend, "ssh-rsa-cert-v01@openssh.com", "ssh-rsa-cert-v01@openssh.com", "ssh-rsa", TestUserKeys.RsaPkcs1, TestUserKeys.RsaPublicKeyFile, DisplayName = "RSA on WinCNG, as measured")]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, "ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519", TestUserKeys.Ed25519OpenSsh, TestUserKeys.Ed25519PublicKeyFile, DisplayName = "Ed25519 on OpenSSL, as measured")]
    [DataRow(SshAlgorithmPreferences.OpenSslBackend, "ecdsa-sha2-nistp256-cert-v01@openssh.com", "ecdsa-sha2-nistp256-cert-v01@openssh.com", "ecdsa-sha2-nistp256", TestUserKeys.EcdsaP256Sec1, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "ECDSA on OpenSSL, as measured")]
    public async Task AuthenticateAsync_CertificatePubkeyWithItsPrivateKey_SignsUnderThePlainMethodAsMeasured(string backend, string certificateType, string method, string plainMethod, string privateKeyText, string plainPublicKeyFile)
    {
        byte[] certificateBlob = Join(Name(certificateType), [7, 7]);
        KeyedPeer peer = await ConnectWithBackendAsync(
            backend,
            Keys(privateKeyText, CertificateFile(certificateType, certificateBlob)),
            ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)),
            Failure("publickey,password"),
            PublicKeyOk(method, certificateBlob),
            Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        Assert.HasCount(3, written);
        Diagnostics.Diff("client message 1", PublicKeyRequest("tester", method, certificateBlob, signed: false), written[1]);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", method, certificateBlob, signed: false), written[1]);
        byte[] signedPart = PublicKeyRequest("tester", method, certificateBlob, signed: true);
        Diagnostics.Diff("client message 2[..signedPart.Length]", signedPart, written[2][..signedPart.Length]);
        CollectionAssert.AreEqual(signedPart, written[2][..signedPart.Length]);
        SshWireReader signatureBlob = new(new SshWireReader(written[2].AsMemory(signedPart.Length)).ReadString());
        Assert.AreEqual(plainMethod, signatureBlob.ReadName());
        byte[] plainKeyBlob = SshPublicKeyFile.Parse(plainPublicKeyFile).Key!.Blob;
        Assert.IsTrue(Verify(plainKeyBlob, plainMethod, Join(String(peer.SessionIdentifier), signedPart), signatureBlob.ReadString().ToArray()), "the private key signs");
        AssertLines(peer.Events, Offered, TryingPublicKey, TryingKey, "* SSH: authenticated via publickey");
    }

    [TestMethod]
    public async Task AuthenticateAsync_CertificateOfAnotherKeyOfTheSameType_SignsAndReportsTheRefusalAsMeasured()
    {
        byte[] certificateBlob = Join(Name(RsaCertificateType), [7, 7]);
        KeyedPeer peer = await ConnectWithBackendAsync(
            SshAlgorithmPreferences.OpenSslBackend,
            Keys(TestUserKeys.RsaPkcs1, CertificateFile(RsaCertificateType, certificateBlob)),
            ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)),
            Failure("publickey,password"),
            PublicKeyOk("rsa-sha2-512-cert-v01@openssh.com", certificateBlob),
            Failure("publickey,password"),
            Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey", "publickey", "password");
        Diagnostics.ActLines(peer.Events.Transcript);
        Diagnostics.Assert("verbose line 3", "* SSH: publickey authentication denied: Invalid signature for supplied public key, or bad username/public key combination", peer.Events.Transcript[3]);
        Assert.AreEqual("* SSH: publickey authentication denied: Invalid signature for supplied public key, or bad username/public key combination", peer.Events.Transcript[3]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_CertificateOfAnotherKeyType_SendsNoSignatureAndReportsTheCallbackErrorAsMeasured()
    {
        byte[] certificateBlob = Join(Name(RsaCertificateType), [7, 7]);
        KeyedPeer peer = await ConnectWithBackendAsync(
            SshAlgorithmPreferences.OpenSslBackend,
            Keys(TestUserKeys.Ed25519OpenSsh, CertificateFile(RsaCertificateType, certificateBlob)),
            ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)),
            Failure("publickey,password"),
            PublicKeyOk("rsa-sha2-512-cert-v01@openssh.com", certificateBlob),
            Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey", "password");
        Diagnostics.ActLines(peer.Events.Transcript);
        Diagnostics.Assert("verbose line 3", "* SSH: publickey authentication denied: Callback returned error", peer.Events.Transcript[3]);
        Assert.AreEqual("* SSH: publickey authentication denied: Callback returned error", peer.Events.Transcript[3]);
    }

    private static string CertificateFile(string certificateType, byte[] certificateBlob) =>
        $"{certificateType} {Convert.ToBase64String(certificateBlob)} cert\n";
}
