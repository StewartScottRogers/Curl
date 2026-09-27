namespace Curl.Cli;

/// <summary>
/// Pins <see cref="DiskDataFileReader"/> through its injected file read and standard-input opener,
/// so no test touches the disk or the real standard input.
/// </summary>
[TestClass]
public sealed class DiskDataFileReaderTests
{
    [TestMethod]
    public void TryReadFile_Readable_ReturnsTheBytes()
    {
        DiskDataFileReader reader = new(_ => [1, 2, 3], () => Stream.Null, NoModificationTime, true);

        bool read = reader.TryReadFile("body.txt", out byte[] contents);

        Assert.IsTrue(read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, contents);
    }

    [TestMethod]
    [DataRow(typeof(FileNotFoundException))]
    [DataRow(typeof(DirectoryNotFoundException))]
    [DataRow(typeof(UnauthorizedAccessException))]
    [DataRow(typeof(ArgumentException))]
    [DataRow(typeof(NotSupportedException))]
    public void TryReadFile_Unreadable_ReturnsFalseAndNoBytes(Type exceptionType)
    {
        Exception failure = (Exception)Activator.CreateInstance(exceptionType)!;
        DiskDataFileReader reader = new(_ => throw failure, () => Stream.Null, NoModificationTime, true);

        bool read = reader.TryReadFile("missing", out byte[] contents);

        Assert.IsFalse(read);
        Assert.IsEmpty(contents);
    }

    [TestMethod]
    public void TryReadFile_UnexpectedFailure_IsNotSwallowed()
    {
        DiskDataFileReader reader = new(_ => throw new InvalidOperationException(), () => Stream.Null, NoModificationTime, true);

        Assert.ThrowsExactly<InvalidOperationException>(() => reader.TryReadFile("x", out _));
    }

    [TestMethod]
    public void ReadStandardInput_Always_ReturnsEveryByteAndClosesTheStream()
    {
        MemoryStream standardInput = new([0x71, 0x20, 0x72, 0x0A]);
        DiskDataFileReader reader = new(_ => [], () => standardInput, NoModificationTime, true);

        byte[] contents = reader.ReadStandardInput();

        CollectionAssert.AreEqual(new byte[] { 0x71, 0x20, 0x72, 0x0A }, contents);
        Assert.IsFalse(standardInput.CanRead);
    }

    [TestMethod]
    public void TryReadModificationTime_Readable_ReturnsTheTimeToTheWholeSecond()
    {
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => new DateTime(2026, 9, 26, 21, 5, 24, 602, DateTimeKind.Utc), true);

        bool read = reader.TryReadModificationTime("CLAUDE.md", out DateTimeOffset modificationTime, out string? failureReason);

