// Runs every upstream tests/data case of one curl release through Curl in process, with the
// conformance harness of Curl.Conformance.UnitLibrary (ADR-0013, ADR-0420), and writes each
// case's raw outcome as JSON - the gap analysis office's "behaviour" area (ADR-0433 decisions 2
// and 9, BL-1728).
//
// Usage: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <tests/data folder> <out.json> [cases]
//
// Inputs:
//   <tests/data folder>  a release's tests/data, as Gap/Tools/Get-UpstreamRelease.ps1 caches it;
//                        every file named test<N> in it is a case.
//   <out.json>           the file to write; its folder must exist.
//   [cases]              optional comma-separated case numbers, e.g. 1,2,3, to run only those.
//
// Output, one JSON object, cases in test-number order:
//   { "release": "<tests/data path>", "platform": "Windows" | "Unix", "commit": "<git HEAD>",
//     "startedAt", "finishedAt",
//     "cases": [ { "number", "kind": "Passed" | "Failed" | "Skipped", "detail", "milliseconds" } ] }
// "detail" is the first difference of a failed case, the harness's reason for a skipped one, and
// empty for a passed one.
//
// Wiring, copied from Curl.Conformance.UnitTests/UpstreamConformanceTests.cs: curl runs in
// process through Curl.Console's public InProcessCurl (BL-1750) over TcpConnector with its dial
// and name lookup replaced (copies of that project's InMemoryServerTcpDialer and
// LoopbackOnlyDnsResolver, below), with the case's <client><setenv> as the whole environment
// Curl reads (BL-2016), the platform is Windows or Unix
// by OS, the runner's time limit is 20 seconds and a case still running after 30 seconds is
// judged failed. Cases run in parallel, up to Environment.ProcessorCount at once, each with its
// own %LOGDIR under <out.json's folder>/upstream-case-logs, which must hold no blank (the runner
// throws on one, as an unquoted %LOGDIR would split); each case's log folder is deleted afterwards.
// Curl's working folder during the run is that log folder, so a case's relative output file
// (upstream runs curl from tests/) is deleted with it.
//
// Host variables (BL-1839): the runner supplies %HOSTIP, %HTTPPORT, %LOGDIR and the like, but not
// the three that name the machine, so this tool replaces them in each case's bytes before the
// runner reads it, with the values upstream's runtests.pl (servers.pm subvariables) gives them:
//   %SRCDIR  the release's tests folder (the parent of <tests/data folder>), forward slashes;
//   %PWD     the folder runtests.pl runs from, which is that same tests folder, so %PWD/../docs
//            is the release's docs folder; "%PWD/%LOGDIR" is first reduced to "%LOGDIR", because
//            upstream's %LOGDIR is relative ("log") and here it is already absolute;
//            when the tests folder's path holds a blank (a cache under C:\Users\<first last>),
//            the release is first copied to <out.json's folder>/upstream-release and both name
//            the copy, since an unquoted blank splits the command line (GF-0044);
//   %PERL    the perl found on PATH, or else the one of the Git for Windows that provides git
//            (<git root>/usr/bin/perl.exe), by an absolute path with forward slashes. With no perl
//            %PERL stays as written, so the runner skips the case for it and the converter
//            (ConvertTo-BehaviourMeasurement.ps1) files it as unmeasured, no-perl.
// "--self-test" instead checks these substitutions and prints a PASS or FAIL line per check.
//
// Known limit: UpstreamCaseRunner.CurlVersion is the constant "8.21.0", so %VERSION in a newer
// release's cases is substituted with 8.21.0 until retargeting (BL-1748) changes it.
#:project ../../Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj
#:project ../../Curl.Console/Curl.Console.csproj
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Curl.Conformance;
using Curl.Console;
using Curl.Networking;
using Curl.Protocol.Abstractions;

if (IsSelfTestRequest(args))
{
    return RunSelfTest();
}

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <tests/data folder> <out.json> [comma-separated case numbers]");
    return 2;
}

TimeSpan timeLimit = TimeSpan.FromSeconds(20);
TimeSpan caseHangLimit = TimeSpan.FromSeconds(30);

string testDataFolder = Path.GetFullPath(args[0]);
string outputPath = Path.GetFullPath(args[1]);
if (!Directory.Exists(testDataFolder))
{
    Console.Error.WriteLine($"No tests/data folder at {testDataFolder}");
    return 2;
}

