using Curl.Protocol.Abstractions;
using Curl.Ssh.Fakes;
using Curl.Ssh.Negotiation;
using Curl.Testing;

namespace Curl.Ssh.HostKeys;

/// <summary>
/// Pins that <c>--hostpubsha256</c>, like <c>--hostpubmd5</c>, stops known_hosts from narrowing
/// the offered host-key types (BL-5106, measured against OpenSSL curl 8.18).
/// </summary>
[TestClass]
public sealed class HostPublicKeySha256NarrowingTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 2222;
    private const string Sha256 = "c4nEydciX4yHnXnQAkUIFU6HFGMS8bFT8zZXUIpUX+U=";

    private static readonly KnownHostsFile RsaFirst = KnownHostsFile.Parse($"{Host} ssh-rsa {KnownHostsFileTests.HostKeyBase64}\n");

    [TestMethod]
    public void NarrowHostKeys_Sha256GivenAndRsaEntryFirst_LeavesTheListAndSaysNothing()
    {
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostsPath = "kh", HostPublicKeySha256 = Sha256 };

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(
            SshAlgorithmPreferences.WindowsReference, Host, Port, options, RsaFirst, events);

        Assert.AreSame(SshAlgorithmPreferences.WindowsReference, narrowed);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public void NarrowHostKeys_NoFingerprintGiven_StillNarrowsAndSaysSo()
    {
        TranscriptTransferEvents events = new();
        SshOptions options = new() { KnownHostsPath = "kh" };

        SshAlgorithmPreferences narrowed = SshHostKeyChecker.NarrowHostKeys(
            SshAlgorithmPreferences.WindowsReference, Host, Port, options, RsaFirst, events);

        Assert.AreEqual("rsa-sha2-256,rsa-sha2-512,ssh-rsa", string.Join(',', narrowed.ServerHostKey));
        CollectionAssert.Contains(events.Transcript.ToList(), "* SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type");
    }

    [TestMethod]
    public void Check_WrongSha256_IsRefusedWithExit60()
    {
        SshOptions options = new() { HostPublicKeySha256 = "c4nEydciX4yHnX" };

        SshTransferException exception = Assert.ThrowsExactly<SshTransferException>(
            () => SshHostKeyChecker.Check(KnownHostsFileTests.HostKey, Host, Port, options, RsaFirst));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, exception.ExitCode);
        Assert.AreEqual(
            $"Denied establishing ssh session: mismatch SHA256 fingerprint. Remote {Sha256} is not equal to c4nEydciX4yHnX",
            exception.Message);
    }
}
