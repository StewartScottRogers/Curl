using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// Runs one transfer-surface check: curl in process with one of curl's generic transfer options (<c>-v</c>, <c>-o</c>,
/// <c>-O</c>, <c>-w</c>, <c>--fail</c>, <c>-m</c>) against a scheme's canned server, on a clock the check drives, so a
/// scheme family's case file names only its route, URL and server.
/// </summary>
internal static class TransferSurfaceCaseRun
{
    /// <summary>The <c>-w</c> format every write-out check uses: each variable the matrix names, separated by <c>|</c>.</summary>
    internal const string WriteOutFormat =
        "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}";

    private static readonly InProcessCurlRunner CurlRunner = InProcessCurl.RunAsync;

    /// <summary>Gets the known failures every transfer-surface case runs under.</summary>
    internal static KnownFailures Known { get; } = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// Runs curl with <paramref name="arguments" /> (and <c>-k</c> first on a TLS route) against a server
    /// <paramref name="route" /> starts, on a clock that never moves, so <c>%{time_total}</c> is zero.
    /// </summary>
    internal static async Task<CurlRunResult> RunAsync(TransferSurfaceRoute route, params string[] arguments)
    {
        using TransferSurfaceRouteInstance instance = route.Start();
        return await RunAsync(instance, arguments, new ManualClock());
    }

