using Curl.Ssh.Fakes;

namespace Curl.Ssh.Negotiation;

/// <summary>
/// Pins where <c>ssh-ed25519</c> is a host-key algorithm the client offers in its <c>KEXINIT</c>
/// (RFC 8709 section 4, RFC 4253 section 7.1): on libssh2 1.11.1 over OpenSSL, as upstream
/// curl 8.21.0's OpenSSL build does (BL-5279), and not on WinCNG, which has no Ed25519.
/// </summary>
[TestClass]
public sealed class SshAlgorithmPreferencesEd25519HostKeyTests
{
    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["zlib", "zlib@openssh.com", "none"]));

    [TestMethod]
    public void ForClient_OpenSslReference_ListsSshEd25519AmongTheHostKeyAlgorithms()
    {
        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, EverythingImplemented, new RepeatingRandomSource(0));

        CollectionAssert.Contains(kexInit.ServerHostKey.ToArray(), "ssh-ed25519");
    }

    [TestMethod]
    public void HostKeysNeverAgreed_OpenSslReference_LeavesSshEd25519AgreeableSoItsKeyIsJudged()
    {
        SshAlgorithmPreferences preset = SshAlgorithmPreferences.OpenSslReference;

        Assert.IsFalse(preset.HostKeysNeverAgreed.Contains("ssh-ed25519"));
    }

    [TestMethod]
    public void ForClient_WindowsReference_DoesNotListSshEd25519BecauseWinCngHasNone()
    {
        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0));

        CollectionAssert.DoesNotContain(kexInit.ServerHostKey.ToArray(), "ssh-ed25519");
    }
}
