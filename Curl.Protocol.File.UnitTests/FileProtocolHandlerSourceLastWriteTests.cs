using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;
using Microsoft.Win32.SafeHandles;

namespace Curl.Protocol.File;

/// <summary>
/// Pins how a <c>file://</c> source's last-write time past 9999-12-31T23:59:59Z reaches
/// <c>-R</c>, <c>-z</c> and the header block (BL-1423, ADR-0411). curl 8.21.0's Windows build,
/// measured on 2026-10-03 with a source stamped 30000-01-01T00:00:00Z, holds such a time as
/// <c>time_t</c> -1: <c>-I</c> prints <c>Last-Modified: Thu, 31 Dec 1969 23:59:59 GMT</c>,
/// <c>-R</c> leaves the output's time alone, <c>-z "1 Jan 2020"</c> writes nothing and
/// <c>-z "-1 Jan 2020"</c> writes the body. A build that reads <c>st_mtime</c> whole holds
/// the real Unix seconds.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerSourceLastWriteTests
{
    private const long Year30000UnixSeconds = 884_541_340_800;

    private const long WindowsLimitUnixSeconds = 32_566_777_199;

    private static readonly DateTimeOffset FileDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeCondition ModifiedSince2020 =
        new(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince);

    private static readonly TimeCondition UnmodifiedSince2020 =
        new(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/f.txt");

    private static string OsPath => "/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_RawTimePast9999WithNoLimit_ReachesSourceLastWriteUnixSeconds()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("lastRepresentableUnixSeconds", "none");

        (TransferResult result, _, string headers) = await DownloadAsync(Year30000UnixSeconds, null, null);

        Diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
        Diagnostics.Act("headers", headers);
        Diagnostics.Assert("SourceLastWriteUnixSeconds", Year30000UnixSeconds, result.SourceLastWriteUnixSeconds);
        Diagnostics.Assert("headers contain Last-Modified", false, headers.Contains("Last-Modified", StringComparison.Ordinal));
        Assert.AreEqual(Year30000UnixSeconds, result.SourceLastWriteUnixSeconds);
        Assert.DoesNotContain("Last-Modified", headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimeInRange_KeepsTheOpenTime()
    {
        long rawUnixSeconds = FileDate.ToUnixTimeSeconds() + 5;
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("fileDate", FileDate);
        Diagnostics.Arrange("rawUnixSeconds", rawUnixSeconds);

        (TransferResult result, _, string headers) = await DownloadAsync(rawUnixSeconds, null, null);

        Diagnostics.Act("SourceLastWriteTimeUtc", result.SourceLastWriteTimeUtc);
        Diagnostics.Act("headers", headers);
        Diagnostics.Assert("SourceLastWriteTimeUtc", FileDate, result.SourceLastWriteTimeUtc);
        Diagnostics.Assert("headers contain Last-Modified line", true, headers.Contains("Last-Modified: Mon, 01 Jan 2001 00:00:00 GMT", StringComparison.Ordinal));
        Assert.AreEqual(FileDate, result.SourceLastWriteTimeUtc);
        Assert.Contains("Last-Modified: Mon, 01 Jan 2001 00:00:00 GMT", headers);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoRawTime_KeepsTheOpenTime()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("fileDate", FileDate);
        Diagnostics.Arrange("lastRepresentableUnixSeconds", WindowsLimitUnixSeconds);

        (TransferResult result, _, _) = await DownloadAsync(null, WindowsLimitUnixSeconds, null);

        Diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
        Diagnostics.Assert("SourceLastWriteUnixSeconds", FileDate.ToUnixTimeSeconds(), result.SourceLastWriteUnixSeconds);
        Assert.AreEqual(FileDate.ToUnixTimeSeconds(), result.SourceLastWriteUnixSeconds);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimePastWindowsLimit_GivesNoRemoteTimeAndAMinusOneHeader()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("lastRepresentableUnixSeconds", WindowsLimitUnixSeconds);

        (TransferResult result, byte[] body, string headers) = await DownloadAsync(Year30000UnixSeconds, WindowsLimitUnixSeconds, null);

        Diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
        Diagnostics.Act("headers", headers);
        Diagnostics.Bytes("body", body);
        Diagnostics.Assert("SourceLastWriteUnixSeconds", null, result.SourceLastWriteUnixSeconds);
        Diagnostics.Assert("headers contain minus-one Last-Modified", true, headers.Contains("Last-Modified: Wed, 31 Dec 1969 23:59:59 GMT", StringComparison.Ordinal));
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("hi\n"), body);
        Assert.IsNull(result.SourceLastWriteUnixSeconds);
        Assert.Contains("Last-Modified: Wed, 31 Dec 1969 23:59:59 GMT", headers);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hi\n"), body);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimePastWindowsLimitIfModifiedSince_ComparesMinusOneAndWritesNothing()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("timeCondition", ModifiedSince2020);

        (TransferResult result, byte[] body, _) = await DownloadAsync(Year30000UnixSeconds, WindowsLimitUnixSeconds, ModifiedSince2020);

        Diagnostics.Act("TimeConditionUnmet", result.TimeConditionUnmet);
        Diagnostics.Act("body length", body.Length);
        Diagnostics.Assert("TimeConditionUnmet", true, result.TimeConditionUnmet);
        Diagnostics.Assert("body length", 0, body.Length);
        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.IsEmpty(body);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimePastWindowsLimitIfUnmodifiedSince_ComparesMinusOneAndWritesTheBody()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("timeCondition", UnmodifiedSince2020);

        (TransferResult result, byte[] body, _) = await DownloadAsync(Year30000UnixSeconds, WindowsLimitUnixSeconds, UnmodifiedSince2020);

        Diagnostics.Act("TimeConditionUnmet", result.TimeConditionUnmet);
        Diagnostics.Bytes("body", body);
        Diagnostics.Assert("TimeConditionUnmet", false, result.TimeConditionUnmet);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("hi\n"), body);
        Assert.IsFalse(result.TimeConditionUnmet);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hi\n"), body);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimePast9999IfModifiedSince_ComparesUnixSecondsAndWritesTheBody()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("timeCondition", ModifiedSince2020);

        (TransferResult result, byte[] body, _) = await DownloadAsync(Year30000UnixSeconds, null, ModifiedSince2020);

        Diagnostics.Act("TimeConditionUnmet", result.TimeConditionUnmet);
        Diagnostics.Bytes("body", body);
        Diagnostics.Assert("TimeConditionUnmet", false, result.TimeConditionUnmet);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("hi\n"), body);
        Assert.IsFalse(result.TimeConditionUnmet);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hi\n"), body);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawTimePast9999IfUnmodifiedSince_ComparesUnixSecondsAndWritesNothing()
    {
        Diagnostics.Arrange("url", FileUrl);
        Diagnostics.Arrange("rawUnixSeconds", Year30000UnixSeconds);
        Diagnostics.Arrange("timeCondition", UnmodifiedSince2020);

        (TransferResult result, byte[] body, _) = await DownloadAsync(Year30000UnixSeconds, null, UnmodifiedSince2020);

        Diagnostics.Act("TimeConditionUnmet", result.TimeConditionUnmet);
        Diagnostics.Act("body length", body.Length);
        Diagnostics.Assert("TimeConditionUnmet", true, result.TimeConditionUnmet);
        Diagnostics.Assert("body length", 0, body.Length);
        Assert.IsTrue(result.TimeConditionUnmet);
        Assert.IsEmpty(body);
    }

    [TestMethod]
    public void NoRawSourceLastWriteReader_ReadLastWriteUnixSeconds_ReadsNothing()
    {
        Diagnostics.Arrange("source", "empty MemoryStream");

        long? actual = new NoRawSourceLastWriteReader().ReadLastWriteUnixSeconds(new MemoryStream());

        Diagnostics.Act("ReadLastWriteUnixSeconds", actual);
        Diagnostics.Assert("ReadLastWriteUnixSeconds", null, actual);
        Assert.IsNull(actual);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ExecuteAsync_SourceStampedYear30000OnDisk_ReadsItsUnixSecondsThroughTheHandler()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bl1423-{Guid.NewGuid():N}.txt");
        await System.IO.File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("hi\n"));
        try
        {
            StampYear30000(path);
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read);
            var fileSystem = new FakeFileSystem();
            fileSystem.AddFileReadingFrom(OsPath, source, source.Length);
            var handler = new FileProtocolHandler(fileSystem) { LastRepresentableUnixSeconds = null };
            Diagnostics.Arrange("url", FileUrl);
            Diagnostics.Arrange("source length", source.Length);
            Diagnostics.Arrange("stamped unix seconds", Year30000UnixSeconds);

            TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream() });

            Diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
            Diagnostics.Assert("SourceLastWriteUnixSeconds", Year30000UnixSeconds, result.SourceLastWriteUnixSeconds);
            Assert.AreEqual(Year30000UnixSeconds, result.SourceLastWriteUnixSeconds);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ExecuteAsync_SourceStampedYear30000OnDiskWithTheWindowsLimit_GivesNoRemoteTime()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bl1423-{Guid.NewGuid():N}.txt");
        await System.IO.File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("hi\n"));
        try
        {
            StampYear30000(path);
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read);
            var fileSystem = new FakeFileSystem();
            fileSystem.AddFileReadingFrom(OsPath, source, source.Length);

            Diagnostics.Arrange("url", FileUrl);
            Diagnostics.Arrange("source length", source.Length);
            Diagnostics.Arrange("stamped unix seconds", Year30000UnixSeconds);

            TransferResult result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = FileUrl, Output = new MemoryStream() });

            Diagnostics.Act("SourceLastWriteUnixSeconds", result.SourceLastWriteUnixSeconds);
            Diagnostics.Assert("SourceLastWriteUnixSeconds", null, result.SourceLastWriteUnixSeconds);
            Assert.IsNull(result.SourceLastWriteUnixSeconds);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Win32SourceLastWriteReader_NotAFileStream_ReadsNothing()
    {
        Diagnostics.Arrange("source", "empty MemoryStream");

        long? actual = new Win32SourceLastWriteReader().ReadLastWriteUnixSeconds(new MemoryStream());

        Diagnostics.Act("ReadLastWriteUnixSeconds", actual);
        Diagnostics.Assert("ReadLastWriteUnixSeconds", null, actual);
        Assert.IsNull(actual);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Win32SourceLastWriteReader_PipeHandle_ReadsNoTimePastTheOpenTime()
    {
        using var pipe = new System.IO.Pipes.AnonymousPipeServerStream(System.IO.Pipes.PipeDirection.In);
        using var pipeAsFile = new FileStream(new SafeFileHandle(pipe.SafePipeHandle.DangerousGetHandle(), ownsHandle: false), FileAccess.Read);

        Diagnostics.Arrange("source", "anonymous pipe read end as FileStream");

        long actual = new Win32SourceLastWriteReader().ReadLastWriteUnixSeconds(pipeAsFile)!.Value;

        Diagnostics.Act("ReadLastWriteUnixSeconds", actual);
        Diagnostics.Assert("ReadLastWriteUnixSeconds <= 0", true, actual <= 0L);
        Assert.IsLessThanOrEqualTo(0L, actual);
    }

    private static void StampYear30000(string path)
    {
        long fileTime = (Year30000UnixSeconds * 10_000_000) + 116_444_736_000_000_000;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
        Assert.IsTrue(SetFileTime(stream.SafeFileHandle, IntPtr.Zero, IntPtr.Zero, ref fileTime));
    }

    private static async Task<(TransferResult Result, byte[] Body, string Headers)> DownloadAsync(
        long? rawUnixSeconds,
        long? lastRepresentableUnixSeconds,
        TimeCondition? condition)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("hi\n"), FileDate);
        var output = new MemoryStream();
        var headers = new MemoryStream();
        var handler = new FileProtocolHandler(fileSystem)
        {
            SourceLastWriteReader = new FixedSourceLastWriteReader(rawUnixSeconds),
            LastRepresentableUnixSeconds = lastRepresentableUnixSeconds,
        };

        TransferResult result = await handler.ExecuteAsync(
            new TransferContext { Url = FileUrl, Output = output, HeaderOutput = headers, TimeCondition = condition });

        return (result, output.ToArray(), Encoding.ASCII.GetString(headers.ToArray()));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileTime(SafeFileHandle file, IntPtr creationTime, IntPtr lastAccessTime, ref long lastWriteTime);

    private sealed class FixedSourceLastWriteReader(long? unixSeconds) : ISourceLastWriteReader
    {
        public long? ReadLastWriteUnixSeconds(Stream source) => unixSeconds;
    }
}
