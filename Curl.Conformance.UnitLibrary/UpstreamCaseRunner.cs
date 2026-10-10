using System.Globalization;

namespace Curl.Conformance;

/// <summary>
/// Runs one vendored upstream test case through curl in process, the way <c>runtests.pl</c> at
/// <c>curl-8_21_0</c> runs it through the <c>curl</c> binary (ADR-0013, decision 4): expand the
/// file, skip it with a reason when the harness cannot run it, write its <c>&lt;client&gt;</c>
/// files, run the command against the in-memory <c>sws</c> emulation, and compare what came out
/// with <c>&lt;verify&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The command gets the arguments <c>runtests.pl</c> puts before it: <c>--output
/// %LOGDIR/curl.out</c> unless the command's <c>option</c> says <c>no-output</c> or the case
/// verifies <c>&lt;stdout&gt;</c> without <c>force-output</c>, then <c>--include</c> unless it says
/// <c>no-include</c>. Upstream's <c>--trace-ascii</c>, <c>--trace-config</c> and <c>--trace-time</c>
/// are left out: Curl has no trace output and no case compares the trace (ADR-0029).
/// </para>
/// <para>
/// <c>%LOGDIR</c> is the absolute log directory with forward slashes, so cases can run in
/// parallel, <c>%PWD</c> is the tests directory the caller names, with forward slashes, and <c>%FILE_PWD</c> is empty, so <c>file://localhost%FILE_PWD/%LOGDIR/…</c> still
/// names the file. As upstream does, standard output and standard error are saved to
/// <c>%LOGDIR/stdout%TESTNUMBER</c> and <c>%LOGDIR/stderr%TESTNUMBER</c> before the comparison,
/// for the cases that verify them as files. <c>%include</c> and <c>%includetext</c> read the file
/// they name by its path, relative to the working directory when not absolute, as nothing when it
/// is not there. <c>%HOSTIP</c> and <c>%CLIENTIP</c> are <c>127.0.0.1</c>, <c>%HTTPPORT</c> is
/// <see cref="HttpPort"/>, <c>%PROXYPORT</c> is <see cref="ProxyPort"/>, and <c>%VERSION</c> is <see cref="CurlVersion"/>. Every other variable
/// is unknown, so a case that uses one is skipped.
/// </para>
/// </remarks>
/// <param name="runCurl">Runs curl for one invocation and returns its exit code.</param>
/// <param name="platform">The features and null device of the platform Curl runs on.</param>
/// <param name="timeProvider">Measures <paramref name="timeLimit"/>.</param>
/// <param name="timeLimit">How long a run may take before the case fails.</param>
public sealed class UpstreamCaseRunner(
    Func<UpstreamCurlInvocation, Task<int>> runCurl,
    UpstreamCurlPlatform platform,
    TimeProvider timeProvider,
    TimeSpan timeLimit)
{
    /// <summary>The value of <c>%HTTPPORT</c>; every connection reaches the emulation whatever its port.</summary>
    public const string HttpPort = "8990";

    /// <summary>The value of <c>%PROXYPORT</c>: connections to this port reach the emulation too, recorded apart for <c>&lt;verify&gt;&lt;proxy&gt;</c>.</summary>
    public const string ProxyPort = "8992";

    /// <summary>The value of <c>%VERSION</c>: the curl release Curl matches.</summary>
    public const string CurlVersion = "8.21.0";

    private const string HostAddress = "127.0.0.1";

    private static readonly string[] ClientFileParts = ["file", "file1", "file2", "file3", "file4"];

    // What runtests.pl's open reads: nothing when the file is not there.
    private static readonly Func<string, byte[]?> ReadIncludedFile = path => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>Runs one case.</summary>
    /// <param name="testNumber">The case's number, from its file name.</param>
    /// <param name="testFile">The case's file, as vendored.</param>
    /// <param name="logDirectory">
    /// An empty directory of the case's own, <c>%LOGDIR</c>, by an absolute path with no blank in it;
    /// the caller deletes it.
    /// </param>
    /// <param name="testsDirectory">
    /// The release's <c>tests</c> folder, <c>%PWD</c>, by an absolute path with no blank in it, as
    /// <c>runtests.pl</c>'s working directory; <see langword="null"/> leaves <c>%PWD</c> without a
    /// value, so a case that uses it is skipped.
    /// </param>
    /// <param name="certificateDirectory">
    /// The folder holding upstream's <c>certs</c> folder, <c>%CERTDIR</c> (cases name
    /// <c>%CERTDIR/certs/test-ca.crt</c>), by an absolute path with no blank in it;
    /// <see langword="null"/> leaves <c>%CERTDIR</c> without a value, so a case that uses it is skipped.
    /// </param>
    /// <returns>Whether the case passed, failed with its first difference, or was skipped with its reason.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="logDirectory"/>, <paramref name="testsDirectory"/> or
    /// <paramref name="certificateDirectory"/> holds a blank, which would split every command that
    /// names <c>%LOGDIR</c>, <c>%PWD</c> or <c>%CERTDIR</c> unquoted (test3009's
    /// <c>--output-dir %PWD/not-there</c>, GF-0044), as upstream's relative <c>log/</c> never does.
    /// </exception>
    public async Task<UpstreamCaseOutcome> RunAsync(
        int testNumber, ReadOnlyMemory<byte> testFile, string logDirectory, string? testsDirectory = null, string? certificateDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(logDirectory);
        RefuseABlank(logDirectory, "log directory", nameof(logDirectory));
        RefuseABlank(testsDirectory ?? string.Empty, "tests directory", nameof(testsDirectory));
        RefuseABlank(certificateDirectory ?? string.Empty, "certificate directory", nameof(certificateDirectory));

        string logDirectoryVariable = logDirectory.Replace('\\', '/');
        Dictionary<string, string> variables = Variables(testNumber, logDirectoryVariable);
        AddDirectoryVariable(variables, "PWD", testsDirectory);
        AddDirectoryVariable(variables, "CERTDIR", certificateDirectory);

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(testFile.Span, variables, platform.Features, ReadIncludedFile);
        UpstreamTestCaseParseResult parsed = expansion.Parse();
        if (!parsed.IsParsed)
        {
            return UpstreamCaseOutcome.Skipped(parsed.Failure.Message);
        }

        return (UpstreamCaseScreening.FindSkipReason(expansion, parsed.TestCase, platform.Features)
                ?? UpstreamCaseScreening.FindFileOutsideLogDirectory(parsed.TestCase, logDirectory)) is { } reason
            ? UpstreamCaseOutcome.Skipped(reason)
            : await RunScreenedAsync(parsed.TestCase, testNumber, logDirectoryVariable).ConfigureAwait(false);
    }

    private static void RefuseABlank(string directory, string description, string parameterName)
    {
        if (directory.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"The {description} {directory} holds a blank; upstream's commands name it unquoted.", parameterName);
        }
    }

    private static void AddDirectoryVariable(Dictionary<string, string> variables, string name, string? directory)
    {
        if (directory is not null)
        {
            variables[name] = directory.Replace('\\', '/');
        }
    }

    private Dictionary<string, string> Variables(int testNumber, string logDirectory) =>
        new(StringComparer.Ordinal)
        {
            ["HOSTIP"] = HostAddress,
            ["CLIENTIP"] = HostAddress,
            ["HTTPPORT"] = HttpPort,
            ["PROXYPORT"] = ProxyPort,
            ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture),
            ["LOGDIR"] = logDirectory,
            ["FILE_PWD"] = string.Empty,
            ["VERSION"] = CurlVersion,
            ["DEV_NULL"] = platform.NullDevice,
        };

    private async Task<UpstreamCaseOutcome> RunScreenedAsync(UpstreamTestCase testCase, int testNumber, string logDirectory)
    {
        string outputFile = logDirectory + "/curl.out";
        WriteClientFiles(testCase);
        List<string> arguments = Arguments(testCase, outputFile);

        // On the real clock the server's waits make a case take seconds that a loaded machine
        // stretches past the time limit (test1677's writedelay: 5.5 seconds, BL-1355); with no
        // curl timer to race them, they are skipped.
        SwsHttpServerConnector server = new(testCase, CurlTimerOptions.AnyIn(arguments) ? TimeProvider.System : new WaitSkippingTimeProvider());
        // Not disposed: CurlCommandRunner.RunAsync takes no cancellation token, so a run past the
        // time limit is stopped only at its next exchange with the abandoned server, and may write
        // to these until then. A memory stream holds nothing but its buffer, which the collector takes.
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();
        MemoryStream standardInput = new(StandardInput(testCase));
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, standardInput, server, new UnreachableDatagramConnector());
        (int exitCode, string? failure) = await RunCurlAsync(invocation, server).ConfigureAwait(false);
        if (failure is not null)
        {
            return UpstreamCaseOutcome.Failed(failure);
        }

        File.WriteAllBytes($"{logDirectory}/stdout{testNumber}", standardOutput.ToArray());
        File.WriteAllBytes($"{logDirectory}/stderr{testNumber}", standardError.ToArray());
        UpstreamCaseRun run = new(exitCode, standardOutput.ToArray(), standardError.ToArray(), server.ReceivedBytes.ToArray(), File.Exists(outputFile) ? File.ReadAllBytes(outputFile) : [])
        {
            ProxyReceivedBytes = server.ProxyReceivedBytes.ToArray(),
        };
        return UpstreamCaseVerification.FindFirstDifference(testCase, run) is { } difference
            ? UpstreamCaseOutcome.Failed(difference)
            : UpstreamCaseOutcome.Passed;
    }

    // Any exception curl lets escape is a failure of the case, not of the harness. Curl starts on
    // a thread of its own: against the in-memory server every await can complete at once, so a
    // run that loops would otherwise never return its task, and the time limit would never start.
    // Past the limit the server is abandoned, which stops such a loop at its next exchange.
    // Not the thread pool: a queued work item can wait past the limit on a busy machine
    // (BL-1056), and a looping curl holding a pool thread starves the very continuations that
    // start and fire the limit (BL-1062). On its own thread curl starts at once, and the limit
    // starts here, synchronously, without waiting for the pool.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever curl throws is reported as the case's failure.")]
    private async Task<(int ExitCode, string? Failure)> RunCurlAsync(UpstreamCurlInvocation invocation, SwsHttpServerConnector server)
    {
        Task<int> curlRun = Task.Factory.StartNew(() => runCurl(invocation), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        try
        {
            return (await curlRun.WaitAsync(timeLimit, timeProvider).ConfigureAwait(false), null);
        }
        catch (TimeoutException)
        {
            server.Abandon();
            return (0, $"curl did not finish within {timeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
        }
        catch (Exception exception)
        {
            return (0, $"curl threw {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static List<string> Arguments(UpstreamTestCase testCase, string outputFile)
    {
        UpstreamTestSection command = testCase.Find("client", "command")!;
        string option = command.GetAttribute("option") ?? string.Empty;
        bool writesOutputFile = !option.Contains("no-output", StringComparison.Ordinal)
            && (testCase.Find("verify", "stdout") is null || option.Contains("force-output", StringComparison.Ordinal));
        List<string> arguments = writesOutputFile ? ["--output", outputFile] : [];
        arguments.AddRange(option.Contains("no-include", StringComparison.Ordinal) ? [] : ["--include"]);
        arguments.AddRange(UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).Arguments);
        return arguments;
    }

    private static void WriteClientFiles(UpstreamTestCase testCase)
    {
        foreach (UpstreamTestSection part in ClientFileParts.SelectMany(name => testCase.FindAll("client", name)))
        {
            string path = part.GetAttribute("name")!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, ClientBytes(part));
        }
    }

    private static byte[] StandardInput(UpstreamTestCase testCase) =>
        testCase.Find("client", "stdin") is { } part
            ? ClientBytes(part)
            : [];

    // A <file> or <stdin> part as runtests.pl writes it: the last line chomped under nonewline,
    // then CRLF forced on every line under crlf="yes" or on header lines under crlf="headers".
    private static byte[] ClientBytes(UpstreamTestSection part) =>
        UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part);
}
