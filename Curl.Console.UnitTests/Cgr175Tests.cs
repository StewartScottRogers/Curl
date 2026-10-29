using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-175 (BL-3704, ADR-0958): <c>-F f=@-</c> shared by two URLs. Serially the second transfer finds standard
/// input drained and fails with exit 26 before sending, as curl 8.22.0 does; under <c>-Z</c> only the option group's
/// last URL sends the form and every earlier one an empty body.
/// </summary>
[TestClass]
public sealed class Cgr175Tests
{
    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_StandardInputFormForTwoUrls_FailsTheSecondWithAShortRead()
    {
        (int exitCode, string stdout, string stderr, List<long> sent) = await RunAsync(
            "-F", "f=@-", "http://127.0.0.1:22000/a", "http://127.0.0.1:22000/b");

        Diagnostics.Assert("exit code", 26, exitCode);
        Diagnostics.Diff("stdout", "body /a\n0 0 200 176\n1 26 000 162\n", stdout);
        Diagnostics.Diff("stderr", "curl: (26) client mime read EOF fail, only 162/176 of needed bytes read\n", stderr);
        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("body /a\n0 0 200 176\n1 26 000 162\n", stdout);
        Assert.AreEqual("curl: (26) client mime read EOF fail, only 162/176 of needed bytes read\n", stderr);
        CollectionAssert.AreEqual(new long[] { 176 }, sent);
    }

    [TestMethod]
    public async Task RunAsync_TextFormForTwoUrls_SendsItToBoth()
    {
        (int exitCode, string stdout, _, List<long> sent) = await RunAsync(
            "-F", "f=value", "http://127.0.0.1:22000/a", "http://127.0.0.1:22000/b");

        Diagnostics.Diff("stdout", "body /a\n0 0 200 153\nbody /b\n1 0 200 153\n", stdout);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("body /a\n0 0 200 153\nbody /b\n1 0 200 153\n", stdout);
        CollectionAssert.AreEqual(new long[] { 153, 153 }, sent);
    }

    [TestMethod]
    public async Task RunAsync_ParallelStandardInputFormForTwoUrls_SendsItOnlyToTheLast()
    {
        (int exitCode, _, string stderr, List<long> sent) = await RunAsync(
            "-Z", "--parallel-immediate", "--parallel-max", "1",
            "-F", "f=@-", "http://127.0.0.1:22000/a", "http://127.0.0.1:22000/b");

        Diagnostics.Assert("bodies sent", "0, 176", string.Join(", ", sent));
        Assert.AreEqual(0, exitCode, stderr);
        CollectionAssert.AreEqual(new long[] { 0, 176 }, sent);
    }

    private async Task<(int ExitCode, string Stdout, string Stderr, List<long> Sent)> RunAsync(params string[] form)
    {
        List<long> sent = [];
        RecordingProtocolHandler http = new("http", async context =>
        {
            long length = context.Http?.Body switch
            {
                StreamBody stream => stream.Length.GetValueOrDefault(),
                BytesBody bytes => bytes.Content.Length,
                _ => -1,
            };
            lock (sent)
            {
                sent.Add(length);
            }

            context.Events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
            byte[] body = Encoding.ASCII.GetBytes($"body {context.Url.AbsolutePath}\n");
            await context.Output.WriteAsync(body, context.CancellationToken);
            return TransferResult.Success(body.Length) with { Report = new TransferReport { ResponseCode = 200, UploadSize = length } };
        });
        string[] arguments = ["-sS", "-w", "%{urlnum} %{exitcode} %{http_code} %{size_upload}\n", .. form];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream("STDIN-DATA-123"u8.ToArray()),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
        string stdout = Encoding.UTF8.GetString(standardOutput.ToArray());
        string stderr = Encoding.UTF8.GetString(standardError.ToArray()).ReplaceLineEndings("\n");
        Diagnostics.Act("exit code, stdout, stderr", $"{exitCode} [{stdout}] [{stderr}]");
        return (exitCode, stdout, stderr, sent);
    }
}
