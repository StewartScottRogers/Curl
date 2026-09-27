using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.21.0's refusal, at transfer setup rather than while reading the command line, of a
/// <c>-d</c> / <c>--json</c> body to be posted with <c>-I</c> (HEAD) or <c>--no-head</c> (GET), measured
/// with <c>curl &lt;args&gt; http://127.0.0.1:1/ -o /dev/null</c> (mingw, Schannel) on 2026-09-27: the
/// two warning lines, nothing else, and exit 2, whatever the option order. No test opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRequestMethodConflictTests
{
    private const string Url = "http://127.0.0.1:1/";

    private static readonly string PostAndHeadLines =
        "Warning: You can only select one HTTP request method! You asked for both POST " + Environment.NewLine
        + "Warning: (-d, --data) and HEAD (-I, --head)." + Environment.NewLine;

    private static readonly string PostAndGetLines =
        "Warning: You can only select one HTTP request method! You asked for both POST " + Environment.NewLine
        + "Warning: (-d, --data) and GET (-G, --get)." + Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private int dispatchesCreated;

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.Failing(
        "http", CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 1");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow(new[] { "-I", "-d", "x" })]
    [DataRow(new[] { "-d", "x", "-I" })]
    [DataRow(new[] { "-I", "--json", "x" })]
    [DataRow(new[] { "-I", "-d", "x", "-G", "--no-get" })]
    public async Task RunAsync_HeadWithPostBody_PrintsThePostAndHeadLinesAndExitsFailedInit(string[] options)
    {
        int exitCode = await RunAsync([.. options, Url]);

        AssertRefused(exitCode, PostAndHeadLines);
    }

    [TestMethod]
    [DataRow(new[] { "--no-head", "-d", "x" })]
    [DataRow(new[] { "-d", "x", "--no-head" })]
    public async Task RunAsync_NoHeadWithPostBody_PrintsThePostAndGetLinesAndExitsFailedInit(string[] options)
    {
        int exitCode = await RunAsync([.. options, Url]);

        AssertRefused(exitCode, PostAndGetLines);
    }

    [TestMethod]
    [DataRow(new[] { "-s", "-I", "-d", "x" })]
    [DataRow(new[] { "-I", "-d", "x", "-s" })]
    [DataRow(new[] { "-s", "-S", "-I", "-d", "x" })]
    public async Task RunAsync_SilentHeadWithPostBody_PrintsNothingAndExitsFailedInit(string[] options)
    {
        int exitCode = await RunAsync([.. options, Url]);

        AssertRefused(exitCode, string.Empty);
    }

    [TestMethod]
    [DataRow(new[] { "-I", "-G", "-d", "x" })]
    [DataRow(new[] { "--no-head", "-G", "-d", "x" })]
    public async Task RunAsync_BodyInQueryWithHeadOrNoHead_RunsTheTransfer(string[] options)
    {
        int exitCode = await RunAsync([.. options, Url]);

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Assert.HasCount(1, http.Contexts);
        Assert.DoesNotContain("You can only select one HTTP request method", StandardErrorText);
    }

    [TestMethod]
    [DataRow(new[] { "-I" })]
    [DataRow(new[] { "--no-head" })]
    [DataRow(new[] { "-d", "x" })]
    public async Task RunAsync_HeadOrNoHeadOrBodyAlone_RunsTheTransfer(string[] options)
    {
        int exitCode = await RunAsync([.. options, Url]);

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Assert.HasCount(1, http.Contexts);
    }

    private void AssertRefused(int exitCode, string expectedStandardError)
    {
        Assert.AreEqual((int)CurlExitCode.FailedInit, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(0, dispatchesCreated);
        Assert.IsEmpty(http.Contexts);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
                _ =>
                {
                    dispatchesCreated++;
                    return new TransferDispatch(new ProtocolDispatcher([http]), []);
                },
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);
}
