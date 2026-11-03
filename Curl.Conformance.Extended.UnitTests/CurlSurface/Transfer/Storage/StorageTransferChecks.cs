using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Storage;

/// <summary>
/// The checks of curl's generic transfer options every storage scheme runs (BL-4615), each through
/// <c>Curl.Console</c> in process against the scheme's reference server over TLS on 443, on a
/// <see cref="StorageTransferClock" /> that moves only when a check advances it. The expected values
/// come from curl's manual page for curl 8.22.0 ("-v", "-o", "-O", "-w", "--fail", "--fail-with-body",
/// "-m", "EXIT CODES") and, for a storage service's error status, from ADR-1090 (s3), ADR-1093 (oci),
/// ADR-1099 (gs) and ADR-1105 (az, abfs, abfss): every status of 300 or more is exit 22.
/// </summary>
internal static class StorageTransferChecks
{
    private const int MaxTimeSeconds = 100;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>Manual, "-v": the connection and the TLS handshake are told on standard error, and standard output holds only the object; the TLS lines are the platform's curl build's.</summary>
    public static Task VerboseAsync(StorageTransferTarget target, string requirementId) =>
        Known.RunCaseAsync([requirementId], async () =>
        {
            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["-v", target.ObjectUrl]);

            string standardError = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, standardError);
            CollectionAssert.AreEqual(target.Body, result.StandardOutput);
            StringAssert.Contains(standardError, $"* Established connection to {target.Host} (127.0.0.1 port {StorageTransferTarget.Port}) from ", standardError);
            StringAssert.Contains(standardError, TlsVerbosePrefix, standardError);
        });

    // curl 8.22.0 tells a connection as "* Established connection to <host> (<address> port <port>) from <local address>
    // port <local port>" (measured for BL-4651); the local port varies, so -v checks only the part before it.
    // curl's TLS lines follow its build: Schannel's ("* schannel: ...") on Windows, OpenSSL's elsewhere.
    private static string TlsVerbosePrefix => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>Manual, "-o": the object goes to the named file and nothing to standard output or standard error.</summary>
    public static Task OutputFileAsync(StorageTransferTarget target, string requirementId) =>
        Known.RunCaseAsync([requirementId], () => InTemporaryDirectoryAsync(async directory =>
        {
            string file = Path.Combine(directory, "out.bin");

            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["-o", file, target.ObjectUrl]);

            AssertQuietSuccess(result);
            CollectionAssert.AreEqual(target.Body, await File.ReadAllBytesAsync(file));
        }));

    /// <summary>Manual, "-O": the object goes to a file named as the URL's last path segment, here in <c>--output-dir</c>.</summary>
    public static Task RemoteNameAsync(StorageTransferTarget target, string requirementId) =>
        Known.RunCaseAsync([requirementId], () => InTemporaryDirectoryAsync(async directory =>
        {
            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["--output-dir", directory, "-O", target.ObjectUrl]);

            AssertQuietSuccess(result);
            CollectionAssert.AreEqual(target.Body, await File.ReadAllBytesAsync(Path.Combine(directory, target.RemoteName)));
        }));

    /// <summary>Manual, "-w": each variable after a successful transfer, <c>%{time_total}</c> read from the clock that never moved.</summary>
    public static Task WriteOutAsync(StorageTransferTarget target, string requirementId, string scheme) =>
        Known.RunCaseAsync([requirementId], () => InTemporaryDirectoryAsync(async directory =>
        {
            const string Format = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}";

            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["-o", Path.Combine(directory, "out.bin"), "-w", Format, target.ObjectUrl]);

            Assert.AreEqual(string.Empty, Text(result.StandardError));
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(
                string.Create(CultureInfo.InvariantCulture, $"0||{target.ObjectUrl}|{target.ObjectUrl}|{scheme}|127.0.0.1|443|{target.Body.Length}|0.000000"),
                Text(result.StandardOutput));
        }));

    /// <summary>Manual, "--fail" and "EXIT CODES" 22: a server error status ends the transfer with exit 22 and writes nothing to standard output.</summary>
    public static Task FailAsync(StorageTransferTarget target, string requirementId) =>
        Known.RunCaseAsync([requirementId], async () =>
        {
            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["--fail", target.MissingUrl]);

            string standardError = Text(result.StandardError);
            Assert.AreEqual(22, result.ExitCode, standardError);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            StringAssert.StartsWith(standardError, "curl: (22) ");
        });

    /// <summary>Manual, "--fail-with-body" and "EXIT CODES" 22: a server error status exits 22, and the error body is written to standard output.</summary>
    public static Task FailWithBodyAsync(StorageTransferTarget target, string requirementId, string errorBodyText) =>
        Known.RunCaseAsync([requirementId], async () =>
        {
            CurlRunResult result = await RunAsync(target, target.CreateServer(), ["--fail-with-body", target.MissingUrl]);

            string standardError = Text(result.StandardError);
            Assert.AreEqual(22, result.ExitCode, standardError);
            StringAssert.Contains(Text(result.StandardOutput), errorBodyText);
            StringAssert.StartsWith(standardError, "curl: (22) ");
        });

    /// <summary>Manual, "-m" and "EXIT CODES" 28: a server that never answers ends the transfer with exit 28 once the clock reaches the deadline.</summary>
    public static Task MaxTimeAsync(StorageTransferTarget target, string requirementId) =>
        Known.RunCaseAsync([requirementId], async () =>
        {
            StorageTransferClock clock = new();
            SilentStorageServer server = new();
            Task<CurlRunResult> run = RunAsync(target, server, ["-m", MaxTimeSeconds.ToString(CultureInfo.InvariantCulture), target.ObjectUrl], clock);
            await server.RequestReceived.WaitAsync(TimeSpan.FromSeconds(30));
            for (int second = 0; second <= MaxTimeSeconds && !run.IsCompleted; second++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                await Task.Yield();
            }

            CurlRunResult result = await run.WaitAsync(TimeSpan.FromSeconds(30));

            string standardError = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, standardError);
            StringAssert.StartsWith(standardError, "curl: (28) ");
            Assert.IsEmpty(result.StandardOutput);
        });

    private static Task<CurlRunResult> RunAsync(StorageTransferTarget target, IReferenceServer server, IReadOnlyList<string> arguments, StorageTransferClock? clock = null) =>
        ExtendedConformanceHarness.RunAsync(
            Curl,
            [ReferenceRoute.Tls(StorageTransferTarget.Port, server, ReferenceCertificate.CreateSelfSigned(target.Host))],
            ["-sS", "-k", .. target.Credentials, .. arguments],
            timeProvider: clock ?? new StorageTransferClock());

    private static void AssertQuietSuccess(CurlRunResult result)
    {
        Assert.AreEqual(string.Empty, Text(result.StandardError));
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsEmpty(result.StandardOutput);
    }

    private static async Task InTemporaryDirectoryAsync(Func<string, Task> check)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory();
        try
        {
            await check(directory.FullName);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
