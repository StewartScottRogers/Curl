using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// Pins CGR-016's <c>--skip-existing</c> and <c>--remove-on-error</c> part (BL-3947): off Windows an
/// output name holding an argument byte that is not valid UTF-8 is checked and deleted under its raw
/// bytes, as curl 8.18.0 on Linux skips <c>--skip-existing -o $'\xfc.txt'</c> when FC 2E 74 78 74 is
/// there and deletes that file after a failure; every other name goes through .NET's file APIs as before.
/// </summary>
[TestClass]
public sealed class Cgr016SkipExistingAndRemoveTests
{
    private const int DirectoryType = 0x4000;

    private static readonly byte[] RawName = [0xFC, 0x2E, 0x74, 0x78, 0x74, 0x00];

    private readonly List<byte[]> statted = [];

    private readonly List<byte[]> removed = [];

    private int fileType = RawByteFileName.RegularFileType;

    private bool removes = true;

    [TestMethod]
    public void Exists_EscapedByteFile_StatsTheRawBytes()
    {
        bool exists = OutputPaths().Exists("\uDCFC.txt");

        Assert.IsTrue(exists);
        Assert.HasCount(1, statted);
        CollectionAssert.AreEqual(RawName, statted[0]);
    }

    [TestMethod]
    public void Exists_EscapedByteDirectory_IsTrue()
    {
        fileType = DirectoryType;

        bool exists = OutputPaths().Exists("\uDCFC.txt");

        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void Exists_EscapedByteMissing_IsFalse()
    {
        fileType = -1;

        bool exists = OutputPaths().Exists("\uDCFC.txt");

        Assert.IsFalse(exists);
    }

    [TestMethod]
    public void Exists_ValidName_ChecksThroughTheRuntime()
    {
        string file = Path.Combine(Path.GetTempPath(), "curl-bl3947-é-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(file, "old");
        try
        {
            bool exists = OutputPaths().Exists(file);

            Assert.IsTrue(exists);
            Assert.IsEmpty(statted);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    public void RemoveFile_EscapedByteRegularFile_RemovesTheRawBytes()
    {
        OutputFileRemoval removal = OutputPaths().RemoveFile("\uDCFC.txt");

        Assert.AreEqual(OutputFileRemoval.Removed, removal);
        Assert.HasCount(1, removed);
        CollectionAssert.AreEqual(RawName, removed[0]);
    }

    [TestMethod]
    public void RemoveFile_EscapedByteUnlinkRefused_Fails()
    {
        removes = false;

        OutputFileRemoval removal = OutputPaths().RemoveFile("\uDCFC.txt");

        Assert.AreEqual(OutputFileRemoval.Failed, removal);
    }

    [TestMethod]
    public void RemoveFile_EscapedByteNotRegularFile_LeavesIt()
    {
        fileType = DirectoryType;

        OutputFileRemoval removal = OutputPaths().RemoveFile("\uDCFC.txt");

        Assert.AreEqual(OutputFileRemoval.NotRegularFile, removal);
        Assert.IsEmpty(removed);
    }

    [TestMethod]
    public void RemoveFile_ValidName_DeletesThroughTheRuntime()
    {
        string file = Path.Combine(Path.GetTempPath(), "curl-bl3947-é-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(file, "old");

        OutputFileRemoval removal = OutputPaths().RemoveFile(file);

        Assert.AreEqual(OutputFileRemoval.Removed, removal);
        Assert.IsFalse(File.Exists(file));
        Assert.IsEmpty(statted);
    }

    private PhysicalOutputPaths OutputPaths() =>
        new(isRegularFile: null, CRuntimeErrorNumbers.Linux, rawByteFiles: new RawByteFileName.FileOperations(FileTypeOf, Remove));

    private int FileTypeOf(byte[] path)
    {
        statted.Add(path);

        return fileType;
    }

    private bool Remove(byte[] path)
    {
        removed.Add(path);

        return removes;
    }
}
