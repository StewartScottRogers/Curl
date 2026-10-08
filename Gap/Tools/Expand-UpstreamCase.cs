// Expands upstream tests/data cases exactly as the conformance harness does, and writes what an
// out-of-process run of each needs - its command line, its one <data> reply, its standard input
// and its client files - as JSON, for Gap/Tools/Measure-ReferenceCrossCheck.ps1 (ADR-0433
// decision 2, BL-1730). It runs nothing and opens no socket.
//
// Usage: dotnet run --file Gap/Tools/Expand-UpstreamCase.cs -- <tests/data folder> <port> <log root> <out.json> <cases> [Windows|Unix]
//
// Inputs:
//   <tests/data folder>  a release's tests/data, as Gap/Tools/Get-UpstreamRelease.ps1 caches it.
//   <port>               the value of %HTTPPORT.
//   <log root>           an absolute folder with no blank; case N's %LOGDIR is <log root>/<N>,
//                        with forward slashes, as UpstreamCaseRunner passes it.
//   <out.json>           the file to write; its folder must exist.
//   <cases>              comma-separated case numbers, e.g. 1,2,3.
//   [platform]           whose features and %DEV_NULL to expand with; default this machine's.
//
// Output, one JSON object: { "cases": [ { "number", "leftOutBy", "arguments", "response",
// "standardInput", "clientFiles": [ { "path", "content" } ] } ] }, cases in the order given.
// "response", "standardInput" and each "content" are base64. "leftOutBy" is null for a case the
// cross-check runs, else the selection rule that leaves it out:
//   not-parsed            the expanded file does not parse, or names a variable with no value
//   server-not-http-alone <client><server> is not exactly "http"
//   reply-not-one-data    <reply> has no <data>, or has a <dataN> or a <servercmd>
// Variables are those of UpstreamCaseRunner: %HOSTIP and %CLIENTIP 127.0.0.1, %FILE_PWD empty,
// %VERSION UpstreamCaseRunner.CurlVersion. The command line is the runner's: --output
// %LOGDIR/curl.out unless the command's option says no-output or the case verifies stdout
// without force-output, then --include unless the option says no-include, then the split
// command. The reply is <data> as the in-process server sends it: decoded, then nonewline.
#:project ../../Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj
using System.Globalization;
using System.Text.Json;
using Curl.Conformance;

if (args.Length is < 5 or > 6)
{
    Console.Error.WriteLine("Usage: dotnet run --file Gap/Tools/Expand-UpstreamCase.cs -- <tests/data folder> <port> <log root> <out.json> <cases> [Windows|Unix]");
    return 2;
}

string testsData = args[0];
string port = args[1];
string logRoot = Path.GetFullPath(args[2]).Replace('\\', '/');
string outFile = args[3];
int[] numbers = args[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(n => int.Parse(n, CultureInfo.InvariantCulture)).ToArray();
string platformName = args.Length == 6 ? args[5] : (OperatingSystem.IsWindows() ? "Windows" : "Unix");
UpstreamCurlPlatform platform = platformName == "Windows" ? UpstreamCurlPlatform.Windows : UpstreamCurlPlatform.Unix;
string[] clientFileParts = ["file", "file1", "file2", "file3", "file4"];

using FileStream stream = File.Create(outFile);
using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
writer.WriteStartObject();
writer.WriteStartArray("cases");
foreach (int number in numbers)
{
    WriteCase(number);
}

writer.WriteEndArray();
writer.WriteEndObject();
return 0;

void WriteCase(int number)
{
    string logDirectory = $"{logRoot}/{number.ToString(CultureInfo.InvariantCulture)}";
    Dictionary<string, string> variables = new(StringComparer.Ordinal)
    {
        ["HOSTIP"] = "127.0.0.1",
        ["CLIENTIP"] = "127.0.0.1",
        ["HTTPPORT"] = port,
        ["TESTNUMBER"] = number.ToString(CultureInfo.InvariantCulture),
        ["LOGDIR"] = logDirectory,
        ["FILE_PWD"] = string.Empty,
        ["VERSION"] = UpstreamCaseRunner.CurlVersion,
        ["DEV_NULL"] = platform.NullDevice,
    };
    byte[] file = File.ReadAllBytes(Path.Combine(testsData, $"test{number.ToString(CultureInfo.InvariantCulture)}"));
    UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(file, variables, platform.Features, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
    UpstreamTestCaseParseResult parsed = expansion.Parse();
    writer.WriteStartObject();
    writer.WriteNumber("number", number);
    string? leftOutBy = !parsed.IsParsed || expansion.UnknownVariables.Count > 0 ? "not-parsed"
        : !IsHttpAlone(parsed.TestCase) ? "server-not-http-alone"
        : !HasOneData(parsed.TestCase) ? "reply-not-one-data"
        : null;
    if (leftOutBy is not null)
    {
        writer.WriteString("leftOutBy", leftOutBy);
        writer.WriteEndObject();
        return;
    }

    UpstreamTestCase testCase = parsed.TestCase!;
    writer.WriteNull("leftOutBy");
    writer.WriteStartArray("arguments");
    foreach (string argument in Arguments(testCase, logDirectory + "/curl.out"))
    {
        writer.WriteStringValue(argument);
    }

    writer.WriteEndArray();
    UpstreamTestSection data = testCase.Find("reply", "data")!;
    writer.WriteBase64String("response", UpstreamTestPartBodies.WithoutFinalNewline(UpstreamTestPartBodies.Decoded(data), data));
    writer.WriteBase64String("standardInput", testCase.Find("client", "stdin") is { } stdin ? ClientBytes(stdin) : []);
    writer.WriteStartArray("clientFiles");
    foreach (UpstreamTestSection part in clientFileParts.SelectMany(name => testCase.FindAll("client", name)))
    {
        writer.WriteStartObject();
        writer.WriteString("path", part.GetAttribute("name"));
        writer.WriteBase64String("content", ClientBytes(part));
        writer.WriteEndObject();
    }

    writer.WriteEndArray();
    writer.WriteEndObject();
}

static bool IsHttpAlone(UpstreamTestCase testCase)
{
    string[] servers = UpstreamTestPartBodies.Lines(testCase.Find("client", "server"))
        .Select(line => line.Trim().Split(' ', 2)[0]).Where(line => line.Length > 0).ToArray();
    return servers is ["http"];
}

static bool HasOneData(UpstreamTestCase testCase) =>
    testCase.Find("reply", "data") is not null
    && !testCase.Sections.Any(s => s.Section == "reply" && s.Name.Length > 4 && s.Name.StartsWith("data", StringComparison.Ordinal) && s.Name[4..].All(char.IsAsciiDigit))
    && !testCase.Sections.Any(s => s.Name == "servercmd");

static List<string> Arguments(UpstreamTestCase testCase, string outputFile)
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

static byte[] ClientBytes(UpstreamTestSection part) =>
    UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part);