string logFolder = Path.Combine(Path.GetDirectoryName(outputPath)!, "upstream-case-logs");
if (logFolder.Contains(' ', StringComparison.Ordinal))
{
    Console.Error.WriteLine($"The log folder {logFolder} holds a blank, which the harness refuses; write the output to a folder without one.");
    return 2;
}

SortedSet<int> caseNumbers = new(
    Directory.GetFiles(testDataFolder, "test*")
        .Select(Path.GetFileName)
        .Select(name => int.TryParse(name!["test".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : -1)
        .Where(number => number >= 0));
if (args.Length == 3)
{
    HashSet<int> wanted = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(text => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture))
        .ToHashSet();
    caseNumbers.IntersectWith(wanted);
}

UpstreamCurlPlatform platform = OperatingSystem.IsWindows() ? UpstreamCurlPlatform.Windows : UpstreamCurlPlatform.Unix;
DateTimeOffset startedAt = DateTimeOffset.Now;
Directory.CreateDirectory(logFolder);
string commit = ReadCommit();
string releaseCopy = Path.Combine(Path.GetDirectoryName(outputPath)!, "upstream-release");
string testsFolder = NameTestsFolder((Path.GetDirectoryName(testDataFolder.TrimEnd('\\', '/')) ?? testDataFolder).Replace('\\', '/'), releaseCopy);
string? perl = FindPerl();

// Some cases name a file relative to curl's working folder (upstream runs them from tests/,
// e.g. "-o %"); run them all from the log folder so such files go where the run deletes them,
// never into the checkout the app was started from.
string startingFolder = Environment.CurrentDirectory;
Environment.CurrentDirectory = logFolder;
var results = new (int Number, UpstreamCaseOutcome Outcome, long Milliseconds)[caseNumbers.Count];
int[] ordered = [.. caseNumbers];
int finished = 0;

await Parallel.ForEachAsync(
    Enumerable.Range(0, ordered.Length),
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    async (index, _) =>
    {
        int number = ordered[index];
        byte[] testFile = SubstituteHostVariables(await File.ReadAllBytesAsync(Path.Combine(testDataFolder, $"test{number}")), testsFolder, perl);
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(logFolder, $"test{number}-{Guid.NewGuid():N}"));
        Stopwatch stopwatch = Stopwatch.StartNew();
        UpstreamCaseOutcome outcome = await RunCaseAsync(number, testFile, logDirectory, platform, timeLimit, caseHangLimit);
        results[index] = (number, outcome, stopwatch.ElapsedMilliseconds);
        int done = Interlocked.Increment(ref finished);
        if (done % 100 == 0 || done == ordered.Length)
        {
            Console.Error.WriteLine($"{done}/{ordered.Length} cases run");
        }
    });

Environment.CurrentDirectory = startingFolder;
DeleteLogDirectory(new DirectoryInfo(logFolder));
DeleteLogDirectory(new DirectoryInfo(releaseCopy));
DateTimeOffset finishedAt = DateTimeOffset.Now;

await using (FileStream output = File.Create(outputPath))
await using (Utf8JsonWriter json = new(output, new JsonWriterOptions { Indented = true }))
{
    json.WriteStartObject();
    json.WriteString("release", testDataFolder);
    json.WriteString("platform", OperatingSystem.IsWindows() ? "Windows" : "Unix");
    json.WriteString("commit", commit);
    json.WriteString("startedAt", startedAt);
    json.WriteString("finishedAt", finishedAt);
    json.WriteStartArray("cases");
    foreach ((int number, UpstreamCaseOutcome outcome, long milliseconds) in results)
    {
        json.WriteStartObject();
        json.WriteNumber("number", number);
        json.WriteString("kind", outcome.Kind.ToString());
        json.WriteString("detail", outcome.Detail);
        json.WriteNumber("milliseconds", milliseconds);
        json.WriteEndObject();
    }

    json.WriteEndArray();
    json.WriteEndObject();
}

Console.WriteLine(string.Join(", ", results.GroupBy(result => result.Outcome.Kind).OrderBy(group => group.Key).Select(group => $"{group.Key} {group.Count()}")));
return 0;

