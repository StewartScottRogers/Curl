using Curl.Conformance.Differential.Libcurl.Scenarios;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Scenarios;

[TestClass]
public sealed class LibcurlScenarioReportWriterTests
{
    [TestMethod]
    public void Write_NoDifference_WritesOnlyTheCounts()
    {
        LibcurlScenarioOutcome[] outcomes = [new(Scenario(0, "V"), ["0 V a"], ["0 V a"])];

        Assert.AreEqual("scenarios 1 differing 0 excluded 2\nV 0 of 1 differ\n", LibcurlScenarioReportWriter.Write(outcomes, 2));
    }

    [TestMethod]
    public void Write_DifferingLines_WritesOursAndRealForEachDifferingLineOnly()
    {
        LibcurlScenarioOutcome[] outcomes =
        [
            new(Scenario(0, "S"), ["0 easy 0 a", "0 easy 1 b"], ["0 easy 0 a", "0 easy 1 c"]),
            new(Scenario(1, "U"), ["1 U set=0"], ["1 U set=0"]),
            new(Scenario(2, "U"), ["2 U EXC X"], ["2 U set=3", "2 extra"]),
        ];

        var report = LibcurlScenarioReportWriter.Write(outcomes, 0);

        Assert.AreEqual(
            "scenarios 3 differing 2 excluded 0\nS 1 of 1 differ\nU 1 of 2 differ\n"
            + "\nOURS 0 easy 1 b\nREAL 0 easy 1 c\n"
            + "\nOURS 2 U EXC X\nREAL 2 U set=3\n"
            + "\nOURS (no line)\nREAL 2 extra\n",
            report);
    }

    [TestMethod]
    public void Write_LinesWithSameFieldCount_KeepsTheFirstAndTheDifferingFields()
    {
        LibcurlScenarioOutcome[] outcomes = [new(Scenario(0, "U"), ["0 U set=0 |0/0=0:61 |0/1=0:62 |0/2=0:63"], ["0 U set=0 |0/0=0:61 |0/1=3:null |0/2=0:63"])];

        var report = LibcurlScenarioReportWriter.Write(outcomes, 0);

        Assert.EndsWith("\nOURS 0 U set=0 |0/1=0:62\nREAL 0 U set=0 |0/1=3:null\n", report);
    }

    [TestMethod]
    public void Write_LinesWithDifferentFieldCounts_KeepsBothWhole()
    {
        LibcurlScenarioOutcome[] outcomes = [new(Scenario(1, "U"), ["1 U EXC X"], ["1 U set=0 |0/0=0:61"])];

        var report = LibcurlScenarioReportWriter.Write(outcomes, 0);

        Assert.EndsWith("\nOURS 1 U EXC X\nREAL 1 U set=0 |0/0=0:61\n", report);
    }

    [TestMethod]
    public void Write_LongLine_CutsItAt700Characters()
    {
        LibcurlScenarioOutcome[] outcomes = [new(Scenario(0, "U"), [new string('a', 701)], ["b"])];

        var report = LibcurlScenarioReportWriter.Write(outcomes, 0);

        Assert.Contains("\nOURS " + new string('a', 700) + "\nREAL b\n", report);
        Assert.DoesNotContain(new string('a', 701), report);
    }

    private static LibcurlScenario Scenario(int index, string kind) => new(index, kind, "http://a/", 0, 0, null, []);
}
