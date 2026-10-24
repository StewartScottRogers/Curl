using System.Text.Json;
using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Printf;

[TestClass]
public sealed class LibcurlPrintfScenarioReaderTests
{
    [TestMethod]
    public void Read_EveryKind_ReadsEachArgument()
    {
        var scenario = LibcurlPrintfScenarioReader.Read("""
            [["%d %ld %c %lld %llu %f %s %s %p %p", [["i", -1], ["l", 7], ["c", 65], ["q", 9000000000], ["u", 18446744073709551615],
              ["d", "3ff8000000000000"], ["s", "hi"], ["s", null], ["p", 16], ["p", null]]]]
            """).Single();

        Assert.AreEqual(0, scenario.Index);
        Assert.AreEqual("%d %ld %c %lld %llu %f %s %s %p %p", scenario.Format);
        CollectionAssert.AreEqual(
            new LibcurlPrintfArgument[]
            {
                new('i', -1, null), new('l', 7, null), new('c', 65, null), new('q', 9000000000, null), new('u', -1, null),
                new('d', 0x3ff8000000000000, null), new('s', null, "hi"), new('s', null, null), new('p', 16, null), new('p', null, null),
            },
            scenario.Arguments.ToArray());
    }

    [TestMethod]
    public void Read_SecondScenario_NumbersItFromZero() =>
        Assert.AreEqual(1, LibcurlPrintfScenarioReader.Read("""[["a", []], ["b", []]]""")[1].Index);

    [TestMethod]
    public void Read_UnknownKind_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => LibcurlPrintfScenarioReader.Read("""[["%d", [["x", 1]]]]"""));

    [TestMethod]
    public void Read_TooManyArguments_Throws() =>
        Assert.ThrowsExactly<FormatException>(() => LibcurlPrintfScenarioReader.Read("[[\"%d\", [" + string.Join(", ", Enumerable.Repeat("[\"i\", 1]", 11)) + "]]]"));

    [TestMethod]
    public void Read_NotJson_Throws() =>
        Assert.Throws<JsonException>(() => LibcurlPrintfScenarioReader.Read("["));

    [TestMethod]
    public void ReadEmbedded_TesterInputFiles_ReadThem()
    {
        Assert.AreEqual("%010F", LibcurlPrintfScenarioReader.ReadEmbedded("LibcurlPrintf/ZeroPaddedNan.json").Single().Format);
        Assert.HasCount(35982, LibcurlPrintfScenarioReader.ReadEmbedded("LibcurlPrintf/PrintfScenarios.json"));
    }

    [TestMethod]
    public void ReadEmbedded_NoSuchFile_Throws() =>
        Assert.ThrowsExactly<FileNotFoundException>(() => LibcurlPrintfScenarioReader.ReadEmbedded("LibcurlPrintf/None.json"));
}