// Curl as UpstreamConformanceTests.RunCurlAsync runs it: every TCP dial reaches the case's in-memory
// servers through TcpConnector, names resolve as on upstream's test machine, the case's <setenv> is
// the whole environment Curl reads (BL-2016), and an active-mode FTP case's listener takes the data
// connection (BL-1978).
static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation)
{
    Func<string, string?> readEnvironmentVariable = name => invocation.EnvironmentVariables.GetValueOrDefault(name);
    InMemoryServerTcpDialer dialer = new(invocation.Connector);
    LoopbackOnlyDnsResolver resolver = new();
    return invocation.ConnectionListener is { } ftpListener
        ? InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, dialer, resolver, invocation.DatagramConnector, readEnvironmentVariable, ftpListener)
        : InProcessCurl.RunAsync(invocation.Arguments, invocation.StandardOutput, invocation.StandardError, invocation.StandardInput, dialer, resolver, invocation.DatagramConnector, readEnvironmentVariable);
}

// Runs one case in its own log folder, which is deleted afterwards, and judges a hang or a throw failed.
static async Task<UpstreamCaseOutcome> RunCaseAsync(int number, byte[] testFile, DirectoryInfo logDirectory, UpstreamCurlPlatform platform, TimeSpan timeLimit, TimeSpan caseHangLimit)
{
    try
    {
        UpstreamCaseRunner runner = new(RunCurlAsync, platform, TimeProvider.System, timeLimit);
        return await Task.Run(() => runner.RunAsync(number, testFile, logDirectory.FullName)).WaitAsync(caseHangLimit);
    }
    catch (TimeoutException)
    {
        return UpstreamCaseOutcome.Failed($"the case did not finish within {caseHangLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
    }
    catch (Exception exception) when (exception is not OutOfMemoryException)
    {
        return UpstreamCaseOutcome.Failed($"the harness threw {exception.GetType().Name}: {exception.Message}");
    }
    finally
    {
        DeleteLogDirectory(logDirectory);
    }
}

// Replaces %SRCDIR, %PWD and %PERL in a case's bytes (one character per byte, so binary data
// survives); a null perl leaves %PERL as written.
static byte[] SubstituteHostVariables(byte[] testFile, string testsFolder, string? perl)
{
    string text = System.Text.Encoding.Latin1.GetString(testFile);
    if (!text.Contains('%', StringComparison.Ordinal))
    {
        return testFile;
    }

    text = text.Replace("%PWD/%LOGDIR", "%LOGDIR", StringComparison.Ordinal)
        .Replace("%SRCDIR", testsFolder, StringComparison.Ordinal)
        .Replace("%PWD", testsFolder, StringComparison.Ordinal);
    if (perl is not null)
    {
        text = text.Replace("%PERL", perl, StringComparison.Ordinal);
    }

    return System.Text.Encoding.Latin1.GetBytes(text);
}

// A perl on PATH, else the perl of the Git for Windows that provides git; null when there is none.
static string? FindPerl()
{
    string[] names = OperatingSystem.IsWindows() ? ["perl.exe"] : ["perl"];
    List<string> candidates = [.. (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .SelectMany(folder => names.Select(name => Path.Combine(folder.Trim('"'), name)))];
    if (OperatingSystem.IsWindows())
    {
        foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(folder.Trim('"'), "git.exe")))
            {
                // <git root>\cmd\git.exe or <git root>\bin\git.exe or <git root>\mingw64\bin\git.exe
                for (DirectoryInfo? root = new DirectoryInfo(folder.Trim('"')).Parent; root is not null; root = root.Parent)
                {
                    candidates.Add(Path.Combine(root.FullName, "usr", "bin", "perl.exe"));
                }
            }
        }
    }

    string? found = candidates.FirstOrDefault(File.Exists);
    return found is null ? null : Path.GetFullPath(found).Replace('\\', '/');
}

static bool IsSelfTestRequest(string[] arguments) => arguments is ["--self-test"];

