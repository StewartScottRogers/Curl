using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins CGR-107's SSH-08 (BL-3957): an <c>sftp://</c> or <c>scp://</c> transfer puts its
/// <c>-w</c> times on the report as curl 8.22.0's libssh2 backend takes them - the
/// connector's name lookup and connect, appconnect at <c>Authentication complete</c>,
/// pretransfer, starttransfer at the first byte and the total, in that order - and a
/// failed transfer of a missing file still has its connect and total times.
/// </summary>
[TestClass]
public sealed class Cgr107TimingTests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_Download_ReportsEveryTimeInCurlsOrder(string scheme)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/big.bin"] = new byte[100_000];

        TransferResult result = await RunAsync(server, $"{scheme}://files.example/data/big.bin");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        TransferTimings timings = result.Report!.Timings!;
        ConnectTimings connect = timings.Connect!;
        long?[] ordered = [timings.Started, connect.NameResolved, connect.Connected, connect.TlsHandshakeCompleted, timings.RequestReady, timings.FirstByteReceived, timings.Completed];
        for (int index = 1; index < ordered.Length; index++)
        {
            Assert.IsGreaterThan(ordered[index - 1]!.Value, ordered[index]!.Value, $"time {index}");
        }
    }

    [TestMethod]
    [DataRow("sftp", DisplayName = "sftp")]
    [DataRow("scp", DisplayName = "scp")]
    public async Task ExecuteAsync_Download_ReportsTheBytesAsTheDownloadSize(string scheme)
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/big.bin"] = new byte[100_000];

        TransferResult result = await RunAsync(server, $"{scheme}://files.example/data/big.bin");

        Assert.AreEqual(100_000, result.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpMissingFile_StillReportsTheConnectAndTotalTimes()
    {
        InMemorySshServer server = new(User, Password);

        TransferResult result = await RunAsync(server, "sftp://files.example/data/missing.txt");

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
        TransferTimings timings = result.Report!.Timings!;
        Assert.IsGreaterThan(timings.Started, timings.Connect!.Connected!.Value);
        Assert.IsGreaterThan(timings.Connect.Connected!.Value, timings.Completed);
        Assert.IsNull(timings.RequestReady);
        Assert.IsNull(timings.FirstByteReceived);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailsAfterTheFirstByte_KeepsThePretransferTime()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();
        TickingClock clock = new();

        TransferResult result = await RunAsync(server, new TimedConnector(server, clock), clock, "sftp://files.example/data/hello.txt", maxFileSize: 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        TransferTimings timings = result.Report!.Timings!;
        Assert.IsGreaterThan(timings.RequestReady!.Value, timings.FirstByteReceived!.Value);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectorRecordsNoTimes_StillReportsAppConnectAndTotal()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello"u8.ToArray();
        TickingClock clock = new();

        TransferResult result = await RunAsync(server, server, clock, "sftp://files.example/data/hello.txt");

        TransferTimings timings = result.Report!.Timings!;
        Assert.IsNull(timings.Connect!.Connected);
        Assert.IsGreaterThan(timings.Started, timings.Connect.TlsHandshakeCompleted!.Value);
        Assert.IsGreaterThan(timings.Started, timings.Completed);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReportsTheTotalTime()
    {
        TickingClock clock = new();

        TransferResult result = await RunAsync(new InMemorySshServer(User, Password), new RefusingConnector(), clock, "sftp://files.example/data/hello.txt");

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        TransferTimings timings = result.Report!.Timings!;
        Assert.IsNull(timings.Connect!.TlsHandshakeCompleted);
        Assert.IsNull(timings.RequestReady);
        Assert.IsGreaterThan(timings.Started, timings.Completed);
    }

    private static Task<TransferResult> RunAsync(InMemorySshServer server, string url)
    {
        TickingClock clock = new();
        return RunAsync(server, new TimedConnector(server, clock), clock, url);
    }

    private static async Task<TransferResult> RunAsync(InMemorySshServer server, IConnector connector, TimeProvider clock, string url, long? maxFileSize = null)
    {
        SshProtocolHandler handler = new(
            connector,
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(User, Password),
            TimeProvider = clock,
            MaxFileSize = maxFileSize,
        });
        await server.WhenSessionsEndAsync();
        return result;
    }

    // A clock one millisecond further on at every reading, so each recorded time is later
    // than the one before it.
    private sealed class TickingClock : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Add(ref now, TimeSpan.TicksPerMillisecond);
    }

    // The server's connection, with the name lookup and connect times a socket connector
    // records.
    private sealed class TimedConnector(InMemorySshServer server, TimeProvider clock) : IConnector
    {
        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            long started = clock.GetTimestamp();
            long resolved = clock.GetTimestamp();
            ConnectResult connected = await server.ConnectAsync(target, cancellationToken);
            return ConnectResult.Connected(connected.Connection!, new ConnectTimings(started, resolved, clock.GetTimestamp(), null));
        }
    }

    // A connector whose every connect is refused, with no times recorded.
    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Refused("Failed to connect", null));
    }
}
