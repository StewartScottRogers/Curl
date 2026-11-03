using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Processes;

/// <summary>Runs curl in process for a scheme that starts a program, with the stand-in starter a case gives it (ADR-1203).</summary>
internal static class ProcessTransferRun
{
    /// <summary>The <c>-w</c> format every write-out case prints: curl 8.22.0's manual, "-w, --write-out".</summary>
    public const string WriteOut = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}";

    /// <summary>The <c>-m</c> value of every timeout case, in seconds.</summary>
    public const int MaxTimeSeconds = 100;

    /// <summary>Runs <paramref name="arguments" /> under <c>-sS</c> on a clock that never moves, so <c>%{time_total}</c> is 0.</summary>
    public static Task<CurlRunResult> RunAsync(IExtensionProcessStarter starter, IReadOnlyDictionary<string, string>? environment, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [], ["-sS", .. arguments], starter, environment, new ManualClock());

    /// <summary>Runs <paramref name="url" /> under <c>-m</c> against a program that never answers, advancing the injected clock a second at a time once curl waits for it.</summary>
    public static async Task<CurlRunResult> RunUntilMaxTimeAsync(SilentProcessStarter starter, IReadOnlyDictionary<string, string>? environment, string url, CancellationToken cancellationToken)
    {
        ManualClock clock = new();
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [], ["-sS", "-m", MaxTimeSeconds.ToString(CultureInfo.InvariantCulture), url], starter, environment, clock);
        await starter.Reading.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        // Until the run ends, not for a fixed number of seconds: curl may reach a clock timer only after the deadline (BL-4663).
        return await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance, cancellationToken);
    }

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>A temporary directory a case writes <c>-o</c> and <c>-O</c> files into, deleted with everything in it.</summary>
    internal sealed class OutputDirectory : IDisposable
    {
        public string FullName { get; } = Directory.CreateTempSubdirectory("curl-transfer-").FullName;

        public string PathOf(string fileName) => Path.Combine(FullName, fileName);

        public void Dispose() => Directory.Delete(FullName, recursive: true);
    }
}