    /// <summary>
    /// Runs curl with <c>-m <paramref name="seconds" /></c> and <paramref name="url" /> against a server that reads
    /// curl's first bytes and never answers, then moves the clock past the deadline once those bytes arrive.
    /// </summary>
    internal static async Task<CurlRunResult> RunPastMaxTimeAsync(TransferSurfaceRoute route, int seconds, string url, CancellationToken cancellationToken)
    {
        SilentServer server = new();
        ManualClock clock = new();
        using TransferSurfaceRouteInstance instance = route.Start(server);
        Task<CurlRunResult> run = RunAsync(instance, ["-sS", "-m", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), url], clock);
        await server.FirstByteReceived.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        // Advance until the run ends, not for -m + 1 seconds: curl may reach a clock timer only after the clock has
        // passed the deadline (BL-4663, MaxTimeDeadlineRun's remarks).
        return await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance, cancellationToken);
    }

    /// <summary>Checks the run's exit code, standard output and standard error, naming <paramref name="requirement" /> and the expected value's source.</summary>
    internal static void AssertRun(CurlRunResult result, string requirement, string source, int exitCode, string standardOutput, string standardError)
    {
        string because = $"{requirement} ({source})";
        Assert.AreEqual(Lf(standardError), Lf(Encoding.UTF8.GetString(result.StandardError)), $"{because}: standard error");
        Assert.AreEqual(exitCode, result.ExitCode, $"{because}: exit code");
        Assert.AreEqual(standardOutput, Encoding.UTF8.GetString(result.StandardOutput), $"{because}: standard output");
    }

    /// <summary>Checks a <c>-v</c> run: exit code and standard output, and that standard error has a line starting with each of <paramref name="verboseLinePrefixes" />, in order.</summary>
    internal static void AssertVerboseRun(CurlRunResult result, string requirement, string source, string standardOutput, params string[] verboseLinePrefixes)
    {
        string because = $"{requirement} ({source})";
        string[] errorLines = Lf(Encoding.UTF8.GetString(result.StandardError)).Split('\n');
        Assert.AreEqual(0, result.ExitCode, $"{because}: exit code\n{string.Join("\n", errorLines)}");
        Assert.AreEqual(standardOutput, Encoding.UTF8.GetString(result.StandardOutput), $"{because}: standard output");
        int next = 0;
        foreach (string prefix in verboseLinePrefixes)
        {
            int found = Array.FindIndex(errorLines, next, line => line.StartsWith(prefix, StringComparison.Ordinal));
            Assert.IsGreaterThanOrEqualTo(0, found, $"{because}: standard error has no line starting \"{prefix}\" after line {next}:\n{string.Join("\n", errorLines)}");
            next = found + 1;
        }
    }

    /// <summary>A new, empty temporary directory the caller deletes.</summary>
    internal static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-transfer-surface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static Task<CurlRunResult> RunAsync(TransferSurfaceRouteInstance instance, string[] arguments, TimeProvider clock) =>
        ExtendedConformanceHarness.RunAsync(CurlRunner, [instance.Route], [.. instance.LeadingArguments, .. arguments], timeProvider: clock);

    /// <summary>A server that reads everything curl sends and never sends a byte.</summary>
    private sealed class SilentServer : IReferenceServer
    {
        private readonly TaskCompletionSource firstByteReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstByteReceived => firstByteReceived.Task;

        public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[256];
            while (await connection.ReadAsync(buffer, cancellationToken) > 0)
            {
                firstByteReceived.TrySetResult();
            }
        }
    }

    /// <summary>A clock that moves only when the check advances it, firing every timer that falls due.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly Lock gate = new();
        private readonly List<ManualTimer> timers = [];
        private long elapsedTicks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            lock (gate)
            {
                return elapsedTicks;
            }
        }

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ManualTimer timer = new(this, callback, state);
            lock (gate)
            {
                timers.Add(timer);
            }

            timer.Change(dueTime, period);
            return timer;
        }

        /// <summary>Moves the clock forward by <paramref name="by" />, firing each timer due by then, earliest first.</summary>
        public void Advance(TimeSpan by)
        {
            long target;
            lock (gate)
            {
                target = elapsedTicks + by.Ticks;
            }

            while (NextDue(target) is { } due)
            {
                lock (gate)
                {
                    elapsedTicks = Math.Max(elapsedTicks, due.DueAt);
                }

                due.Fire();
            }

            lock (gate)
            {
                elapsedTicks = target;
            }
        }

        private ManualTimer? NextDue(long target)
        {
            lock (gate)
            {
                return timers.Where(timer => timer.DueAt <= target).OrderBy(timer => timer.DueAt).FirstOrDefault();
            }
        }

        private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
        {
            private long period = -1;

            public long DueAt { get; private set; } = long.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    DueAt = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.elapsedTicks + dueTime.Ticks;
                    this.period = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? -1 : period.Ticks;
                }

                return true;
            }

            public void Fire()
            {
                lock (owner.gate)
                {
                    DueAt = period < 0 ? long.MaxValue : DueAt + period;
                }

                callback(state);
            }

            public void Dispose()
            {
                lock (owner.gate)
                {
                    owner.timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

/// <summary>
/// A scheme's route to its canned server: TCP, TLS (curl run with <c>-k</c> against a fresh self-signed certificate)
/// or UDP, on one port, with the server each check starts.
/// </summary>
internal sealed class TransferSurfaceRoute
{
    private readonly Func<IReferenceServer?, X509Certificate2?, ReferenceRoute> create;

    private TransferSurfaceRoute(bool isTls, Func<IReferenceServer?, X509Certificate2?, ReferenceRoute> create)
    {
        IsTls = isTls;
        this.create = create;
    }

    /// <summary>Gets a value indicating whether curl reaches the server over TLS.</summary>
    internal bool IsTls { get; }

    /// <summary>A plain TCP route on <paramref name="port" /> to the server <paramref name="server" /> makes.</summary>
    internal static TransferSurfaceRoute Tcp(int port, Func<IReferenceServer> server) =>
        new(false, (replacement, _) => ReferenceRoute.Tcp(port, replacement ?? server()));

    /// <summary>A TLS route on <paramref name="port" /> to the server <paramref name="server" /> makes, curl run with <c>-k</c>.</summary>
    internal static TransferSurfaceRoute Tls(int port, Func<IReferenceServer> server) =>
        new(true, (replacement, certificate) => ReferenceRoute.Tls(port, replacement ?? server(), certificate!));

    /// <summary>A UDP route on <paramref name="port" /> to the datagram server <paramref name="server" /> makes.</summary>
    internal static TransferSurfaceRoute Udp(int port, Func<IDatagramReferenceServer> server) =>
        new(false, (_, _) => ReferenceRoute.Udp(port, server()));

    /// <summary>Starts the route for one run, with <paramref name="replacement" /> in place of the scheme's own server when given.</summary>
    internal TransferSurfaceRouteInstance Start(IReferenceServer? replacement = null)
    {
        X509Certificate2? certificate = IsTls ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        return new TransferSurfaceRouteInstance(create(replacement, certificate), IsTls ? ["-k"] : [], certificate);
    }
}

/// <summary>One run's route and the arguments it puts before the check's own, holding the TLS certificate until disposed.</summary>
internal sealed class TransferSurfaceRouteInstance(ReferenceRoute route, string[] leadingArguments, X509Certificate2? certificate) : IDisposable
{
    /// <summary>Gets the route the harness serves.</summary>
    internal ReferenceRoute Route { get; } = route;

    /// <summary>Gets the arguments curl takes before the check's own: <c>-k</c> on a TLS route.</summary>
    internal string[] LeadingArguments { get; } = leadingArguments;

    /// <inheritdoc />
    public void Dispose() => certificate?.Dispose();
}
