using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins the lines <see cref="SshDiagnosticLog" /> words for the steps an end-to-end session
/// does not reach (BL-925), and that a disabled level writes nothing.
/// </summary>
[TestClass]
public sealed class SshDiagnosticLogTests
{
    private static readonly SshNegotiatedAlgorithms Algorithms = new(
        "curve25519-sha256", "ssh-ed25519", "aes256-gcm@openssh.com", "aes256-gcm@openssh.com", null, null, "none", "none", true, false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Failed_SessionStartupFailure_NamesTheExitCodeAndTheLibssh2Code()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("level", DiagnosticLogLevel.Error);
        Diagnostics.Arrange("failure", "SessionEstablishmentFailed(-5, \"Unable to exchange encryption keys\")");

        new SshDiagnosticLog(recording).Failed(SshTransferException.SessionEstablishmentFailed(-5, "Unable to exchange encryption keys"));

        WriteLines(
            new[] { "failed with FailedInit (2): Failure establishing ssh session: -5, Unable to exchange encryption keys (libssh2 -5)" },
            recording.At(DiagnosticLogLevel.Error));
        CollectionAssert.AreEqual(
            new[] { "failed with FailedInit (2): Failure establishing ssh session: -5, Unable to exchange encryption keys (libssh2 -5)" },
            recording.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public void KeysReExchanged_ImplicitMacs_NamesTheAlgorithmsInForce()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("level", DiagnosticLogLevel.Info);
        Diagnostics.Arrange("algorithms", "curve25519-sha256, ssh-ed25519, aes256-gcm@openssh.com both ways, no MACs, no compression");

        new SshDiagnosticLog(recording).KeysReExchanged(Algorithms);

        WriteLines(
            new[] { "key re-exchange: kex curve25519-sha256, host key ssh-ed25519, cipher aes256-gcm@openssh.com/aes256-gcm@openssh.com, MAC implicit/implicit, compression none/none" },
            recording.At(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "key re-exchange: kex curve25519-sha256, host key ssh-ed25519, cipher aes256-gcm@openssh.com/aes256-gcm@openssh.com, MAC implicit/implicit, compression none/none" },
            recording.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void StepsNotReachedEndToEnd_AreWordedAsPinned()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        SshDiagnosticLog log = new(recording);
        Diagnostics.Arrange("level", DiagnosticLogLevel.Verbose);
        Diagnostics.Arrange("steps", "no methods listed, channel refused, exec refused, SFTP answer 101, scp upload started, transfer failed");

        log.AuthenticationMethodsListed(null);
        log.ChannelOpened(false);
        log.ChannelRequested("exec"u8.ToArray(), "scp -t '/x'"u8.ToArray(), succeeded: false);
        log.SftpAnswerRead([101, 0, 0, 0, 1]);
        log.TransferStarted("scp", "/x", isUpload: true);
        log.TransferEnded(TransferResult.Failure(CurlExitCode.PartialFile, "short"), TimeSpan.Zero);

        string[] expected =
        [
            "authenticated with none", "session channel refused", "channel request exec scp -t '/x': refused", "SFTP answer 101",
            "scp upload to /x started", "failed with PartialFile (18): short",
        ];
        WriteLines(expected, recording.Lines.Select(line => line.Message).ToArray());
        CollectionAssert.AreEqual(expected, recording.Lines.Select(line => line.Message).ToArray());
    }

    [TestMethod]
    public void EveryStep_AtNone_WritesNothing()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        SshDiagnosticLog log = new(recording);
        Diagnostics.Arrange("level", DiagnosticLogLevel.None);
        Diagnostics.Arrange("steps", "every SshDiagnosticLog step once");

        log.MessageSent(20);
        log.MessageReceived(20);
        log.HandshakeNegotiated(new SshNegotiatedHandshake("SSH-2.0-c", "SSH-2.0-s", [], [], Algorithms));
        log.KeysExchanged("curve25519-sha256", TimeSpan.Zero);
        log.KeysReExchanged(Algorithms);
        log.HostKeyPresented([]);
        log.HostKeyAccepted(new SshOptions(), null);
        log.AuthenticationMethodsListed("password");
        log.AuthenticationTried("password");
        log.AuthenticationEnded("password", succeeded: false);
        log.ChannelOpened(true);
        log.ChannelRequested([], [], succeeded: true);
        log.SftpRequestSent(3, 0);
        log.SftpAnswerRead([101]);
        log.TransferStarted("sftp", "/f", isUpload: false);
        log.TransferEnded(TransferResult.Success(1), TimeSpan.Zero);
        log.TransferEnded(TransferResult.Failure(CurlExitCode.PartialFile, "short"), TimeSpan.Zero);
        log.Failed(SshTransferException.SshLayerError());

        Diagnostics.Act("lines written", recording.Lines.Count);
        Diagnostics.Assert("lines written", 0, recording.Lines.Count);
        Assert.IsEmpty(recording.Lines);
    }

    [TestMethod]
    public void Failed_FailureWithoutALibssh2Code_NamesTheExitCodeAlone()
    {
        var recording = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("level", DiagnosticLogLevel.Error);
        Diagnostics.Arrange("failure", "SshLayerError()");

        new SshDiagnosticLog(recording).Failed(SshTransferException.SshLayerError());

        WriteLines(new[] { "failed with Ssh (79): Error in the SSH layer" }, recording.At(DiagnosticLogLevel.Error));
        CollectionAssert.AreEqual(new[] { "failed with Ssh (79): Error in the SSH layer" }, recording.At(DiagnosticLogLevel.Error));
    }

    private void WriteLines(string[] expected, string[] actual)
    {
        string expectedText = string.Join(" | ", expected);
        string actualText = string.Join(" | ", actual);
        Diagnostics.Act("lines", actualText);
        Diagnostics.Assert("lines", expectedText, actualText);
        Diagnostics.Diff("lines", expectedText, actualText);
    }
}
