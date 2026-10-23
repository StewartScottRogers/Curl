using System.Text;

using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the order in which a <c>-Z</c> run writes the bodies of several transfers to one host that
/// answers at once, as curl 8.22.0 writes them for <c>/echo /default /utf8 /four</c> (ADR-0768): the
/// first transfer to a plain <c>http</c> host runs alone, and when it ends, the transfers its end frees
/// start one at a time, the first of them last. The fake server holds <c>/echo</c> until the run has
/// started every transfer it can, which the progress meter's first draw shows, and answers every other
/// path at once.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelOrderTests
{
    private static readonly string[] Urls = ["http://o.h/echo", "http://o.h/default", "http://o.h/utf8", "http://o.h/four"];

    private readonly MemoryStream standardOutput = new();

    private readonly FirstWriteSignallingStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_DefaultParallelMax_WritesEchoUtf8FourDefault() =>
        await AssertStandardOutputOrderAsync([], "/echo/utf8/four/default");

    [TestMethod]
    public async Task RunAsync_ParallelMaxTwo_WritesEchoUtf8DefaultFour() =>
        await AssertStandardOutputOrderAsync(["--parallel-max", "2"], "/echo/utf8/default/four");

    [TestMethod]
    public async Task RunAsync_ParallelMaxOne_WritesUrlOrder() =>
        await AssertStandardOutputOrderAsync(["--parallel-max", "1"], "/echo/default/utf8/four");

    /// <summary>
    /// Runs <c>-Z</c> with <paramref name="parallelMaxOptions" /> over the four URLs, ends <c>/echo</c>
    /// once the meter has drawn, and checks the bodies, each its own path, arrive as <paramref name="expectedOrder" />.
    /// </summary>
    private async Task AssertStandardOutputOrderAsync(string[] parallelMaxOptions, string expectedOrder)
    {
        HoldingEchoServer server = new();
        string[] arguments = ["-Z", .. parallelMaxOptions, .. Urls];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        Task<int> run = new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([server])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                writesProgressMeter: true)
            .RunAsync(arguments);
        await Task.WhenAll(server.EchoStarted.Task, standardError.Written.Task);
        server.EchoAnswer.SetResult();
        int exitCode = await run;

        string written = Encoding.ASCII.GetString(standardOutput.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Diff("stdout", expectedOrder, written);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedOrder, written);
    }

    /// <summary>
    /// An <c>http</c> server whose every transfer writes its URL path as its body: <c>/echo</c> once the
    /// test lets it answer, every other path at once.
    /// </summary>
    private sealed class HoldingEchoServer : IProtocolHandler
    {
        /// <summary>Gets what completes when <c>/echo</c> has started.</summary>
        public TaskCompletionSource EchoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets what the test completes to let <c>/echo</c> answer.</summary>
        public TaskCompletionSource EchoAnswer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        /// <inheritdoc />
        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            string path = context.Url.AbsolutePath;
            if (path == "/echo")
            {
                EchoStarted.TrySetResult();
                await EchoAnswer.Task;
            }

            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(path), CancellationToken.None);
            return TransferResult.Success(path.Length);
        }
    }

    /// <summary>A memory stream that signals its first write: the <c>-Z</c> progress meter's first draw.</summary>
    private sealed class FirstWriteSignallingStream : MemoryStream
    {
        /// <summary>Gets what completes on the first write.</summary>
        public TaskCompletionSource Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            Written.TrySetResult();
        }

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            Written.TrySetResult();
        }
    }
}
