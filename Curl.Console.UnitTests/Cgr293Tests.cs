using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-293 (BL-3822): under <c>-Z --skip-existing</c> the skipped transfer's <c>-w</c> line is
/// written after the running transfers', as curl 8.22.0 does: <c>1 0 y.txt</c>, then <c>0 0 x.txt</c>.
/// </summary>
[TestClass]
public sealed class Cgr293Tests
{
    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_SkipExistingUnderParallel_ReportsTheSkippedTransferAfterTheRunningOnes()
    {
        files.ExistingPaths.Add("x.txt");
        string[] arguments =
        [
            "-Z", "--parallel-immediate", "-s", "--skip-existing",
            "-w", "%{urlnum} %{exitcode} %{filename_effective}\\n",
            "-o", "x.txt", "http://127.0.0.1:22000/a", "-o", "y.txt", "http://127.0.0.1:22000/b",
        ];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([new OkHandler()])),
                files,
                files,
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
        string stdout = Encoding.UTF8.GetString(standardOutput.ToArray());
        Diagnostics.Act("exit code, stdout", $"{exitCode} [{stdout}]");

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "1 0 y.txt\n0 0 x.txt\n", stdout);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("1 0 y.txt\n0 0 x.txt\n", stdout);
    }

    private sealed class OkHandler : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            await context.Output.WriteAsync(new byte[] { 1 }, context.CancellationToken);
            return TransferResult.Success(1);
        }
    }
}
