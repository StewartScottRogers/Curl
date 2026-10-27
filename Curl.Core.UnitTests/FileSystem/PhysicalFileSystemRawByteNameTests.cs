using System.Runtime.InteropServices;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Microsoft.Win32.SafeHandles;

namespace Curl.Core.FileSystem;

/// <summary>
/// Drives <see cref="PhysicalFileSystem" />'s read open of a <see cref="RawBytePath" /> holding a
/// byte that is not valid UTF-8 (CGR-114, BL-3899): with an injected raw opener on every platform,
/// and on Linux and macOS against a temporary file whose name is those raw bytes, as curl's
/// <c>open()</c> takes them.
/// </summary>
[TestClass]
public sealed partial class PhysicalFileSystemRawByteNameTests
{
    /// <summary>The runtime's <c>O_WRONLY | O_CLOEXEC | O_CREAT</c>.</summary>
    private const int CreateForWriteFlags = 0x0001 | 0x0010 | 0x0020;

    /// <summary>The text form of <c>caf</c>, the raw byte E9, <c>.txt</c>.</summary>
    private const string RawByteName = "caf\uDCE9.txt";

    public TestContext TestContext { get; set; } = null!;

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello raw bytes");

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void RawBytePathReadOpenerFor_Platform_IsNoneOnlyOnWindows(bool isWindows, bool expectsOpener)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("isWindows", isWindows);

        Func<byte[], PhysicalFileSystem.RawBytePathOpen>? opener = PhysicalFileSystem.RawBytePathReadOpenerFor(isWindows);

