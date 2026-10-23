using System.Text;

namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class DifferentialComparerTests
{
    [TestMethod]
    public void Compare_SameRuns_FindsNoDifference()
    {
        var outcome = DifferentialComparer.Compare(Case(true, true), Run(0, "a", "e", "w", ("f", "x")), Run(0, "a", "e", "w", ("f", "x")));

        Assert.IsEmpty(outcome.Differences);
    }

    [TestMethod]
    public void Compare_EveryPartDiffers_WritesEachBlockInOrder()
    {
        var outcome = DifferentialComparer.Compare(Case(true, true), Run(null, "o1", "e1", "w1", ("b", "1"), ("a", "2")), Run(7, "o2", "e2", "w2"));

        CollectionAssert.AreEqual(
            new[]
            {
                "exit: ours=TIMEOUT real=7",
                "stdout:\n  ours=b'o1'\n  real=b'o2'",
                "stderr:\n  ours=b'e1'\n  real=b'e2'",
                "files:\n  ours={'a': b'2', 'b': b'1'}\n  real={}",
                "wire:\n  ours=b'w1'\n  real=b'w2'",
            },
            outcome.Differences.ToArray());
    }

    [TestMethod]
    public void Compare_StderrAndWireNotCompared_IgnoresThem()
    {
        var outcome = DifferentialComparer.Compare(Case(false, false), Run(0, "a", "e1", "w1"), Run(0, "a", "e2", "w2"));

        Assert.IsEmpty(outcome.Differences);
    }

    [TestMethod]
    public void Compare_FileMissingOrChanged_IsAFilesDifference()
    {
        var missing = DifferentialComparer.Compare(Case(true, true), Run(0, "", "", "", ("a", "1")), Run(0, "", "", "", ("b", "1")));
        var changed = DifferentialComparer.Compare(Case(true, true), Run(0, "", "", "", ("a", "1")), Run(0, "", "", "", ("a", "2")));

        Assert.HasCount(1, missing.Differences);
        Assert.AreEqual("files:\n  ours={'a': b'1'}\n  real={'a': b'2'}", changed.Differences.Single());
    }

    [TestMethod]
    public void Compare_LongOutput_ShowsTheFirst600Bytes()
    {
        var outcome = DifferentialComparer.Compare(Case(true, true), Run(0, new string('x', 601), "", ""), Run(0, "", "", ""));

        Assert.AreEqual("stdout:\n  ours=b'" + new string('x', 600) + "'\n  real=b''", outcome.Differences.Single());
    }

    internal static DifferentialCase Case(bool comparesStandardError, bool comparesWire, string name = "c", params string[] arguments) =>
        new(name, arguments, null, new Dictionary<string, byte[]>(), TimeSpan.FromSeconds(20), comparesStandardError, comparesWire, []);

    internal static DifferentialRunResult Run(int? exitCode, string standardOutput, string standardError, string wire, params (string Path, string Content)[] files) =>
        new(
            exitCode,
            Encoding.Latin1.GetBytes(standardOutput),
            Encoding.Latin1.GetBytes(standardError),
            files.ToDictionary(file => file.Path, file => Encoding.Latin1.GetBytes(file.Content)),
            Encoding.Latin1.GetBytes(wire),
            TimeSpan.Zero);
}
