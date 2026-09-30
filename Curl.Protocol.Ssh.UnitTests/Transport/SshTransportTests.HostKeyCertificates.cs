using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Transport;

public sealed partial class SshTransportTests
{
    [TestMethod]
    [DataRow("expired", DisplayName = "valid before 1970-01-01T00:00:01Z and for another host")]
    [DataRow("forged", DisplayName = "a CA signature that does not verify")]
    [DataRow("truncated", DisplayName = "nothing after the certified key")]
    public async Task ExchangeKeysAsync_CertificateItselfUntrustworthy_StillVerifiesWithTheCertifiedKeyAsLibssh2Does(string defect)
    {
        const string name = "ssh-ed25519-cert-v01@openssh.com";
        TestHostKey valid = TestHostKey.Certificate(name, TestHostKey.Ed25519());
        byte[] start = valid.Blob[..(4 + name.Length + 4 + 32 + 4 + 32)];
        byte[] body = defect switch
        {
            "expired" => TestHostKey.CertificateBody(start, ["other.example"], validBefore: 1),
            "forged" => [.. valid.Blob[start.Length..^1], (byte)(valid.Blob[^1] ^ 0x01)],
            _ => [],
        };
        ScriptedExchange run = Script("curve25519-sha256", TestHostKey.Certificate(name, TestHostKey.Ed25519(), body));

        SshKeyExchangeResult result = await ExchangeAsync(run);

        Assert.AreEqual(name, result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(run.Server.ExchangeHash, result.ExchangeHash);
    }

    [TestMethod]
    [DataRow("name", DisplayName = "a certificate blob naming another certificate type")]
    [DataRow("nonce", DisplayName = "no nonce")]
    [DataRow("key", DisplayName = "the certified key cut short")]
    public async Task ExchangeKeysAsync_MalformedCertificate_FailsWithMinus8(string defect)
    {
        const string name = "ssh-ed25519-cert-v01@openssh.com";
        TestHostKey valid = TestHostKey.Certificate(name, TestHostKey.Ed25519());
        byte[] blob = defect switch
        {
            "name" => Join(Name("ssh-rsa-cert-v01@openssh.com"), valid.Blob[(4 + name.Length)..]),
            "nonce" => Name(name),
            _ => Join(Name(name), String(new byte[32]), UInt32(32), new byte[16]),
        };

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", valid with { Blob = blob }));
    }

    [TestMethod]
    [DataRow("key", DisplayName = "a 31-byte sk-ssh-ed25519 public key")]
    [DataRow("signature", DisplayName = "a 63-byte sk-ssh-ed25519 signature")]
    [DataRow("counter", DisplayName = "an sk-ssh-ed25519 signature without its counter")]
    public async Task ExchangeKeysAsync_MalformedSecurityKeyEd25519HostKeyOrSignature_FailsWithMinus8(string defect)
    {
        const string name = "sk-ssh-ed25519@openssh.com";
        TestHostKey valid = TestHostKey.SecurityKeyEd25519();
        TestHostKey broken = defect switch
        {
            "key" => valid with { Blob = Join(Name(name), String(new byte[31]), String(TestHostKey.SecurityKeyApplication)) },
            "signature" => valid with { Sign = h => Join(Name(name), String(new byte[63]), [0x01, 0, 0, 0, 7]) },
            _ => valid with { Sign = h => valid.Sign(h)[..^4] },
        };

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", broken));
    }

    [TestMethod]
    [DataRow("curve", DisplayName = "an sk-ecdsa key naming nistp384")]
    [DataRow("r", DisplayName = "an sk-ecdsa r longer than the field")]
    [DataRow("flags", DisplayName = "an sk-ecdsa signature with other flags than were signed")]
    public async Task ExchangeKeysAsync_MalformedSecurityKeyEcdsaHostKeyOrSignature_FailsWithMinus8(string defect)
    {
        const string name = "sk-ecdsa-sha2-nistp256@openssh.com";
        TestHostKey valid = TestHostKey.SecurityKeyEcdsa();
        byte[] point = [0x04, .. TestHostKey.FixedNistP256.Q.X!, .. TestHostKey.FixedNistP256.Q.Y!];
        TestHostKey broken = defect switch
        {
            "curve" => valid with { Blob = Join(Name(name), Name("nistp384"), String(point), String(TestHostKey.SecurityKeyApplication)) },
            "r" => valid with { Sign = h => Join(Name(name), String(Join(Mpint([0x01, .. new byte[32]]), Mpint([0x01]))), [0x01, 0, 0, 0, 7]) },
            _ => valid with { Sign = h => [.. valid.Sign(h)[..^5], 0x05, 0, 0, 0, 7] },
        };

        await AssertKeyExchangeFailsAsync(Script("curve25519-sha256", broken));
    }

    [TestMethod]
    [DataRow("Windows", "rsa-sha2-512-cert-v01@openssh.com", DisplayName = "measured: the Windows build and rsa-sha2-512-cert-v01@openssh.com")]
    [DataRow("Windows", "rsa-sha2-256-cert-v01@openssh.com", DisplayName = "measured: the Windows build and rsa-sha2-256-cert-v01@openssh.com")]
    [DataRow("Windows", "ssh-rsa-cert-v01@openssh.com", DisplayName = "measured: the Windows build and ssh-rsa-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "ecdsa-sha2-nistp256-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and ecdsa-sha2-nistp256-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "ecdsa-sha2-nistp384-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and ecdsa-sha2-nistp384-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "ecdsa-sha2-nistp521-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and ecdsa-sha2-nistp521-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "rsa-sha2-512-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and rsa-sha2-512-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "rsa-sha2-256-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and rsa-sha2-256-cert-v01@openssh.com")]
    [DataRow("OpenSSL", "ssh-rsa-cert-v01@openssh.com", DisplayName = "measured: the OpenSSL build and ssh-rsa-cert-v01@openssh.com")]
    public async Task NegotiateAlgorithmsAsync_ReferencePresetAndOnlyAnRsaOrEcdsaCertificateShared_FailsWithMinus5AsMeasured(string platform, string hostKey)
    {
        SshAlgorithmPreferences preset = platform == "Windows" ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;
        SshTransport transport = ReferenceTransport(preset, hostKey);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await transport.NegotiateAlgorithmsAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("Failure establishing ssh session: -5, Unable to exchange encryption keys", failure.Message);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_OpenSslPresetAndAnEd25519Certificate_AgreesItAsMeasured()
    {
        SshTransport transport = ReferenceTransport(SshAlgorithmPreferences.OpenSslReference, "ssh-ed25519-cert-v01@openssh.com");

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        Assert.AreEqual("ssh-ed25519-cert-v01@openssh.com", handshake.Algorithms.ServerHostKey);
    }

    [TestMethod]
    public async Task NegotiateAlgorithmsAsync_ReferencePresetAndACertificateBeforeAPlainKey_AgreesThePlainKey()
    {
        SshTransport transport = ReferenceTransport(SshAlgorithmPreferences.WindowsReference, "rsa-sha2-512-cert-v01@openssh.com", "ssh-rsa");

        SshNegotiatedHandshake handshake = await transport.NegotiateAlgorithmsAsync(CancellationToken.None);

        Assert.AreEqual("ssh-rsa", handshake.Algorithms.ServerHostKey);
    }

    private static SshTransport ReferenceTransport(SshAlgorithmPreferences preset, params string[] serverHostKeys)
    {
        SshKexInit server = ServerKexInit("diffie-hellman-group14-sha256", serverHostKeys[0], strict: false) with { ServerHostKey = serverHostKeys };
        byte[] serverBytes = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(server).Bytes;
        return new SshTransport(new ScriptedConnection(serverBytes), preset, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0), new TestEphemeralKeys());
    }
}