        diagnostics.Act("opener", opener is not null);
        Assert.AreEqual(expectsOpener, opener is not null);
    }

    [TestMethod]
    [DataRow(2, FileAccessStatus.NotFound)]
    [DataRow(1, FileAccessStatus.AccessDenied)]
    [DataRow(13, FileAccessStatus.AccessDenied)]
    [DataRow(20, FileAccessStatus.NotADirectory)]
    [DataRow(21, FileAccessStatus.IsDirectory)]
    [DataRow(5, FileAccessStatus.IoError)]
    public async Task OpenForReadAsync_RawOpenFails_ReportsTheErrnosStatus(int errno, FileAccessStatus expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("errno", errno);
        byte[]? openedBytes = null;
        PhysicalFileSystem fileSystem = new(
            setsUnixCreateMode: false,
            openRawBytePathForRead: bytes =>
            {
                openedBytes = bytes;
                return new PhysicalFileSystem.RawBytePathOpen(null, errno);
            });

        FileOpenResult result = await fileSystem.OpenForReadAsync("/dir/" + RawByteName, CancellationToken.None);

        diagnostics.Act("status", result.Status);
        Assert.AreEqual(expected, result.Status);
        Assert.IsNull(result.FailureException);
        Assert.AreEqual("2F6469722F636166E92E74787400", Convert.ToHexString(openedBytes!));
    }

    [TestMethod]
    public async Task OpenForReadAsync_RawOpenGivesADescriptor_ReadsItsContentLengthAndTime()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string directory = Directory.CreateTempSubdirectory("curl-rawbyte-").FullName;
        try
        {
            string realPath = Path.Combine(directory, "real.txt");
            await File.WriteAllBytesAsync(realPath, Content, TestContext.CancellationToken);
            diagnostics.Arrange("file", realPath);
            PhysicalFileSystem fileSystem = new(
                setsUnixCreateMode: false,
                openRawBytePathForRead: _ => new PhysicalFileSystem.RawBytePathOpen(File.OpenHandle(realPath), 0));

            FileOpenResult result = await fileSystem.OpenForReadAsync(RawByteName, CancellationToken.None);
            byte[] read;
            using (MemoryStream copy = new())
            {
                await using Stream content = result.Content!;
                await content.CopyToAsync(copy, TestContext.CancellationToken);
                read = copy.ToArray();
            }

            diagnostics.Act("status", result.Status);
            Assert.AreEqual(FileAccessStatus.Ok, result.Status);
            Assert.AreEqual(Content.Length, result.Length);
            Assert.IsNotNull(result.LastWriteTimeUtc);
            CollectionAssert.AreEqual(Content, read);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task OpenForReadAsync_RawOpenGivesAnInvalidHandle_FailsWithoutThrowing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("handle", "invalid");
        PhysicalFileSystem fileSystem = new(
            setsUnixCreateMode: false,
            openRawBytePathForRead: _ => new PhysicalFileSystem.RawBytePathOpen(new SafeFileHandle(-1, ownsHandle: true), 0));

        FileOpenResult result = await fileSystem.OpenForReadAsync(RawByteName, CancellationToken.None);

        diagnostics.Act("status", result.Status);
        Assert.IsFalse(result.IsOpen);
        Assert.IsNotNull(result.FailureException);
    }

    [TestMethod]
    public async Task OpenForReadAsync_PathWithoutRawByte_NeverCallsTheRawOpener()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string directory = Directory.CreateTempSubdirectory("curl-rawbyte-").FullName;
        try
        {
            string missing = Path.Combine(directory, "café.txt");
            diagnostics.Arrange("path", missing);
            bool called = false;
            PhysicalFileSystem fileSystem = new(
                setsUnixCreateMode: false,
                openRawBytePathForRead: _ =>
                {
                    called = true;
                    return new PhysicalFileSystem.RawBytePathOpen(null, 0);
                });

            FileOpenResult result = await fileSystem.OpenForReadAsync(missing, CancellationToken.None);

            diagnostics.Act("status", result.Status);
            Assert.AreEqual(FileAccessStatus.NotFound, result.Status);
            Assert.IsFalse(called);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string directory = Directory.CreateTempSubdirectory("curl-rawbyte-").FullName;
        string path = Path.Combine(directory, RawByteName);
        byte[] name = [.. RawBytePath.ToBytes(path), 0];
        try
        {
            CreateUnderRawBytes(name, Content);
            diagnostics.Arrange("name", Convert.ToHexString(name));

            FileOpenResult result = await new PhysicalFileSystem().OpenForReadAsync(path, CancellationToken.None);
            byte[] read;
            using (MemoryStream copy = new())
            {
                await using Stream content = result.Content!;
                await content.CopyToAsync(copy, TestContext.CancellationToken);
                read = copy.ToArray();
            }

            diagnostics.Act("status", result.Status);
            Assert.AreEqual(FileAccessStatus.Ok, result.Status);
            Assert.AreEqual(Content.Length, result.Length);
            CollectionAssert.AreEqual(Content, read);
        }
        finally
        {
            DeleteUnderRawBytes(name);
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task OpenForReadAsync_MissingRawByteNameOnUnix_IsNotFound()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string directory = Directory.CreateTempSubdirectory("curl-rawbyte-").FullName;
        try
        {
            string path = Path.Combine(directory, RawByteName);
            diagnostics.Arrange("path", path);

            FileOpenResult result = await new PhysicalFileSystem().OpenForReadAsync(path, CancellationToken.None);

            diagnostics.Act("status", result.Status);
            Assert.AreEqual(FileAccessStatus.NotFound, result.Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static unsafe void CreateUnderRawBytes(byte[] name, byte[] content)
    {
        nint descriptor;
        fixed (byte* path = name)
        {
            descriptor = Open(path, CreateForWriteFlags, 0x180);
        }

        Assert.IsGreaterThanOrEqualTo((nint)0, descriptor, "the test could not create its raw-byte file");
        using FileStream stream = new(new SafeFileHandle(descriptor, ownsHandle: true), FileAccess.Write);
        stream.Write(content);
    }

    private static unsafe void DeleteUnderRawBytes(byte[] name)
    {
        fixed (byte* path = name)
        {
            _ = Unlink(path);
        }
    }

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Open")]
    private static unsafe partial nint Open(byte* path, int flags, int mode);

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Unlink")]
    private static unsafe partial int Unlink(byte* path);
}
