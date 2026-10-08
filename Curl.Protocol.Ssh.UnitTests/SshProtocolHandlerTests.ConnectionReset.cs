using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Sftp;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins a connection the server resets after the key exchange - a read that throws the
/// <see cref="IOException" /> of a reset, through <see cref="ResettingConnection" /> - as
/// curl 8.21.0 (libssh2 1.11.1) reported a reset injected between it and OpenSSH 10.2 at
/// each step, measured 2026-10-01 (BL-1046, ADR-0289); and an <see cref="IOException" />
/// from the local output, which is not a connection failure.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const string ResetFile = "/data/f";

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_ResetAtTheServiceRequest_IsExit2AndLibssh2sReceiveErrorAsMeasured(string scheme)
    {
        Outcome outcome = await RunResetAsync("service ssh-userauth", $"{scheme}://{Host}{ResetFile}");

        AssertFailure(outcome, CurlExitCode.FailedInit, "Failure establishing ssh session: -43, Failed to get response to ssh-userauth request");
    }

    [TestMethod]
    [DataRow("channel open session", "Unable to startup channel", DisplayName = "channel open")]
    [DataRow("subsystem sftp", "Unable to request SFTP subsystem", DisplayName = "subsystem request")]
    [DataRow("sftp init", "Timeout waiting for response from SFTP subsystem", DisplayName = "INIT")]
    public async Task ExecuteAsync_ResetDuringSftpStartUp_IsExit2WithTheStepsMessageAsMeasured(string step, string message)
    {
        Outcome outcome = await RunResetAsync(step, $"sftp://{Host}{ResetFile}");

        AssertFailure(outcome, CurlExitCode.FailedInit, "Failure initializing sftp session: " + message);
    }

    [TestMethod]
    [DataRow("sftp 16 .", DisplayName = "REALPATH")]
    [DataRow("sftp 17 " + ResetFile, DisplayName = "STAT")]
    [DataRow("sftp 5 " + ResetFile, DisplayName = "first READ")]
    public async Task ExecuteAsync_ResetAtAnSftpRequest_IsExit79AsMeasured(string step)
    {
        Outcome outcome = await RunResetAsync(step, $"sftp://{Host}{ResetFile}");

        AssertFailure(outcome, CurlExitCode.Ssh, "Error in the SSH layer");
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetAtTheSftpOpen_SucceedsWithNothingWrittenAsMeasured()
    {
        Outcome outcome = await RunResetAsync("sftp 3 " + ResetFile, $"sftp://{Host}{ResetFile}");

        Diagnostics.AssertResult(TransferResult.Success(0), outcome.Result);
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.IsEmpty(outcome.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetDuringAnSftpDownload_IsExit79WithTheBytesWrittenAsMeasured()
    {
        Outcome outcome = await RunResetAsync("sftp 5 " + ResetFile, $"sftp://{Host}{ResetFile}", occurrence: 2);

        AssertPartialDownload(outcome);
    }

    [TestMethod]
    [DataRow("channel open session", "Unexpected error", DisplayName = "channel open")]
    [DataRow("exec scp -pf '" + ResetFile + "'", "Failed waiting for channel success", DisplayName = "exec request")]
    [DataRow("scp header", "Failed reading SCP response", DisplayName = "header")]
    public async Task ExecuteAsync_ResetDuringScpStartUp_IsExit79WithTheStepsMessageAsMeasured(string step, string message)
    {
        Outcome outcome = await RunResetAsync(step, $"scp://{Host}{ResetFile}");

        AssertFailure(outcome, CurlExitCode.Ssh, message);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResetDuringAnScpDownload_IsExit79WithTheBytesWrittenAsMeasured()
    {
        Outcome outcome = await RunResetAsync("scp data", $"scp://{Host}{ResetFile}");

        AssertPartialDownload(outcome);
    }

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_LocalOutputWriteFails_PassesTheOutputsIOExceptionOnRatherThanAConnectionFailure(string scheme)
    {
        InMemorySshServer server = Server();
        server.Files[ResetFile] = Hello;
        IOException outputFailure = new("The disk is full.");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"{scheme}://{Host}{ResetFile}"),
            Output = new UnwritableStream(outputFailure),
            Credentials = new NetworkCredential(User, Password),
        };
        ArrangeTransfer(context);
        Diagnostics.Arrange("output failure", outputFailure.Message);

        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await Handler(server).ExecuteAsync(context));
        await server.WhenSessionsEndAsync();

        ActTransfer(null, context, server);
        Diagnostics.Act("thrown", thrown.Message);
        Diagnostics.Assert("thrown is the output's exception", true, ReferenceEquals(outputFailure, thrown));
        Assert.AreSame(outputFailure, thrown);
    }

    // A 100000-byte file, so a download reset after its first block has written part of it.
    private async Task<Outcome> RunResetAsync(string step, string url, int occurrence = 1)
    {
        InMemorySshServer server = new(User, Password) { ResetsAt = step, ResetsAtOccurrence = occurrence };
        server.Files[ResetFile] = [.. Enumerable.Range(0, 100000).Select(index => (byte)index)];
        Diagnostics.Arrange("reset at", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{step} (occurrence {occurrence})"));
        return await RunAsync(server, url);
    }

    private void AssertPartialDownload(Outcome outcome)
    {
        Diagnostics.Assert("exit code", CurlExitCode.Ssh, outcome.Result.ExitCode);
        Diagnostics.Assert("bytes transferred", outcome.Output.Length, outcome.Result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ssh, outcome.Result.ExitCode);
        Assert.AreEqual("Error in the SSH layer", outcome.Result.ErrorMessage);
        Assert.IsTrue(outcome.Output.Length is > 0 and < 100000, $"part of the file is written, not {outcome.Output.Length} bytes");
        Assert.AreEqual(outcome.Output.Length, outcome.Result.BytesTransferred);
    }

    private sealed class UnwritableStream(IOException failure) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw failure;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw failure;
    }
}
