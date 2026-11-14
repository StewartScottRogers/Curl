using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins BL-5108: <c>--trace-ascii -</c> to a standard output whose reader has gone ends in exit 23
/// (<c>Failure writing output to destination</c>) as curl does, not exit 2.
/// </summary>
[TestClass]
public sealed class TraceToClosedStandardOutputExitCodeTests
{
    private const int BigWrite = 8192;

    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_TraceAsciiToStandardOutputWhoseReaderHasGone_ExitsWithWriteError()
    {
        FailingWriteStream closed = new();
        RecordingProtocolHandler http = new("http", async context =>
        {
            context.Events.ReportDataReceived(new byte[BigWrite]);
            context.Events.ReportDataReceived(new byte[BigWrite]);
            try
            {
                await context.Output.WriteAsync(new byte[BigWrite]);
            }
            catch (IOException failure)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed " + BigWrite + " returned " + ((failure as OutputWriteFailedException)?.BytesAccepted ?? 0));
            }

            return TransferResult.Success(BigWrite);
        });
        Diagnostics.Arrange("standard output", "a stream whose writes throw");

        int exitCode = await RunAsync(closed, http, "-sS", "--trace-ascii", "-", "http://h/");
        string stderr = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("exit code, stderr", $"{exitCode} [{stderr}]");

        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        StringAssert.StartsWith(stderr, "curl: (23) Failure writing output to destination");
    }

    [TestMethod]
    public async Task RunAsync_BodyToStandardOutputWhoseReaderHasGone_ExitsWithWriteError()
    {
        FailingWriteStream closed = new();
        RecordingProtocolHandler http = new("http", async context =>
        {
            try
            {
                await context.Output.WriteAsync(new byte[BigWrite]);
            }
            catch (IOException)
            {
                return TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed " + BigWrite + " returned 0");
            }

            return TransferResult.Success(BigWrite);
        });

        int exitCode = await RunAsync(closed, http, "-sS", "http://h/");
        string stderr = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("exit code, stderr", $"{exitCode} [{stderr}]");

        Assert.AreEqual(23, exitCode);
        StringAssert.StartsWith(stderr, "curl: (23) Failure writing output to destination");
    }

    private Task<int> RunAsync(Stream output, IProtocolHandler handler, params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                files,
                files,
                output,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);
}
