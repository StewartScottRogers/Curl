using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="DiskDataFileReader"/> through its injected file read and standard-input opener,
/// so no test touches the disk or the real standard input.
/// </summary>
[TestClass]
public sealed class DiskDataFileReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TryReadFile_Readable_ReturnsTheBytes()
    {
        Diagnostics.Arrange("file name", "body.txt");
        Diagnostics.Bytes("injected file contents", [1, 2, 3]);
        DiskDataFileReader reader = new(_ => [1, 2, 3], () => Stream.Null, NoModificationTime, true);

        bool read = reader.TryReadFile("body.txt", out byte[] contents);
        Diagnostics.Act("read", read);
        Diagnostics.Bytes("contents", contents);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Diff("contents", new byte[] { 1, 2, 3 }, contents);
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
        Diagnostics.Arrange("file name", "missing");
        Diagnostics.Arrange("injected failure", exceptionType.Name);
        Exception failure = (Exception)Activator.CreateInstance(exceptionType)!;
        DiskDataFileReader reader = new(_ => throw failure, () => Stream.Null, NoModificationTime, true);

        bool read = reader.TryReadFile("missing", out byte[] contents);
        Diagnostics.Act("read", read);
        Diagnostics.Bytes("contents", contents);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("contents length", 0, contents.Length);
        Assert.IsFalse(read);
        Assert.IsEmpty(contents);
    }

    [TestMethod]
    public void TryReadFile_UnexpectedFailure_IsNotSwallowed()
    {
        Diagnostics.Arrange("file name", "x");
        Diagnostics.Arrange("injected failure", nameof(InvalidOperationException));
        DiskDataFileReader reader = new(_ => throw new InvalidOperationException(), () => Stream.Null, NoModificationTime, true);

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(() => reader.TryReadFile("x", out _));
        Diagnostics.Act("thrown exception type", thrown.GetType().Name);

        Diagnostics.Assert("thrown exception type", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    [TestMethod]
    public void ReadStandardInput_Always_ReturnsEveryByteAndClosesTheStream()
    {
        MemoryStream standardInput = new([0x71, 0x20, 0x72, 0x0A]);
        Diagnostics.Arrange("standard input", Convert.ToHexString(standardInput.ToArray()));
        DiskDataFileReader reader = new(_ => [], () => standardInput, NoModificationTime, true);

        byte[] contents = reader.ReadStandardInput();
        Diagnostics.Bytes("contents", contents);
        Diagnostics.Act("standard input still readable", standardInput.CanRead);

        Diagnostics.Diff("contents", new byte[] { 0x71, 0x20, 0x72, 0x0A }, contents);
        Diagnostics.Assert("standard input still readable", false, standardInput.CanRead);
        CollectionAssert.AreEqual(new byte[] { 0x71, 0x20, 0x72, 0x0A }, contents);
        Assert.IsFalse(standardInput.CanRead);
    }

    [TestMethod]
    public void TryReadModificationTime_Readable_ReturnsTheTimeToTheWholeSecond()
    {
        Diagnostics.Arrange("injected last write time", new DateTime(2026, 9, 26, 21, 5, 24, 602, DateTimeKind.Utc).ToString("o"));
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => new DateTime(2026, 9, 26, 21, 5, 24, 602, DateTimeKind.Utc), true);

        bool read = ActReadModificationTime(reader, "CLAUDE.md", "CLAUDE.md", out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("modification time", new DateTimeOffset(2026, 9, 26, 21, 5, 24, TimeSpan.Zero).ToString("o"), modificationTime.ToString("o"));
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
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
        Diagnostics.Arrange("failure kind", failureKind);
        Diagnostics.Arrange("isWindows", true);
        Exception failure = LookupFailure(failureKind);
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw failure, true);

        bool read = ActReadModificationTime(reader, "x", "x", out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason ?? "<null>", failureReason ?? "<null>");
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
        Diagnostics.Arrange("failure kind", failureKind);
        Diagnostics.Arrange("isWindows", false);
        Exception failure = StatFailure(failureKind);
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw failure, false);

        bool read = ActReadModificationTime(reader, "x", "x", out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason, failureReason ?? "<null>");
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
        Diagnostics.Arrange("isWindows", false);

        bool read = ActReadModificationTime(
            DiskDataFileReader.ForPlatform(isWindows: false), Path.Combine(scratch.Path, relativePath), scratch.Relative(Path.Combine(scratch.Path, relativePath)), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason, failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_ForPlatformOffWindowsEmptyPath_IsNoSuchFileOrDirectory()
    {
        Diagnostics.Arrange("isWindows", false);

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: false), string.Empty, "<empty>", out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "No such file or directory", failureReason ?? "<null>");
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
        Diagnostics.Arrange("isWindows", false);
        Diagnostics.Arrange("expected unix seconds", expectedSeconds);

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: false), path, scratch.Relative(path), out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("unix seconds", expectedSeconds, modificationTime.ToUnixTimeSeconds());
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsTrue(read);
        Assert.AreEqual(expectedSeconds, modificationTime.ToUnixTimeSeconds());
        Assert.IsNull(failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: <c>stat</c> follows a symbolic link, so a
    /// dangling one fails as <c>No such file or directory</c>, one whose target runs through a file as
    /// <c>Not a directory</c>, and a loop as <c>Too many levels of symbolic links</c> (ADR-0090).
    /// </summary>
    /// <param name="linkTarget">What the link resolves to, under a scratch directory holding the file <c>file</c>, or <c>loop</c>.</param>
    /// <param name="expectedReason">The reason reported.</param>
    [TestMethod]
    [DataRow("nothing", "No such file or directory")]
    [DataRow("file/x", "Not a directory")]
    [DataRow("loop", "Too many levels of symbolic links")]
    public void TryReadModificationTime_ForStatLinkThatCannotBeFollowed_ReportsCurlsStatReason(string linkTarget, string expectedReason)
    {
        using ScratchDirectory scratch = new();
        Diagnostics.Arrange("link target", linkTarget);
        DiskDataFileReader reader = DiskDataFileReader.ForStatFollowingLinksWith(_ => linkTarget == "loop"
            ? throw new IOException("Too many levels of symbolic links in '/tmp/loop1'.")
            : new FileInfo(Path.Combine(scratch.Path, linkTarget)));

        bool read = ActReadModificationTime(reader, Path.Combine(scratch.Path, "file"), scratch.Relative(Path.Combine(scratch.Path, "file")), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason, failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_ForStatLinkFailingOtherwise_ReportsThatFailure()
    {
        using ScratchDirectory scratch = new();
        Diagnostics.Arrange("injected failure", nameof(UnauthorizedAccessException));
        DiskDataFileReader reader = DiskDataFileReader.ForStatFollowingLinksWith(_ => throw new UnauthorizedAccessException());

        bool read = ActReadModificationTime(reader, Path.Combine(scratch.Path, "file"), scratch.Relative(Path.Combine(scratch.Path, "file")), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "Permission denied", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual("Permission denied", failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: a link to an existing file has the target's
    /// time; so does a link to a directory, which <see cref="File.ResolveLinkTarget(string, bool)"/>
    /// returns as a <see cref="FileInfo"/>.
    /// </summary>
    /// <param name="targetName">The link's target under a scratch directory holding the file <c>file</c> and the directory <c>dir</c>.</param>
    [TestMethod]
    [DataRow("file")]
    [DataRow("dir")]
    public void TryReadModificationTime_ForStatLinkToExistingTarget_IsTheTargetsLastWriteTime(string targetName)
    {
        using ScratchDirectory scratch = new();
        string target = Path.Combine(scratch.Path, targetName);
        DateTime targetTime = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        Directory.SetLastWriteTimeUtc(target, targetTime);
        Diagnostics.Arrange("link target", scratch.Relative(target));
        Diagnostics.Arrange("target last write time", targetTime.ToString("o"));
        DiskDataFileReader reader = DiskDataFileReader.ForStatFollowingLinksWith(_ => new FileInfo(target));

        bool read = ActReadModificationTime(reader, Path.Combine(scratch.Path, "file"), scratch.Relative(Path.Combine(scratch.Path, "file")), out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("modification time", new DateTimeOffset(targetTime).ToString("o"), modificationTime.ToString("o"));
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsTrue(read);
        Assert.AreEqual(new DateTimeOffset(targetTime), modificationTime);
        Assert.IsNull(failureReason);
    }

    /// <summary>
    /// curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: <c>ln -s nothing dangling</c> and
    /// <c>ln -s loop1 loop2; ln -s loop2 loop1</c>, relative targets as measured.
    /// </summary>
    /// <param name="linkName">The link after <c>-z</c>.</param>
    /// <param name="expectedReason">The reason curl reports.</param>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    [DataRow("dangling", "No such file or directory")]
    [DataRow("loop1", "Too many levels of symbolic links")]
    public void TryReadModificationTime_ForPlatformOffWindowsUnfollowableLink_ReportsCurlsStatReason(string linkName, string expectedReason)
    {
        using ScratchDirectory scratch = new();
        Diagnostics.Arrange("links", "dangling -> nothing; loop2 -> loop1; loop1 -> loop2");
        File.CreateSymbolicLink(Path.Combine(scratch.Path, "dangling"), "nothing");
        File.CreateSymbolicLink(Path.Combine(scratch.Path, "loop2"), "loop1");
        File.CreateSymbolicLink(Path.Combine(scratch.Path, "loop1"), "loop2");

        bool read = ActReadModificationTime(
            DiskDataFileReader.ForPlatform(isWindows: false), Path.Combine(scratch.Path, linkName), scratch.Relative(Path.Combine(scratch.Path, linkName)), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason, failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    /// <summary>curl 8.18.0 (OpenSSL build, Ubuntu), 2026-09-27: <c>ln -s f goodlink</c> prints nothing and uses the target's time.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public void TryReadModificationTime_ForPlatformOffWindowsLinkToExistingFile_IsTheTargetsLastWriteTime()
    {
        using ScratchDirectory scratch = new();
        DateTime targetTime = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(scratch.Path, "file"), targetTime);
        string link = Path.Combine(scratch.Path, "goodlink");
        File.CreateSymbolicLink(link, "file");
        Diagnostics.Arrange("link", "goodlink -> file");
        Diagnostics.Arrange("target last write time", targetTime.ToString("o"));

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: false), link, scratch.Relative(link), out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("modification time", new DateTimeOffset(targetTime).ToString("o"), modificationTime.ToString("o"));
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsTrue(read);
        Assert.AreEqual(new DateTimeOffset(targetTime), modificationTime);
        Assert.IsNull(failureReason);
    }

    /// <summary>curl 8.21.0 on Windows, 2026-09-26: <c>-z &lt;a directory&gt;</c> reports 0x00000005.</summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryReadModificationTime_ForPlatformWindowsDirectory_IsAccessDenied()
    {
        using ScratchDirectory scratch = new();
        Diagnostics.Arrange("isWindows", true);

        bool read = ActReadModificationTime(
            DiskDataFileReader.ForPlatform(isWindows: true), Path.Combine(scratch.Path, "dir"), scratch.Relative(Path.Combine(scratch.Path, "dir")), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "CreateFile failed: GetLastError 0x00000005", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual("CreateFile failed: GetLastError 0x00000005", failureReason);
    }

    /// <summary>
    /// curl 8.21.0 (Schannel build) on Windows, 2026-09-26: <c>-z con</c> fails in <c>CreateFile</c>
    /// and <c>-z nul</c> in <c>GetFileTime</c>, both with 0x00000057.
    /// </summary>
    /// <param name="device">The DOS device named after <c>-z</c>.</param>
    /// <param name="expectedReason">The reason curl reports.</param>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("con", "CreateFile failed: GetLastError 0x00000057")]
    [DataRow("nul", "GetFileTime failed: GetLastError 0x00000057")]
    public void TryReadModificationTime_ForPlatformWindowsDosDevice_ReportsTheCallThatFailed(string device, string expectedReason)
    {
        Diagnostics.Arrange("isWindows", true);

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: true), device, device, out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason, failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    /// <summary>
    /// curl 8.21.0 on Windows, 2026-09-26 (ADR-0037): <c>-z ""</c>, <c>-z nodir/x</c> and
    /// <c>-z &lt;file&gt;/x</c> report 0x00000003, <c>-z "x*y"</c> 0x0000007b, and a missing file nothing.
    /// </summary>
    /// <param name="relativePath">The value after <c>-z</c>, under a scratch directory unless empty.</param>
    /// <param name="expectedReason">The reason curl reports, or <see langword="null"/> for none.</param>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("", "CreateFile failed: GetLastError 0x00000003")]
    [DataRow("nodir/x", "CreateFile failed: GetLastError 0x00000003")]
    [DataRow("file/x", "CreateFile failed: GetLastError 0x00000003")]
    [DataRow("x*y", "CreateFile failed: GetLastError 0x0000007b")]
    [DataRow("missing", null)]
    public void TryReadModificationTime_ForPlatformWindowsUnreadable_ReportsCurlsReason(string relativePath, string? expectedReason)
    {
        using ScratchDirectory scratch = new();
        string path = relativePath.Length == 0 ? relativePath : Path.Combine(scratch.Path, relativePath);
        Diagnostics.Arrange("isWindows", true);

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: true), path, scratch.Relative(path), out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", expectedReason ?? "<null>", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual(expectedReason, failureReason);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryReadModificationTime_ForPlatformWindowsFile_IsItsLastWriteTime()
    {
        using ScratchDirectory scratch = new();
        string path = Path.Combine(scratch.Path, "file");
        DateTimeOffset lastWrite = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        Diagnostics.Arrange("isWindows", true);
        Diagnostics.Arrange("last write time", lastWrite.ToString("o"));

        bool read = ActReadModificationTime(DiskDataFileReader.ForPlatform(isWindows: true), path, scratch.Relative(path), out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("modification time", DateTimeOffset.FromUnixTimeSeconds(lastWrite.ToUnixTimeSeconds()).ToString("o"), modificationTime.ToString("o"));
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsTrue(read);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(lastWrite.ToUnixTimeSeconds()), modificationTime);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    public void TryReadModificationTime_LookupFailedInANamedCall_NamesThatCall()
    {
        Diagnostics.Arrange("injected failure", "FileTimeLookupException(GetFileTime, 0x57)");
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw new FileTimeLookupException("GetFileTime", 0x57), true);

        bool read = ActReadModificationTime(reader, "x", "x", out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "GetFileTime failed: GetLastError 0x00000057", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual("GetFileTime failed: GetLastError 0x00000057", failureReason);
    }

    [TestMethod]
    public void FileTimeLookupException_Always_CarriesTheCallAndTheWindowsErrorCode()
    {
        Diagnostics.Arrange("failed call", "CreateFile");
        Diagnostics.Arrange("error code", 0x20);

        FileTimeLookupException exception = new("CreateFile", 0x20);
        Diagnostics.Act("failed call", exception.FailedCall);
        Diagnostics.Act("error code", exception.ErrorCode);
        Diagnostics.Act("HResult", exception.HResult);
        Diagnostics.Act("message", exception.Message);

        Diagnostics.Assert("failed call", "CreateFile", exception.FailedCall);
        Diagnostics.Assert("error code", 0x20, exception.ErrorCode);
        Diagnostics.Assert("HResult", unchecked((int)0x80070020), exception.HResult);
        Diagnostics.Assert("message", "CreateFile failed: GetLastError 0x00000020", exception.Message);
        Assert.AreEqual("CreateFile", exception.FailedCall);
        Assert.AreEqual(0x20, exception.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80070020), exception.HResult);
        Assert.AreEqual("CreateFile failed: GetLastError 0x00000020", exception.Message);
    }

    [TestMethod]
    public void TryReadModificationTime_UnexpectedFailure_IsNotSwallowed()
    {
        Diagnostics.Arrange("path", "x");
        Diagnostics.Arrange("injected failure", nameof(InvalidOperationException));
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw new InvalidOperationException(), true);

        InvalidOperationException thrown = Assert.ThrowsExactly<InvalidOperationException>(() => reader.TryReadModificationTime("x", out _, out _));
        Diagnostics.Act("thrown exception type", thrown.GetType().Name);

        Diagnostics.Assert("thrown exception type", nameof(InvalidOperationException), thrown.GetType().Name);
    }

    [TestMethod]
    public void TryReadModificationTime_ForProcessExistingFile_IsItsLastWriteTimeToTheWholeSecond()
    {
        string path = typeof(DiskDataFileReaderTests).Assembly.Location;
        DateTimeOffset lastWrite = new(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        Diagnostics.Arrange("last write time", lastWrite.ToString("o"));

        bool read = ActReadModificationTime(DiskDataFileReader.ForProcess, path, "<test assembly>/" + Path.GetFileName(path), out DateTimeOffset modificationTime, out string? failureReason);

        Diagnostics.Assert("read", true, read);
        Diagnostics.Assert("modification time", DateTimeOffset.FromUnixTimeSeconds(lastWrite.ToUnixTimeSeconds()).ToString("o"), modificationTime.ToString("o"));
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsTrue(read);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(lastWrite.ToUnixTimeSeconds()), modificationTime);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryReadModificationTime_ForProcessMissingFileOnWindows_IsFileNotFoundWithNoReason()
    {
        Diagnostics.Arrange("platform", "Windows");

        bool read = ActReadModificationTime(
            DiskDataFileReader.ForProcess, Path.Combine(AppContext.BaseDirectory, "no-such-file.bl246"), "<base>/no-such-file.bl246", out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "<null>", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.IsNull(failureReason);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TryReadModificationTime_ForProcessMissingFileOffWindows_IsNoSuchFileOrDirectory()
    {
        Diagnostics.Arrange("platform", "not Windows");

        bool read = ActReadModificationTime(
            DiskDataFileReader.ForProcess, Path.Combine(AppContext.BaseDirectory, "no-such-file.bl246"), "<base>/no-such-file.bl246", out _, out string? failureReason);

        Diagnostics.Assert("read", false, read);
        Diagnostics.Assert("failure reason", "No such file or directory", failureReason ?? "<null>");
        Assert.IsFalse(read);
        Assert.AreEqual("No such file or directory", failureReason);
    }

    [TestMethod]
    public void ForProcess_Always_IsTheSameReader()
    {
        Diagnostics.Arrange("reader", "DiskDataFileReader.ForProcess read twice");
        var first = DiskDataFileReader.ForProcess;
        var second = DiskDataFileReader.ForProcess;
        Diagnostics.Act("same instance", ReferenceEquals(first, second));
        Diagnostics.Assert("same instance", true, ReferenceEquals(first, second));
        Assert.AreSame(first, second);
    }

    /// <summary>Reads a modification time and writes the shown path (ARRANGE) and the three results (ACT); the shown path hides the temporary folder.</summary>
    private bool ActReadModificationTime(DiskDataFileReader reader, string path, string shownPath, out DateTimeOffset modificationTime, out string? failureReason)
    {
        Diagnostics.Arrange("path", shownPath);
        bool read = reader.TryReadModificationTime(path, out modificationTime, out failureReason);
        Diagnostics.Act("read", read);
        Diagnostics.Act("modification time", modificationTime.ToString("o"));
        Diagnostics.Act("failure reason", failureReason ?? "<null>");
        return read;
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

        /// <summary>The path with the scratch directory replaced by <c>&lt;temp&gt;</c> and separators written as <c>/</c>, so it reads the same on every platform.</summary>
        public string Relative(string fullPath) => fullPath.Replace(Path, "<temp>", StringComparison.Ordinal).Replace('\\', '/');

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
