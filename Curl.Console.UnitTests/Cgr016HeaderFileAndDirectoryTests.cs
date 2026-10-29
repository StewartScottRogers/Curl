using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// Pins CGR-016's <c>--create-dirs</c> part (BL-3868): off Windows a directory whose name holds an
/// argument byte that is not valid UTF-8 is made under its raw bytes, as curl 8.18.0 on Linux makes
/// <c>--create-dirs -o $'\xfd/b.txt'</c>'s directory FD; every other name is made as before. The
/// <c>-D</c> file opens through <see cref="RawByteFileName.OpenForWriteAsync(Curl.Protocol.Abstractions.IFileSystem, string, Curl.Protocol.Abstractions.FileWriteMode, UnixFileMode, Func{byte[], Curl.Protocol.Abstractions.FileWriteMode, UnixFileMode, Curl.Protocol.Abstractions.FileOpenResult}, CancellationToken)" />,
/// which <see cref="Cgr016OutputFileTests" /> pins.
/// </summary>
[TestClass]
public sealed class Cgr016HeaderFileAndDirectoryTests
{
    private readonly List<byte[]> rawDirectories = [];

    private int rawErrorNumber;

    [TestMethod]
    public void TryCreateDirectory_EscapedByte_MakesTheRawBytes()
    {
        bool created = OutputPaths().TryCreateDirectory("\uDCFD", out int errorNumber);

        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        Assert.HasCount(1, rawDirectories);
        CollectionAssert.AreEqual(new byte[] { 0xFD, 0x00 }, rawDirectories[0]);
    }

    [TestMethod]
    public void TryCreateDirectory_EscapedByteAlreadyThere_CountsAsMade()
    {
        rawErrorNumber = CRuntimeErrorNumbers.AlreadyExists;

        bool created = OutputPaths().TryCreateDirectory("d/\uDCFD", out int errorNumber);

        Assert.IsTrue(created);
        Assert.AreEqual(0, errorNumber);
        CollectionAssert.AreEqual(new byte[] { 0x64, 0x2F, 0xFD, 0x00 }, rawDirectories[0]);
    }

    [TestMethod]
    public void TryCreateDirectory_EscapedByteRefused_GivesTheErrno()
    {
        rawErrorNumber = CRuntimeErrorNumbers.NotADirectory;

        bool created = OutputPaths().TryCreateDirectory("\uDC80", out int errorNumber);

        Assert.IsFalse(created);
        Assert.AreEqual(CRuntimeErrorNumbers.NotADirectory, errorNumber);
    }

    [TestMethod]
    public void TryCreateDirectory_ValidName_MakesItThroughTheRuntime()
    {
        string root = Path.Combine(Path.GetTempPath(), "curl-bl3868-" + Guid.NewGuid().ToString("N"));
        try
        {
            string directory = Path.Combine(root, "é");

            bool created = OutputPaths().TryCreateDirectory(directory, out int errorNumber);

            Assert.IsTrue(created);
            Assert.AreEqual(0, errorNumber);
            Assert.IsTrue(Directory.Exists(directory));
            Assert.IsEmpty(rawDirectories);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TryCreateDirectory_EscapedByteWithNoRawMaker_MakesItThroughTheRuntime()
    {
        string root = Path.Combine(Path.GetTempPath(), "curl-bl3868-" + Guid.NewGuid().ToString("N"));
        try
        {
            PhysicalOutputPaths windowsOutputPaths = new(isRegularFile: null, CRuntimeErrorNumbers.Windows);

            bool created = windowsOutputPaths.TryCreateDirectory(Path.Combine(root, "\uDCFD"), out _);

            Assert.IsTrue(created);
            Assert.HasCount(1, Directory.GetDirectories(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private PhysicalOutputPaths OutputPaths() => new(isRegularFile: null, CRuntimeErrorNumbers.Linux, MakeRawDirectory);

    private int MakeRawDirectory(byte[] path)
    {
        rawDirectories.Add(path);

        return rawErrorNumber;
    }
}
