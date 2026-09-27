using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// The part of <c>CookieStoreTests</c> that pins <see cref="CookieStore"/>'s <c>-b</c> strings and its cookie-file input and output to curl 8.21.0
/// (<c>x86_64-w64-mingw32</c>, Schannel, libpsl), measured on 2026-09-26 with <c>Record-CurlExchange.ps1</c>;
/// BL-221's Notes record each command.
/// </summary>
public sealed partial class CookieStoreTests
{
    /// <summary>Measured: <c>curl -b 'x=1; y=2'</c> sends <c>Cookie: x=1; y=2</c>.</summary>
    [TestMethod]
    public void GetCookieHeader_CookieStringOnly_SendsItVerbatim()
    {
        CookieStore store = new();
        store.AddCookieString("x=1; y=2");

        Assert.AreEqual("x=1; y=2", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>Measured: <c>curl -b '  spaced = v ;;'</c> sends the string untouched.</summary>
    [TestMethod]
    public void GetCookieHeader_CookieStringWithSpaces_SendsItUntouched()
    {
        CookieStore store = new();
        store.AddCookieString("  spaced = v ;;");

        Assert.AreEqual("  spaced = v ;;", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>
    /// Measured: <c>curl -b x=1 -b z.txt -b w=3</c>, <c>z.txt</c> holding <c>z=1</c>, sends
    /// <c>Cookie: z=1; x=1; w=3</c>: stored cookies first, then each string in order.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_StoredCookiesAndStrings_SendsStoredCookiesFirst()
    {
        CookieStore store = new();
        store.AddCookieString("x=1");
        store.LoadCookieFile(new StringReader("127.0.0.1\tFALSE\t/\tFALSE\t0\tz\t1\n"), discardSessionCookies: false, Now);
        store.AddCookieString("w=3");

        Assert.AreEqual("z=1; x=1; w=3", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>
    /// Measured: <c>curl -b long.txt -b x=1</c> (<c>aaa</c> and <c>bb</c> of 4000 characters, <c>c</c> of 165,
    /// <c>dd=1</c>) leaves <c>c</c> out and the string with it; <c>-b long2.txt</c> (<c>aaa</c> and <c>bb</c>)
    /// with a 402-character string sends the string past the 8183-character limit.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_CookieStringAfterTheLimit_IsSentOnlyWhenNoCookieWasLeftOut()
    {
        string x4000 = new('x', 4000);
        string q400 = new('q', 400);
        CookieStore capped = new();
        capped.LoadCookieFile(
            new StringReader($"127.0.0.1\tFALSE\t/\tFALSE\t0\taaa\t{x4000}\n127.0.0.1\tFALSE\t/\tFALSE\t0\tbb\t{x4000}\n127.0.0.1\tFALSE\t/\tFALSE\t0\tc\t{new string('x', 165)}\n127.0.0.1\tFALSE\t/\tFALSE\t0\tdd\t1\n"),
            discardSessionCookies: false,
            Now);
        capped.AddCookieString("x=1");
        CookieStore uncapped = new();
        uncapped.LoadCookieFile(
            new StringReader($"127.0.0.1\tFALSE\t/\tFALSE\t0\taaa\t{x4000}\n127.0.0.1\tFALSE\t/\tFALSE\t0\tbb\t{x4000}\n"),
            discardSessionCookies: false,
            Now);
        uncapped.AddCookieString("y=" + q400);

        Assert.AreEqual($"aaa={x4000}; dd=1; bb={x4000}", capped.GetCookieHeader(Loopback, secure: false, Now));
        Assert.AreEqual($"aaa={x4000}; bb={x4000}; y={q400}", uncapped.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>Measured: <c>curl -b x=1 -c s5jar.txt</c>, the response setting <c>a=1</c>, writes only <c>a</c> to the jar.</summary>
    [TestMethod]
    public void WriteCookieJar_CookieString_IsNotWritten()
    {
        CookieStore store = new();
        store.AddCookieString("x=1");
        store.StoreFromResponse(Loopback, ["a=1"], Now);
        StringWriter writer = new() { NewLine = "\n" };

        store.WriteCookieJar(writer, Now);

        Assert.EndsWith("\n\n127.0.0.1\tFALSE\t/\tFALSE\t0\ta\t1\n", writer.ToString());
    }

    [TestMethod]
    public async Task LoadCookieFileAsync_ExistingFile_LoadsItsBytesAsLatin1()
    {
        FakeFileSystem fileSystem = new() { ReadContent = Encoding.Latin1.GetBytes("127.0.0.1\tFALSE\t/\tFALSE\t0\tc\taéb\n") };
        CookieStore store = new();

        await store.LoadCookieFileAsync(fileSystem, "in.txt", discardSessionCookies: false, Now, CancellationToken.None);

        Assert.AreEqual("in.txt", fileSystem.OpenedPath);
        Assert.AreEqual("c=aéb", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>Measured with BL-220's <c>-b none.txt</c>: a cookie file that does not exist loads nothing, silently.</summary>
    [TestMethod]
    public async Task LoadCookieFileAsync_MissingFile_LoadsNothing()
    {
        CookieStore store = new();

        await store.LoadCookieFileAsync(new FakeFileSystem(), "none.txt", discardSessionCookies: false, Now, CancellationToken.None);

        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    public async Task SaveCookieJarAsync_WritableFile_WritesTheJarWithThePlatformLineEnding()
    {
        MemoryStream written = new();
        FakeFileSystem fileSystem = new() { WriteTarget = written };
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["a=é"], Now);

        bool saved = await store.SaveCookieJarAsync(fileSystem, "jar.txt", Now, CancellationToken.None);

        string newLine = Environment.NewLine;
        string expected = string.Join(newLine, NetscapeCookieFile.HeaderLines) + newLine + "127.0.0.1\tFALSE\t/\tFALSE\t0\ta\té" + newLine;
        Assert.IsTrue(saved);
        Assert.AreEqual("jar.txt", fileSystem.OpenedPath);
        Assert.AreEqual(CookieStore.JarCreateMode, fileSystem.CreateMode);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(expected), written.ToArray());
    }

    /// <summary>
    /// Measured: <c>curl -c nodir\x.txt</c> and <c>curl -sS -c adir</c> (a directory), with <c>-s</c>,
    /// <c>-v</c> and <c>--trace-ascii</c>, print nothing about the jar and exit 0. Saving reports the failure
    /// only through its result.
    /// </summary>
    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.IsDirectory)]
    [DataRow(FileAccessStatus.AccessDenied)]
    public async Task SaveCookieJarAsync_UnwritablePath_ReturnsFalseSilently(FileAccessStatus status)
    {
        FakeFileSystem fileSystem = new() { WriteFailure = status };
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["a=1"], Now);

        Assert.IsFalse(await store.SaveCookieJarAsync(fileSystem, "nodir\\x.txt", Now, CancellationToken.None));
    }

    [TestMethod]
    public async Task SaveCookieJarAsync_WriteFails_ReturnsFalseSilently()
    {
        FakeFileSystem fileSystem = new() { WriteTarget = new FailingStream() };

        Assert.IsFalse(await new CookieStore().SaveCookieJarAsync(fileSystem, "jar.txt", Now, CancellationToken.None));
    }

    [TestMethod]
    public async Task NullArguments_Throw()
    {
        CookieStore store = new();
        FakeFileSystem fileSystem = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => store.AddCookieString(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.LoadCookieFile(null!, discardSessionCookies: false, Now));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.WriteCookieJar(null!, Now));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.LoadCookieFileAsync(null!, "p", false, Now, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.LoadCookieFileAsync(fileSystem, null!, false, Now, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SaveCookieJarAsync(null!, "p", Now, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => store.SaveCookieJarAsync(fileSystem, null!, Now, CancellationToken.None));
    }

    /// <summary>An <see cref="IFileSystem"/> that serves one file to read and one stream to write, or fails.</summary>
    private sealed class FakeFileSystem : IFileSystem
    {
        public byte[]? ReadContent { get; init; }

        public Stream? WriteTarget { get; init; }

        public FileAccessStatus WriteFailure { get; init; } = FileAccessStatus.AccessDenied;

        public string? OpenedPath { get; private set; }

        public UnixFileMode CreateMode { get; private set; }

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            OpenedPath = path;
            return ValueTask.FromResult(
                ReadContent is null
                    ? FileOpenResult.Failed(FileAccessStatus.NotFound)
                    : FileOpenResult.Opened(new MemoryStream(ReadContent), ReadContent.Length, null));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
        {
            Assert.AreEqual(FileWriteMode.Truncate, mode);
            OpenedPath = path;
            CreateMode = createMode;
            return ValueTask.FromResult(
                WriteTarget is null ? FileOpenResult.Failed(WriteFailure) : FileOpenResult.Opened(WriteTarget, 0, null));
        }
    }

    /// <summary>A stream whose every write fails, as a full disk does.</summary>
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("There is not enough space on the disk."));
    }
}
