using System.Text;
using Curl.Engine;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="RateLimitedUploadStream" /> holding a <c>-T</c> or <c>-F</c> upload at <c>--limit-rate</c>,
/// as curl 8.22.0 sends a <c>-F f=@f.txt --limit-rate 10</c> body 10 bytes a second (AF-0300, BL-2938), on a
/// clock whose every wait passes at once: one second's worth per asynchronous read and a second's wait before
/// each later one, while seeking, the length and the position pass through to the upload so a resend can rewind it.
/// </summary>
[TestClass]
public sealed class RateLimitedUploadStreamTests
{
    private static readonly byte[] Body = Encoding.ASCII.GetBytes("0123456789abcdefghijABCDEFGHIJ");

    private readonly ImmediateTimerTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ReadAsync_ThirtyBytesAtTenPerSecond_ReadsTenBytesASecond()
    {
        await using RateLimitedUploadStream upload = new(new MemoryStream(Body), 10, clock);
        Diagnostics.Arrange("body", Encoding.ASCII.GetString(Body));

        (List<int> pieces, byte[] read) = await ReadAllAsync(upload);

        Diagnostics.Act("pieces", string.Join(", ", pieces));
        Diagnostics.Act("waits (ms)", WaitsText);
        CollectionAssert.AreEqual(Body, read);
        CollectionAssert.AreEqual(new[] { 10, 10, 10 }, pieces);
        Assert.IsTrue(clock.Waits.Count >= 2 && clock.Waits.All(wait => wait == TimeSpan.FromSeconds(1)), WaitsText);
    }

    [TestMethod]
    public async Task ReadAsync_ArrayOverload_ReadsNoMoreThanOneSecondsWorth()
    {
        await using RateLimitedUploadStream upload = new(new MemoryStream(Body), 10, clock);
        byte[] buffer = new byte[64];

        int read = await upload.ReadAsync(buffer, 4, 60, CancellationToken.None);

        Diagnostics.Act("read", read);
        Assert.AreEqual(10, read);
        CollectionAssert.AreEqual(Body[..10], buffer[4..14]);
    }

