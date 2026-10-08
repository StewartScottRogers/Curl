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
// process through Curl.Console's public InProcessCurl (BL-1750), the platform is Windows or Unix
// by OS, the runner's time limit is 20 seconds and a case still running after 30 seconds is
// judged failed. Cases run in parallel, up to Environment.ProcessorCount at once, each with its
// own %LOGDIR under <out.json's folder>/upstream-case-logs, which must hold no blank (the runner
// throws on one, as an unquoted %LOGDIR would split); each case's log folder is deleted afterwards.
// Curl's working folder during the run is that log folder, so a case's relative output file
// (upstream runs curl from tests/) is deleted with it.
//
// Known limit: UpstreamCaseRunner.CurlVersion is the constant "8.21.0", so %VERSION in a newer
// release's cases is substituted with 8.21.0 until retargeting (BL-1748) changes it.
#:project ../../Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj
#:project ../../Curl.Console/Curl.Console.csproj
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Curl.Conformance;
using Curl.Console;

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
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(testDataFolder, $"test{number}"));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(logFolder, $"test{number}-{Guid.NewGuid():N}"));
        Stopwatch stopwatch = Stopwatch.StartNew();
        UpstreamCaseOutcome outcome;
        try
        {
            UpstreamCaseRunner runner = new(RunCurlAsync, platform, TimeProvider.System, timeLimit);
            outcome = await Task.Run(() => runner.RunAsync(number, testFile, logDirectory.FullName)).WaitAsync(caseHangLimit);
        }
        catch (TimeoutException)
        {
            outcome = UpstreamCaseOutcome.Failed($"the case did not finish within {caseHangLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            outcome = UpstreamCaseOutcome.Failed($"the harness threw {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            DeleteLogDirectory(logDirectory);
        }

        results[index] = (number, outcome, stopwatch.ElapsedMilliseconds);
        int done = Interlocked.Increment(ref finished);
        if (done % 100 == 0 || done == ordered.Length)
        {
            Console.Error.WriteLine($"{done}/{ordered.Length} cases run");
        }
    });

Environment.CurrentDirectory = startingFolder;
DeleteLogDirectory(new DirectoryInfo(logFolder));
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

static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation) =>
    InProcessCurl.RunAsync(
        invocation.Arguments,
        invocation.StandardOutput,
        invocation.StandardError,
        invocation.StandardInput,
        invocation.Connector,
        invocation.DatagramConnector);

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