static int RunSelfTest()
{
    bool failed = false;
    void Check(bool passed, string check)
    {
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {check}");
        failed |= !passed;
    }

    string Substitute(string line, string? perl) =>
        System.Text.Encoding.Latin1.GetString(SubstituteHostVariables(System.Text.Encoding.Latin1.GetBytes(line), "/rel/tests", perl));

    Check(Substitute("%SRCDIR/data/data-xml1 %SRCDIR/..", null) == "/rel/tests/data/data-xml1 /rel/tests/..", "%SRCDIR is the tests folder, wherever it stands");
    Check(Substitute("%PWD/../docs/ %FILE_PWD", null) == "/rel/tests/../docs/ %FILE_PWD", "%PWD is the tests folder and %FILE_PWD is left to the runner");
    Check(Substitute("--proto-default file %PWD/%LOGDIR/test%TESTNUMBER.txt", null) == "--proto-default file %LOGDIR/test%TESTNUMBER.txt", "%PWD/%LOGDIR is reduced to the absolute %LOGDIR");
    Check(Substitute("%PERL %SRCDIR/x.pl", "/usr/bin/perl") == "/usr/bin/perl /rel/tests/x.pl", "%PERL is the perl found");
    Check(Substitute("%PERL -e 1", null) == "%PERL -e 1", "with no perl, %PERL stays as written for the runner to skip");
    Check(Substitute("http://%HOSTIP:%HTTPPORT/1 100%", "/usr/bin/perl") == "http://%HOSTIP:%HTTPPORT/1 100%", "other variables and a lone percent are untouched");
    return failed ? 1 : 0;
}

// The commit of the checkout the app runs from, or "unknown" where git cannot tell.
static string ReadCommit()
{
    try
    {
        using Process git = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Environment.CurrentDirectory,
        })!;
        string commit = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return git.ExitCode == 0 && commit.Length > 0 ? commit : "unknown";
    }
    catch (System.ComponentModel.Win32Exception)
    {
        return "unknown";
    }
}

// The tests folder %PWD and %SRCDIR name. An unquoted one holding a blank splits the command
// line (test3009, GF-0044), so then the release is copied to releaseCopy, which holds no blank
// when the log folder beside it holds none, and the copy's tests folder is named instead.
static string NameTestsFolder(string testsFolder, string releaseCopy)
{
    if (!testsFolder.Contains(' ', StringComparison.Ordinal))
    {
        return testsFolder;
    }

    CopyFolder(Path.GetDirectoryName(testsFolder)!, releaseCopy);
    return Path.Combine(releaseCopy, Path.GetFileName(testsFolder)).Replace('\\', '/');
}

// Copies a folder and everything under it, replacing what is already there.
static void CopyFolder(string source, string destination)
{
    foreach (string folder in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, folder)));
    }

    Directory.CreateDirectory(destination);
    foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
    }
}

// A run that timed out may still hold a file open; the folder is left to the system then.
static void DeleteLogDirectory(DirectoryInfo logDirectory)
{
    try
    {
        logDirectory.Delete(recursive: true);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

// A copy of Curl.Conformance.UnitTests' InMemoryServerTcpDialer (internal there): dials every TCP
// connection TcpConnector opens into the case's in-memory server, the unspecified address failing
// to connect (test1293) and a refused connect reaching TcpConnector as the system reports one.
internal sealed class InMemoryServerTcpDialer(IConnector server) : ITcpDialer
{
    public async ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        if (endPoint.Address.Equals(IPAddress.Any) || endPoint.Address.Equals(IPAddress.IPv6Any))
        {
            throw new SocketException((int)SocketError.AddressNotAvailable);
        }

        ConnectResult connected = await server.ConnectAsync(new ConnectTarget(endPoint.Address.ToString(), endPoint.Port, false), cancellationToken);
        if (connected.IsConnectionRefused)
        {
            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        IConnection connection = connected.Connection
            ?? throw new IOException($"The in-memory server refused the connection: {connected.ErrorMessage}");
        return new DialedTcpConnection(connection, connection.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0));
    }

    public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
        DialAsync(endPoint, cancellationToken);

    public async ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken)
    {
        ConnectResult connected = await server.ConnectAsync(new ConnectTarget(address.Path, 1, false), cancellationToken);
        return connected.Connection
            ?? throw new IOException($"The in-memory server refused the connection: {connected.ErrorMessage}");
    }
}

// A copy of Curl.Conformance.UnitTests' LoopbackOnlyDnsResolver (internal there): an address literal
// is itself, localhost and every name under .localhost are ::1 and 127.0.0.1, ip6-localhost is ::1,
// and every other name does not resolve.
internal sealed class LoopbackOnlyDnsResolver : IDnsResolver
{
    public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        IReadOnlyList<IPAddress> addresses =
            IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? literal) ? [literal]
            : string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ? [IPAddress.IPv6Loopback, IPAddress.Loopback]
            : string.Equals(host, "ip6-localhost", StringComparison.OrdinalIgnoreCase) ? [IPAddress.IPv6Loopback]
            : [];
        return ValueTask.FromResult(addresses);
    }
}