    [TestMethod]
    public void Read_Synchronous_PassesThroughUnlimited()
    {
        using RateLimitedUploadStream upload = new(new MemoryStream(Body), 10, clock);
        byte[] buffer = new byte[64];

        int read = upload.Read(buffer, 0, buffer.Length);

        Diagnostics.Act("read", read);
        Assert.AreEqual(Body.Length, read);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task SeekPositionAndLength_PassThroughToTheUpload_SoAResendCanRewindIt()
    {
        MemoryStream inner = new(Body);
        await using RateLimitedUploadStream upload = new(inner, 10, clock);
        int firstRead = await upload.ReadAsync(new byte[64]);

        long afterRead = upload.Position;
        long rewound = upload.Seek(0, SeekOrigin.Begin);
        upload.Position = 5;

        Diagnostics.Act("position after read", afterRead);
        Assert.AreEqual(10, firstRead);
        Assert.IsTrue(upload.CanSeek);
        Assert.IsTrue(upload.CanRead);
        Assert.IsFalse(upload.CanWrite);
        Assert.AreEqual(Body.Length, upload.Length);
        Assert.AreEqual(10, afterRead);
        Assert.AreEqual(0, rewound);
        Assert.AreEqual(5, inner.Position);
        Assert.AreEqual(5, upload.Position);
    }

    [TestMethod]
    public void WriteAndSetLength_Throw_AndFlushDoesNothing()
    {
        MemoryStream inner = new(Body);
        using RateLimitedUploadStream upload = new(inner, 10, clock);

        upload.Flush();

        Assert.ThrowsExactly<NotSupportedException>(() => upload.Write(Body, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => upload.SetLength(1));
        Assert.AreEqual(Body.Length, inner.Length);
    }

    [TestMethod]
    public void CanRewind_UploadThatSaysNothing_IsTrue()
    {
        using RateLimitedUploadStream upload = new(new MemoryStream(Body), 10, clock);

        Assert.IsTrue(upload.CanRewind);
    }

    [TestMethod]
    public void CanRewind_StandardInputThatCannotRewind_IsFalse()
    {
        StandardInputUploadStream unseekable = new(new MemoryStream(Body));
        StandardInputUploadStream standardInput = new(unseekable) { SeekOfUnseekableInputSucceeds = false };
        using RateLimitedUploadStream upload = new(standardInput, 10, clock);

        Diagnostics.Act("standard input can rewind", standardInput.CanRewind);
        Assert.IsFalse(standardInput.CanRewind);
        Assert.IsFalse(upload.CanRewind);
    }

    [TestMethod]
    public async Task RunAsync_UploadFileUnderLimitRate_HandsTheHandlerTheUploadTenBytesASecond()
    {
        InMemoryFileSystem files = new() { ReadContent = Body };
        List<int> pieces = [];
        RecordingProtocolHandler handler = new("dict", async context =>
        {
            pieces.AddRange((await ReadAllAsync(context.Upload!)).Pieces);
            return TransferResult.Success(0);
        });

        int exitCode = await RunAsync(files, handler, "-s", "-T", "up.txt", "--limit-rate", "10", "dict://h/x");

        Diagnostics.Act("pieces", string.Join(", ", pieces));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { 10, 10, 10 }, pieces);
        Assert.IsNotEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task RunAsync_FormUnderLimitRate_HandsTheHandlerTheFormBodyTenBytesASecond()
    {
        List<int> pieces = [];
        RecordingProtocolHandler handler = new("http", async context =>
        {
            StreamBody body = (StreamBody)context.Http!.Body!;
            pieces.AddRange((await ReadAllAsync(body.Content)).Pieces);
            return TransferResult.Success(0);
        });

        int exitCode = await RunAsync(new InMemoryFileSystem(), handler, "-s", "-F", "f=abc", "--limit-rate", "10", "http://h/x");

        Diagnostics.Act("pieces", string.Join(", ", pieces));
        Assert.AreEqual(0, exitCode);
        Assert.IsGreaterThan(1, pieces.Count);
        Assert.IsTrue(pieces.All(piece => piece <= 10), string.Join(", ", pieces));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileWithoutLimitRate_HandsTheHandlerTheUploadInOneRead()
    {
        InMemoryFileSystem files = new() { ReadContent = Body };
        List<int> pieces = [];
        RecordingProtocolHandler handler = new("dict", async context =>
        {
            pieces.AddRange((await ReadAllAsync(context.Upload!)).Pieces);
            return TransferResult.Success(0);
        });

        await RunAsync(files, handler, "-s", "-T", "up.txt", "dict://h/x");

        Diagnostics.Act("pieces", string.Join(", ", pieces));
        CollectionAssert.AreEqual(new[] { Body.Length }, pieces);
        Assert.IsEmpty(clock.Waits);
    }

    private string WaitsText => string.Join(", ", clock.Waits.Select(wait => (long)wait.TotalMilliseconds));

    private static async Task<(List<int> Pieces, byte[] Read)> ReadAllAsync(Stream upload)
    {
        List<int> pieces = [];
        using MemoryStream read = new();
        byte[] buffer = new byte[65536];
        int count;
        while ((count = await upload.ReadAsync(buffer)) > 0)
        {
            pieces.Add(count);
            read.Write(buffer, 0, count);
        }

        return (pieces, read.ToArray());
    }

    private async Task<int> RunAsync(InMemoryFileSystem files, RecordingProtocolHandler handler, params string[] arguments)
    {
        TransferDispatch dispatch = new(new ProtocolDispatcher([handler]));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode = await new CurlCommandRunner(
                _ => dispatch,
                files,
                files,
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false,
                timeProvider: clock)
            .RunAsync(arguments);
        Diagnostics.Act("exit code", exitCode);
        return exitCode;
    }
}
