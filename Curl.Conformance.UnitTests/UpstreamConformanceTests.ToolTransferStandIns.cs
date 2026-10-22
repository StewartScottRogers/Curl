using System.Globalization;
using System.Text;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // The curl tool's transfer cases that set curl's debug-build test variables rely on Debug (ADR-0735, BL-2910).
    // CURL_UPLOAD_SIZE (447) is the upload size src/tool_operate.c hands libcurl in place of the -T file's, so curl sends
    // that Content-Length and that many bytes of a growing file: RunCurlAsync gives curl a file system whose reads are cut
    // to it (UploadSizeCappingFileSystem). CURL_DBG_SUPPRESS_CONNECT_HDS (2349) keeps the CONNECT reply heads out of the
    // header output exactly as --suppress-connect-headers does (lib/sendf.c at curl-8_22_0): RunCurlAsync appends that
    // option to the command line. The tool's WebSocket cases under CURL_ENTROPY (2300) already get Debug from the ws rule,
    // and RunCurlAsync gives curl the forced key sequence (WebSocketRandomSourceFor). The real tool reads none of them.
    internal const string UploadSizeVariableName = "CURL_UPLOAD_SIZE";

    internal const string SuppressConnectHeadersVariableName = "CURL_DBG_SUPPRESS_CONNECT_HDS";

    private static bool SetsToolTransferDebugVariable(string text) =>
        text.Contains("\n" + UploadSizeVariableName + "=", StringComparison.Ordinal)
        || text.Contains("\n" + SuppressConnectHeadersVariableName + "=", StringComparison.Ordinal);

    // The file system RunCurlAsync gives curl: reads cut to the case's CURL_UPLOAD_SIZE (atoi's 0 for a value that is not
    // a number), else null, for the physical one.
    internal static IFileSystem? FileSystemFor(IReadOnlyDictionary<string, string> environment) =>
        environment.GetValueOrDefault(UploadSizeVariableName) is { } size
            ? new UploadSizeCappingFileSystem(new PhysicalFileSystem(), int.TryParse(size, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int length) ? length : 0)
            : null;

    // The command line RunCurlAsync runs: the case's, with --suppress-connect-headers appended under
    // CURL_DBG_SUPPRESS_CONNECT_HDS (which reaches the last option group alone, as no upstream case sets it beside --next).
    internal static IReadOnlyList<string> ArgumentsFor(UpstreamCurlInvocation invocation) =>
        invocation.EnvironmentVariables.ContainsKey(SuppressConnectHeadersVariableName)
            ? [.. invocation.Arguments, "--suppress-connect-headers"]
            : invocation.Arguments;

    // Opens every file through the inner file system, and reports each file read as uploadSize bytes long and ends it there.
    internal sealed class UploadSizeCappingFileSystem(IFileSystem inner, long uploadSize) : IFileSystem
    {
        public long UploadSize => uploadSize;

        public async ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            FileOpenResult opened = await inner.OpenForReadAsync(path, cancellationToken).ConfigureAwait(false);
            return opened.Content is { } content
                ? FileOpenResult.Opened(new LengthCappedReadStream(content, uploadSize), uploadSize, opened.LastWriteTimeUtc)
                : opened;
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            inner.OpenForWriteAsync(path, mode, createMode, cancellationToken);
    }

    // A read-only view of the first length bytes of inner, which it disposes.
    internal sealed class LengthCappedReadStream(Stream inner, long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, Room(count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer[..Room(buffer.Length)], cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Room(int count) => (int)Math.Clamp(length - inner.Position, 0, count);
    }

    [TestMethod]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_UPLOAD_SIZE=498\n</setenv>\n", true)]
    [DataRow("<features>\nproxy\nDebug\n</features>\n<setenv>\nCURL_DBG_SUPPRESS_CONNECT_HDS=1\n</setenv>\n", true)]
    [DataRow("<features>\nDebug\nws\n</features>\n<setenv>\nCURL_ENTROPY=12345678\n</setenv>\n<command>\n-T . ws://%HOSTIP:%HTTPPORT/2300\n</command>\n", true)]
    [DataRow("<features>\nDebug\n</features>\n<setenv>\nCURL_UPLOAD=498\n</setenv>\n", false)]
    public void PlatformFor_ToolTransferDebugVariable_ReportsDebugOnlyThen(string client, bool reportsDebug)
    {
        byte[] testFile = Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n{client}</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }

    [TestMethod]
    public void FileSystemFor_NoUploadSizeVariable_IsNull()
    {
        Dictionary<string, string> environment = new() { ["CURL_ENTROPY"] = "12345678" };

        IFileSystem? fileSystem = FileSystemFor(environment);

        Assert.IsNull(fileSystem);
    }

    [TestMethod]
    [DataRow("498", 498L)]
    [DataRow("many", 0L)]
    public void FileSystemFor_UploadSizeVariable_CapsReadsAtItsAtoiValue(string value, long expectedLength)
    {
        Dictionary<string, string> environment = new() { [UploadSizeVariableName] = value };

        IFileSystem? fileSystem = FileSystemFor(environment);

        Assert.IsInstanceOfType<UploadSizeCappingFileSystem>(fileSystem);
        Assert.AreEqual(expectedLength, ((UploadSizeCappingFileSystem)fileSystem).UploadSize);
    }

    [TestMethod]
    public async Task UploadSizeCappingFileSystem_OpenForRead_ReportsAndReadsOnlyTheUploadSize()
    {
        UploadSizeCappingFileSystem fileSystem = new(new MemoryFileSystem(Encoding.ASCII.GetBytes("hellohello")), 7);

        FileOpenResult opened = await fileSystem.OpenForReadAsync("upload", CancellationToken.None);
        using StreamReader reader = new(opened.Content!, Encoding.ASCII);
        string read = await reader.ReadToEndAsync();

        Assert.AreEqual(7, opened.Length);
        Assert.AreEqual(7, opened.Content!.Length);
        Assert.AreEqual("hellohe", read);
    }

    [TestMethod]
    public async Task UploadSizeCappingFileSystem_OpenForReadFails_ReturnsTheFailure()
    {
        UploadSizeCappingFileSystem fileSystem = new(new MemoryFileSystem(null), 7);

        FileOpenResult opened = await fileSystem.OpenForReadAsync("missing", CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.NotFound, opened.Status);
    }

    [TestMethod]
    public async Task UploadSizeCappingFileSystem_OpenForWrite_GoesToTheInnerFileSystem()
    {
        MemoryFileSystem inner = new(null);
        UploadSizeCappingFileSystem fileSystem = new(inner, 7);

        FileOpenResult opened = await fileSystem.OpenForWriteAsync("out", FileWriteMode.Truncate, UnixFileMode.None, CancellationToken.None);

        Assert.AreSame(inner.Written, opened.Content);
    }

    [TestMethod]
    public void LengthCappedReadStream_ReadsSeeksAndRefusesWrites()
    {
        using LengthCappedReadStream stream = new(new MemoryStream(Encoding.ASCII.GetBytes("abcdef")), 4);
        byte[] buffer = new byte[8];

        int first = stream.Read(buffer, 0, 3);
        int second = stream.Read(buffer, 3, 5);
        stream.Position = 1;
        long sought = stream.Seek(2, SeekOrigin.Begin);
        stream.Flush();

        Assert.AreEqual(3, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual("abcd", Encoding.ASCII.GetString(buffer, 0, 4));
        Assert.AreEqual(2, sought);
        Assert.AreEqual(2, stream.Position);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(buffer, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(1));
    }

    [TestMethod]
    public async Task LengthCappedReadStream_ReadAsyncWithArray_StopsAtTheLength()
    {
        await using LengthCappedReadStream stream = new(new MemoryStream(Encoding.ASCII.GetBytes("abcdef")), 2);
        byte[] buffer = new byte[4];

        int read = await stream.ReadAsync(buffer, 0, 4, CancellationToken.None);

        Assert.AreEqual(2, read);
    }

    [TestMethod]
    public void ArgumentsFor_SuppressConnectHeadersVariable_AppendsTheOption()
    {
        UpstreamCurlInvocation invocation = InvocationWith(new Dictionary<string, string> { [SuppressConnectHeadersVariableName] = "1" });

        IReadOnlyList<string> arguments = ArgumentsFor(invocation);

        CollectionAssert.AreEqual(new[] { "http://127.0.0.1/", "--suppress-connect-headers" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_NoSuppressConnectHeadersVariable_KeepsTheCommandLine()
    {
        UpstreamCurlInvocation invocation = InvocationWith(new Dictionary<string, string>());

        IReadOnlyList<string> arguments = ArgumentsFor(invocation);

        Assert.AreSame(invocation.Arguments, arguments);
    }

    // The forced WebSocket key reaches the curl tool's handshake: without it the key is random and test2300 differs at
    // its Sec-WebSocket-Key line.
    [TestMethod]
    public async Task RunCurlAsync_ToolWebSocketCaseUnderCurlEntropy_SendsTheForcedKey()
    {
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test2300{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(2300, testFile);

        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.ToString());
    }

    private static UpstreamCurlInvocation InvocationWith(Dictionary<string, string> environment) =>
        new(["http://127.0.0.1/"], Stream.Null, Stream.Null, Stream.Null, null!, null!, environment);

    // Opens every read path as content (or not found when null) and every write path as one shared MemoryStream.
    private sealed class MemoryFileSystem(byte[]? content) : IFileSystem
    {
        public MemoryStream Written { get; } = new();

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(content is null
                ? FileOpenResult.Failed(FileAccessStatus.NotFound)
                : FileOpenResult.Opened(new MemoryStream(content), content.Length, null));

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Opened(Written, 0, null));
    }
}
