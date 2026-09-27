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

    [TestMethod]
    public void TryReadModificationTime_UnreadableWithoutWindowsErrors_ReportsNoReason()
    {
        DiskDataFileReader reader = new(_ => [], () => Stream.Null, _ => throw new UnauthorizedAccessException(), false);

        bool read = reader.TryReadModificationTime("x", out _, out string? failureReason);

        Assert.IsFalse(read);
        Assert.IsNull(failureReason);
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
}
