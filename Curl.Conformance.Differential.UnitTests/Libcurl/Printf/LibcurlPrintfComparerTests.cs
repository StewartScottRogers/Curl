using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Printf;

[TestClass]
public sealed class LibcurlPrintfComparerTests
{
    private static readonly LibcurlPrintfScenario Same = new(0, "%d", [new('i', 1, null)]);

    private static readonly LibcurlPrintfScenario Skipped = new(1, "%Lf", [new('d', 0x3ff8000000000000, null)]);

    private static readonly LibcurlPrintfScenario Differing = new(2, "%s %p %llu %f %s %c", [new('s', null, "a"), new('p', null, null), new('u', -1, null), new('d', 0x3ff8000000000000, null), new('s', null, null), new('c', 65, null)]);

    [TestMethod]
    public void Compare_SameNotRunAndDifferent_CountsEach()
    {
        var outcome = LibcurlPrintfComparer.Compare("pf_in", [Same, Skipped, Differing], ["1 31", LibcurlPrintfLineWriter.NotRun, "1 61"], ["1 31", "8 312E353030303030", "1 62"]);

        Assert.AreEqual("pf_in", outcome.Name);
        Assert.AreEqual(1, outcome.Identical);
        Assert.AreEqual(1, outcome.NotRunAgainstReference);
        Assert.AreEqual(new LibcurlPrintfDifference(Differing, "1 62", "1 61"), outcome.Differences.Single());
    }

    [TestMethod]
    public void Write_Differences_WritesTheTesterReportShape()
    {
        var outcomes = new[]
        {
            new LibcurlPrintfOutcome("t1", 1, 0, []),
            LibcurlPrintfComparer.Compare("pf_in", [Differing], ["1 61"], ["EXC FormatException"]),
        };

        Assert.AreEqual(
            "### pf_in\n'%s %p %llu %f %s %c' [('s', 'a'), ('p', None), ('u', 18446744073709551615), ('d', '3ff8000000000000'), ('s', None), ('c', 65)]\n  OURS 'EXC FormatException'\n  REAL '1 a'\n\n",
            LibcurlPrintfComparer.Write(outcomes));
    }

    [TestMethod]
    public void Summary_Outcomes_CountsEachFile() =>
        Assert.AreEqual(
            "t1: cases 1 identical 1 different 0 not run 0; pf_in: cases 3 identical 1 different 1 not run 1",
            LibcurlPrintfComparer.Summary([new LibcurlPrintfOutcome("t1", 1, 0, []), new LibcurlPrintfOutcome("pf_in", 1, 1, [new(Same, "a", "b")])]));

    [TestMethod]
    [DataRow("2 6869", "2 hi")]
    [DataRow("-1 ", "-1 ")]
    [DataRow("EXC OverflowException", "EXC OverflowException")]
    [DataRow("7", "7")]
    public void Decode_Line_DecodesOnlyACountAndHex(string line, string expected) =>
        Assert.AreEqual(expected, LibcurlPrintfComparer.Decode(line));
}
