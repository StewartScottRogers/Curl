using System.Text;
using Curl.Core;
using Curl.Core.Multipart;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins STD-226: curl 8.21.0 opens a <c>--trace</c> or <c>--trace-ascii</c> file at its first trace
/// event, so <c>--trace tr.txt -F f=@nofile.txt</c> (or <c>-T nofile.txt</c>) exits 26 with no
/// <c>tr.txt</c>.
/// </summary>
[TestClass]
public sealed class TraceFileOpenedOnFirstWriteStreamTests
{
    [TestMethod]
    [DataRow("--trace", "-F", "f=@nofile.txt")]
    [DataRow("--trace-ascii", "-F", "f=@nofile.txt")]
    [DataRow("--trace", "-T", "nofile.txt")]
    [DataRow("--trace-ascii", "-T", "nofile.txt")]
    public async Task RunAsync_TransferFailsAtSetup_OpensNoTraceFile(string trace, string option, string value)
    {
        InMemoryFileSystem files = new();
        files.UnreadablePaths.Add("nofile.txt");
        RecordingProtocolHandler http = new("http", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                files,
                files,
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files,
                formBodyBuilder: new MultipartFormBodyBuilder(files, Encoding.UTF8, () => "B", standardInput: null))
            .RunAsync(["-sS", trace, "tr.txt", option, value, "http://127.0.0.1:24000/echo/"]);

        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.IsFalse(files.Written.ContainsKey("tr.txt"));
    }

    [TestMethod]
    public void Flush_BeforeAnyWrite_OpensNoFile()
    {
        InMemoryFileSystem files = new();
        using TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", new MemoryStream());

        trace.Flush();

        Assert.IsEmpty(files.WriteModes);
    }

    [TestMethod]
    public void Write_TwoWrites_OpensTheFileOnceTruncatedAndHoldsBoth()
    {
        InMemoryFileSystem files = new();
        MemoryStream standardError = new();
        using (TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", standardError))
        {
            trace.Write("one "u8.ToArray(), 0, 4);
            trace.Write("two"u8);
            trace.Flush();
        }

        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes.Single());
        CollectionAssert.AreEqual("one two"u8.ToArray(), files.Written["tr.txt"].ToArray());
        Assert.IsFalse(files.Written["tr.txt"].CanWrite);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public void OpenNow_ThenAWrite_OpensTheFileOnce()
    {
        InMemoryFileSystem files = new();
        using TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", new MemoryStream());

        trace.OpenNow();
        Assert.HasCount(1, files.WriteModes);
        trace.Write("one"u8);

        Assert.HasCount(1, files.WriteModes);
        CollectionAssert.AreEqual("one"u8.ToArray(), files.Written["tr.txt"].ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_AfterAWrite_ClosesTheFile()
    {
        InMemoryFileSystem files = new();
        TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", new MemoryStream());
        trace.Write("one"u8);

        await trace.DisposeAsync();

        Assert.IsFalse(files.Written["tr.txt"].CanWrite);
    }

    [TestMethod]
    public void Write_FileCannotBeOpened_WritesToStandardErrorAndLeavesItOpen()
    {
        InMemoryFileSystem files = new();
        files.UnwritablePaths.Add("tr.txt");
        MemoryStream standardError = new();
        using (TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", standardError))
        {
            trace.Write("one "u8);
            trace.Write("two"u8);
        }

        Assert.HasCount(1, files.WriteModes);
        Assert.IsTrue(standardError.CanWrite);
        CollectionAssert.AreEqual("one two"u8.ToArray(), standardError.ToArray());
    }

    [TestMethod]
    public void Write_FileFailsToTakeTheBytes_IgnoresTheFailure()
    {
        InMemoryFileSystem files = new();
        files.WriteFailingPaths.Add("tr.txt");
        MemoryStream standardError = new();
        using TraceFileOpenedOnFirstWriteStream trace = new(files, "tr.txt", standardError);

        trace.Write("one"u8);

        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public void Write_OpenDoesNotCompleteSynchronously_Throws()
    {
        using TraceFileOpenedOnFirstWriteStream trace = new(new NeverOpeningFileSystem(), "tr.txt", new MemoryStream());

        Assert.ThrowsExactly<InvalidOperationException>(() => trace.Write("one"u8));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using TraceFileOpenedOnFirstWriteStream trace = new(new InMemoryFileSystem(), "tr.txt", new MemoryStream());

        Assert.IsTrue(trace.CanWrite);
        Assert.IsFalse(trace.CanRead);
        Assert.IsFalse(trace.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => trace.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => trace.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => trace.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => trace.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => trace.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => trace.SetLength(0));
    }

    private sealed class NeverOpeningFileSystem : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            new(new TaskCompletionSource<FileOpenResult>().Task);
    }
}
