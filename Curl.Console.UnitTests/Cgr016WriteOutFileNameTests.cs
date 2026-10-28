using System.Diagnostics.CodeAnalysis;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins CGR-016's <c>%output{}</c> part (BL-3863): off Windows a <c>-w</c> output file name holding
/// an argument byte that is not valid UTF-8 opens under its raw bytes, as curl 8.18.0 on Linux
/// creates <c>%output{\xff.txt}x</c> as FF 2E 74 78 74 holding <c>x</c>; every other name, and every
/// name on Windows, opens through <see cref="FileStream" /> as before. Each test chooses the platform
/// it plays, so all of them run on every platform.
/// </summary>
[TestClass]
public sealed class Cgr016WriteOutFileNameTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "curl-cgr016-write-out-" + Guid.NewGuid().ToString("N"));

    private readonly List<(byte[] Path, FileWriteMode Mode, UnixFileMode CreateMode)> rawOpens = [];

    private readonly MemoryStream rawFile = new();

    private bool rawOpenFails;

    [TestInitialize]
    public void CreateRoot() => Directory.CreateDirectory(root);

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public async Task RenderAsync_EscapedByteOffWindows_WritesUnderTheRawBytes()
    {
        WriteOutTemplateRenderer renderer = new(Opener(writesLineFeedAsCrLf: false, isWindows: false), false, WriteOutTimeDialect.Glibc, TimeProvider.System);

        await renderer.RenderAsync("%output{\uDCFF.txt}x", new NoVariables(), new MemoryStream(), new MemoryStream());

        Assert.HasCount(1, rawOpens);
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x2E, 0x74, 0x78, 0x74, 0x00 }, rawOpens[0].Path);
        Assert.AreEqual(FileWriteMode.Truncate, rawOpens[0].Mode);
        Assert.AreEqual(DiskWriteOutFileOpener.CreateMode, rawOpens[0].CreateMode);
        CollectionAssert.AreEqual("x"u8.ToArray(), rawFile.ToArray());
    }

    [TestMethod]
    public void TryOpen_AppendEscapedByteOffWindows_OpensTheRawBytesForAppending()
    {
        Assert.IsTrue(Opener(writesLineFeedAsCrLf: false, isWindows: false).TryOpen("d/\uDC80", append: true, out Stream? stream));

        Assert.AreSame(rawFile, stream);
        CollectionAssert.AreEqual(new byte[] { 0x64, 0x2F, 0x80, 0x00 }, rawOpens[0].Path);
        Assert.AreEqual(FileWriteMode.Append, rawOpens[0].Mode);
    }

    [TestMethod]
    public void TryOpen_RawOpenFails_RefusesTheFile()
    {
        rawOpenFails = true;

        Assert.IsFalse(Opener(writesLineFeedAsCrLf: false, isWindows: false).TryOpen("\uDCFF.txt", append: false, out Stream? stream));

        Assert.IsNull(stream);
        Assert.HasCount(1, rawOpens);
    }

    [TestMethod]
    public void TryOpen_RawOpenInTextMode_WritesLineFeedsAsCrLf()
    {
        Assert.IsTrue(Opener(writesLineFeedAsCrLf: true, isWindows: false).TryOpen("\uDCFF.txt", append: false, out Stream? stream));
        stream.Write("a\n"u8);
        stream.Dispose();

        CollectionAssert.AreEqual("a\r\n"u8.ToArray(), rawFile.ToArray());
    }

    [TestMethod]
    public void TryOpen_EscapedByteOnWindows_OpensThroughFileStream()
    {
        Assert.IsTrue(Opener(writesLineFeedAsCrLf: false, isWindows: true).TryOpen(Path.Combine(root, "\uDCFF.txt"), append: false, out Stream? stream));
        stream.Dispose();

        Assert.IsInstanceOfType<FileStream>(stream);
        Assert.IsEmpty(rawOpens);
    }

    [TestMethod]
    public void TryOpen_ValidNameOffWindows_OpensThroughFileStream()
    {
        string file = Path.Combine(root, "é�.txt");

        Assert.IsTrue(Opener(writesLineFeedAsCrLf: false, isWindows: false).TryOpen(file, append: false, out Stream? stream));
        stream.Dispose();

        Assert.IsTrue(File.Exists(file));
        Assert.IsEmpty(rawOpens);
    }

    private DiskWriteOutFileOpener Opener(bool writesLineFeedAsCrLf, bool isWindows) => new(writesLineFeedAsCrLf, isWindows, RecordRawOpen);

    private FileOpenResult RecordRawOpen(byte[] path, FileWriteMode mode, UnixFileMode createMode)
    {
        rawOpens.Add((path, mode, createMode));
        return rawOpenFails ? FileOpenResult.Failed(FileAccessStatus.AccessDenied) : FileOpenResult.Opened(rawFile, 0, null);
    }

    private sealed class NoVariables : IWriteOutVariableSource
    {
        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            text = null;
            return false;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }
}
