using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins how <see cref="SftpSession" /> starts and ends, against an in-memory peer, with
/// the outcomes measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1) against OpenSSH 10.2
/// running a scripted SFTP subsystem (BL-569, ADR-0220).
/// </summary>
[TestClass]
public sealed class SftpSessionTests
{
    private const string InitializationFailedPrefix = "Failure initializing sftp session: ";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task StartAsync_ServerAnswersVersion3_SendsInitForVersion3()
    {
        SftpServerScript script = SftpServerScript.Started();
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);

        await SftpSession.StartAsync(Transport(connection), CancellationToken.None);
        Diagnostics.ActRequests(connection.Written);

        List<byte[]> requests = SftpServerScript.SftpRequests(connection.Written);
        Diagnostics.Assert("request count", 1, requests.Count);
        Diagnostics.Diff("SSH_FXP_INIT", new byte[] { SftpPacketType.Init, 0, 0, 0, 3 }, requests[0]);
        Assert.HasCount(1, requests);
        CollectionAssert.AreEqual(new byte[] { SftpPacketType.Init, 0, 0, 0, 3 }, requests[0]);
    }

    [TestMethod]
    [DataRow(2u, DisplayName = "version 2, accepted as measured")]
    [DataRow(4u, DisplayName = "version 4, accepted as measured")]
    public async Task StartAsync_ServerNamesAnotherVersion_AcceptsIt(uint version)
    {
        SftpServerScript script = Subsystem().Sftp([SftpPacketType.Version, .. UInt32(version)]);
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);

        SftpSession session = await SftpSession.StartAsync(Transport(connection), CancellationToken.None);
        Diagnostics.ActRequests(connection.Written);

        Diagnostics.Assert("session started", true, session is not null);
    }

    [TestMethod]
    public async Task StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured()
    {
        SftpServerScript script = Subsystem()
            .Sftp([SftpPacketType.Status, .. UInt32(0), .. UInt32(4)])
            .Sftp(SftpPacketType.Version)
            .Sftp(SftpPacketType.Version, 0, 0)
            .Sftp(Join([SftpPacketType.Version], UInt32(3), Name("xyz"), Name("1")));
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);

        SftpSession session = await SftpSession.StartAsync(Transport(connection), CancellationToken.None);
        Diagnostics.ActRequests(connection.Written);

        Diagnostics.Assert("session started", true, session is not null);
    }

    [TestMethod]
    [DataRow("000000037879", "Data too short when extracting extname", DisplayName = "name cut short, as measured")]
    [DataRow("0000", "Data too short when extracting extname", DisplayName = "name length cut short")]
    [DataRow("0000000378797A", "Data too short when extracting extdata", DisplayName = "no data, as measured")]
    [DataRow("0000000378797A00000005616263", "Data too short when extracting extdata", DisplayName = "data cut short")]
    public async Task StartAsync_ExtensionCutShort_FailsAsMeasured(string extensionsHex, string expected)
    {
        SftpServerScript script = Subsystem().Sftp([SftpPacketType.Version, .. UInt32(3), .. Convert.FromHexString(extensionsHex)]);

        await AssertStartFailsAsync(script, expected);
    }

    [TestMethod]
    public async Task StartAsync_ChannelRefused_FailsWithUnableToStartupChannel()
    {
        SftpServerScript script = new SftpServerScript()
            .Ssh(Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(0), UInt32(1), Name("no"), Name(string.Empty)));

        await AssertStartFailsAsync(script, "Unable to startup channel");
    }

    [TestMethod]
    public async Task StartAsync_PeerClosesBeforeConfirming_FailsWithUnableToStartupChannel()
    {
        await AssertStartFailsAsync(new SftpServerScript(), "Unable to startup channel");
    }

    [TestMethod]
    public async Task StartAsync_SubsystemRefused_FailsAsMeasuredWithNoSubsystemConfigured()
    {
        SftpServerScript script = new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelFailure, .. UInt32(0)]);

        await AssertStartFailsAsync(script, "Unable to request SFTP subsystem");
    }

    [TestMethod]
    public async Task StartAsync_ConnectionEndsBeforeTheVersion_FailsAsMeasured()
    {
        await AssertStartFailsAsync(Subsystem(), "Timeout waiting for response from SFTP subsystem");
    }

    [TestMethod]
    public async Task StartAsync_ChannelEndsBeforeTheVersion_FailsWithTheSameMessage()
    {
        SftpServerScript script = Subsystem().Ssh([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)]);

        await AssertStartFailsAsync(script, "Timeout waiting for response from SFTP subsystem");
    }

    [TestMethod]
    public async Task StartAsync_ChannelEndsInsideThePacket_FailsWithTheSameMessage()
    {
        SftpServerScript script = Subsystem().ChannelData([0, 0, 0, 9, SftpPacketType.Version]);

        await AssertStartFailsAsync(script, "Timeout waiting for response from SFTP subsystem");
    }

    [TestMethod]
    public async Task ShutdownAsync_ClosesTheChannelAsLibssh2Does()
    {
        SftpServerScript script = SftpServerScript.Started().Ssh([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]);
        Diagnostics.ArrangeScript(script);
        ScriptedConnection connection = new(script.Bytes);
        SftpSession session = await SftpSession.StartAsync(Transport(connection), CancellationToken.None);

        await session.ShutdownAsync(CancellationToken.None);

        List<byte[]> written = SftpServerScript.SshPayloads(connection.Written);
        Diagnostics.Act("SSH payloads written", $"{written.Count}, message numbers {string.Join(", ", written.Select(payload => payload[0]))}");
        Diagnostics.Diff("second-last payload, SSH_MSG_CHANNEL_EOF", Join([SshConnectionMessageNumber.ChannelEof], UInt32(SftpServerScript.ServerChannel)), written[^2]);
        Diagnostics.Diff("last payload, SSH_MSG_CHANNEL_CLOSE", Join([SshConnectionMessageNumber.ChannelClose], UInt32(SftpServerScript.ServerChannel)), written[^1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], UInt32(SftpServerScript.ServerChannel)), written[^2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], UInt32(SftpServerScript.ServerChannel)), written[^1]);
    }

    internal static SshTransport Transport(ScriptedConnection connection) =>
        new(connection, SshAlgorithmPreferences.Full, new SshAlgorithmCatalogue(["none"]), new RepeatingRandomSource(0x33), new TestEphemeralKeys());

    private static SftpServerScript Subsystem() =>
        new SftpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)]);

    private async Task AssertStartFailsAsync(SftpServerScript script, string expected)
    {
        Diagnostics.ArrangeScript(script);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SftpSession.StartAsync(Transport(new ScriptedConnection(script.Bytes)), CancellationToken.None));
        Diagnostics.ActFailure(failure);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, failure.ExitCode);
        Diagnostics.Diff("message", InitializationFailedPrefix + expected, failure.Message);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(InitializationFailedPrefix + expected, failure.Message);
    }
}
