using Curl.Conformance.Differential.Libcurl.Multi;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Multi;

[TestClass]
public sealed class LibcurlMultiCaseReaderTests
{
    [TestMethod]
    public void ReadMulti_ReadsEachCasesSteps()
    {
        var cases = LibcurlMultiCaseReader.ReadMulti("""[[["easy", [[10002, "s", "http://x/"], [155, "l", 300]]], ["add", 0], ["run"]], [["setopt", 7, "l", 1]]]""");
        Assert.HasCount(2, cases);
        Assert.AreEqual(new LibcurlMultiValue(10002, "s", 0, "http://x/"), cases[0][0].Options[0]);
        Assert.AreEqual(new LibcurlMultiValue(155, "l", 300, null), cases[0][0].Options[1]);
        Assert.AreEqual(("add", 0L), (cases[0][1].Operation, cases[0][1].Argument));
        Assert.AreEqual(("run", 0L, null), (cases[0][2].Operation, cases[0][2].Argument, cases[0][2].Setting));
        Assert.AreEqual(new LibcurlMultiValue(7, "l", 1, null), cases[1][0].Setting);
    }

    [TestMethod]
    public void ReadShare_ReadsShareSettings()
    {
        var steps = LibcurlMultiCaseReader.ReadShare("""[["sset", 1, 2], ["sset", 2, "x"], ["ssetnull", 5]]""");
        Assert.AreEqual(new LibcurlMultiValue(1, "l", 2, null), steps[0].Setting);
        Assert.AreEqual(new LibcurlMultiValue(2, "s", 0, "x"), steps[1].Setting);
        Assert.AreEqual(new LibcurlMultiValue(5, "null", 0, null), steps[2].Setting);
    }

    [TestMethod]
    public void ReadEmbedded_EveryTesterFile_IsEmbeddedAndReads()
    {
        foreach (var name in new[] { "m_in", "m2", "m2i", "m3" })
        {
            Assert.IsNotEmpty(LibcurlMultiCaseReader.ReadMulti(LibcurlMultiCaseReader.ReadEmbedded($"LibcurlMulti/{name}.json")), name);
        }

        Assert.IsNotEmpty(LibcurlMultiCaseReader.ReadShare(LibcurlMultiCaseReader.ReadEmbedded("LibcurlMulti/sh.json")));
    }

    [TestMethod]
    public void ReadEmbedded_MissingFile_Throws() =>
        Assert.ThrowsExactly<FileNotFoundException>(() => LibcurlMultiCaseReader.ReadEmbedded("LibcurlMulti/none.json"));

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMultiCaseReader.ReadEmbedded(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMultiCaseReader.ReadMulti(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMultiCaseReader.ReadShare(null!));
    }
}