        Assert.IsTrue(read);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 26, 21, 5, 24, TimeSpan.Zero), modificationTime);
        Assert.IsNull(failureReason);
    }

    /// <summary>
    /// curl 8.21.0 on Windows, 2026-09-26: <c>-z nodir/x</c> reports 0x00000003, <c>-z &lt;a directory&gt;</c>
    /// 0x00000005, <c>-z C:/pagefile.sys</c> 0x00000020, <c>-z "x*y"</c> 0x0000007b and <c>-z ""</c>
    /// 0x00000003; <c>-z notadate</c>, file not found, reports nothing.
    /// </summary>
    /// <param name="failureKind">Which failure the injected lookup throws.</param>
    /// <param name="expectedReason">The reason reported, or <see langword="null"/> for none.</param>
    [TestMethod]
    [DataRow("FileNotFound", null)]
    [DataRow("DirectoryNotFound", "CreateFile failed: GetLastError 0x00000003")]
    [DataRow("AccessDenied", "CreateFile failed: GetLastError 0x00000005")]
    [DataRow("SharingViolation", "CreateFile failed: GetLastError 0x00000020")]
    [DataRow("InvalidName", "CreateFile failed: GetLastError 0x0000007b")]
    [DataRow("EmptyPath", "CreateFile failed: GetLastError 0x00000003")]
    [DataRow("NoWindowsErrorCode", "CreateFile failed: GetLastError 0x00000003")]
    public void TryReadModificationTime_Unreadable_ReportsCurlsWindowsReason(string failureKind, string? expectedReason)
    {
        Exception failure = LookupFailure(failureKind);
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw failure, true);

        bool read = reader.TryReadModificationTime("x", out DateTimeOffset modificationTime, out string? failureReason);

        Assert.IsFalse(read);
        Assert.AreEqual(default, modificationTime);
        Assert.AreEqual(expectedReason, failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27, whose <c>getfiletime</c> is unchanged in 8.21.0:
    /// <c>-z nodir/x</c>, <c>-z missing</c> and <c>-z ""</c> report <c>No such file or directory</c>,
    /// <c>-z file/x</c> <c>Not a directory</c>, <c>-z noaccess/x</c> <c>Permission denied</c>, a
    /// 300-character name <c>File name too long</c> and a symbolic-link loop
    /// <c>Too many levels of symbolic links</c>.
    /// </summary>
    /// <param name="failureKind">Which failure the injected lookup throws.</param>
    /// <param name="expectedReason">The reason reported.</param>
    [TestMethod]
    [DataRow("FileNotFound", "No such file or directory")]
    [DataRow("DirectoryNotFound", "No such file or directory")]
    [DataRow("EmptyPath", "No such file or directory")]
    [DataRow("AccessDenied", "Permission denied")]
    [DataRow("NameTooLong", "File name too long")]
    [DataRow("NotADirectory", "Not a directory")]
    [DataRow("SymbolicLinkLoop", "Too many levels of symbolic links")]
    [DataRow("NotSupported", "No such file or directory")]
    public void TryReadModificationTime_UnreadableWithoutWindowsErrors_ReportsCurlsStatReason(string failureKind, string expectedReason)
    {
        Exception failure = StatFailure(failureKind);
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw failure, false);

        bool read = reader.TryReadModificationTime("x", out DateTimeOffset modificationTime, out string? failureReason);

        Assert.IsFalse(read);
        Assert.AreEqual(default, modificationTime);
        Assert.AreEqual(expectedReason, failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: <c>stat</c> fails for a path through a file,
    /// with or without a trailing separator, and for a missing one.
    /// </summary>
    /// <param name="relativePath">The path under a scratch directory holding the file <c>file</c>.</param>
    /// <param name="expectedReason">The reason reported.</param>
    [TestMethod]
    [DataRow("file/x", "Not a directory")]
    [DataRow("file/x/y", "Not a directory")]
    [DataRow("file/", "Not a directory")]
    [DataRow("nodir/x", "No such file or directory")]
    [DataRow("missing", "No such file or directory")]
    public void TryReadModificationTime_ForPlatformOffWindowsUnreadable_ReportsCurlsStatReason(string relativePath, string expectedReason)
    {
        using ScratchDirectory scratch = new();

        bool read = DiskDataFileReader.ForPlatform(isWindows: false).TryReadModificationTime(
            Path.Combine(scratch.Path, relativePath), out _, out string? failureReason);

        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_ForPlatformOffWindowsEmptyPath_IsNoSuchFileOrDirectory()
    {
        bool read = DiskDataFileReader.ForPlatform(isWindows: false).TryReadModificationTime(string.Empty, out _, out string? failureReason);

        Assert.IsFalse(read);
        Assert.AreEqual("No such file or directory", failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: <c>-z &lt;a directory&gt;</c>, with or without a
    /// trailing separator, and <c>-z &lt;a file&gt;</c> print nothing: <c>stat</c> reads their times.
    /// </summary>
    /// <param name="relativePath">The path under a scratch directory holding the file <c>file</c> and the directory <c>dir</c>.</param>
    [TestMethod]
    [DataRow("dir")]
    [DataRow("dir/")]
    [DataRow("file")]
    public void TryReadModificationTime_ForPlatformOffWindowsFileOrDirectory_IsItsLastWriteTime(string relativePath)
    {
        using ScratchDirectory scratch = new();
        string path = Path.Combine(scratch.Path, relativePath);
        long expectedSeconds = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero).ToUnixTimeSeconds();

        bool read = DiskDataFileReader.ForPlatform(isWindows: false).TryReadModificationTime(path, out DateTimeOffset modificationTime, out string? failureReason);

        Assert.IsTrue(read);
        Assert.AreEqual(expectedSeconds, modificationTime.ToUnixTimeSeconds());
        Assert.IsNull(failureReason);
    }

    /// <summary>curl 8.21.0 on Windows, 2026-09-26: <c>-z &lt;a directory&gt;</c> reports 0x00000005.</summary>
    [TestMethod]
    public void TryReadModificationTime_ForPlatformWindowsDirectory_IsAccessDenied()
    {
        using ScratchDirectory scratch = new();

        bool read = DiskDataFileReader.ForPlatform(isWindows: true).TryReadModificationTime(
            Path.Combine(scratch.Path, "dir"), out _, out string? failureReason);

        Assert.IsFalse(read);
        Assert.AreEqual("CreateFile failed: GetLastError 0x00000005", failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_UnexpectedFailure_IsNotSwallowed()
    {
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw new InvalidOperationException(), true);

        Assert.ThrowsExactly<InvalidOperationException>(() => reader.TryReadModificationTime("x", out _, out _));
    }

    [TestMethod]
    public void TryReadModificationTime_ForProcessExistingFile_IsItsLastWriteTimeToTheWholeSecond()
    {
        string path = typeof(DiskDataFileReaderTests).Assembly.Location;
        DateTimeOffset lastWrite = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

        bool read = DiskDataFileReader.ForProcess.TryReadModificationTime(path, out DateTimeOffset modificationTime, out string? failureReason);

        Assert.IsTrue(read);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(lastWrite.ToUnixTimeSeconds()), modificationTime);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_ForProcessMissingFile_IsFileNotFound()
    {
        bool read = DiskDataFileReader.ForProcess.TryReadModificationTime(
            Path.Combine(AppContext.BaseDirectory, "no-such-file.bl246"), out _, out string? failureReason);

        Assert.IsFalse(read);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    public void ForProcess_Always_IsTheSameReader()
    {
        var first = DiskDataFileReader.ForProcess;
        var second = DiskDataFileReader.ForProcess;
        Assert.AreSame(first, second);
    }

    private static DateTime NoModificationTime(string path) => throw new FileNotFoundException(null, path);

    private static Exception LookupFailure(string failureKind) => failureKind switch
    {
        "FileNotFound" => new FileNotFoundException(),
        "DirectoryNotFound" => new DirectoryNotFoundException(),
        "AccessDenied" => new UnauthorizedAccessException(),
        "SharingViolation" => new IOException(null, unchecked((int)0x80070020)),
        "InvalidName" => new IOException(null, unchecked((int)0x8007007B)),
        "EmptyPath" => new ArgumentException(),
        _ => new IOException(),
    };

    private static Exception StatFailure(string failureKind) => failureKind switch
    {
        "FileNotFound" => new FileNotFoundException(),
        "DirectoryNotFound" => new DirectoryNotFoundException(),
        "EmptyPath" => new ArgumentException(),
        "AccessDenied" => new UnauthorizedAccessException(),
        "NameTooLong" => new PathTooLongException(),
        "NotADirectory" => new IOException("Not a directory"),
        "SymbolicLinkLoop" => new IOException("Too many levels of symbolic links : '/tmp/loop1'", 40),
        _ => new NotSupportedException(),
    };

    /// <summary>A directory under the temporary folder holding the file <c>file</c> and the directory <c>dir</c>, deleted on dispose.</summary>
    private sealed class ScratchDirectory : IDisposable
    {
        public ScratchDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bl288-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "dir"));
            File.WriteAllBytes(System.IO.Path.Combine(Path, "file"), [0x78]);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
