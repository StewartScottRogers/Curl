using Curl.Conformance.Differential.Libcurl.Mime;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Mime;

[TestClass]
public sealed class LibcurlMimeTests
{
    private static readonly LibcurlMimeCaseOutcome[] Outcomes = [new("same", []), new("quote-name", ["d1", "d2"])];

    [TestMethod]
    [DataRow(LibcurlMimeStepKind.Name, "name=0")]
    [DataRow(LibcurlMimeStepKind.FileName, "fn=0")]
    [DataRow(LibcurlMimeStepKind.Type, "type=0")]
    [DataRow(LibcurlMimeStepKind.Encoder, "enc=0")]
    [DataRow(LibcurlMimeStepKind.Data, "data=0")]
    [DataRow(LibcurlMimeStepKind.File, "file=0")]
    [DataRow(LibcurlMimeStepKind.Headers, "hdrs=0")]
    [DataRow(LibcurlMimeStepKind.SubParts, "sub=0")]
    public void Step_WritesTheCallsLabel(LibcurlMimeStepKind kind, string expected) =>
        Assert.AreEqual(expected, LibcurlMimeLogWriter.Step(kind, 0));

    [TestMethod]
    public void Transfer_WritesBothCodes() =>
        Assert.AreEqual("setopt=0 rc=26", LibcurlMimeLogWriter.Transfer(0, 26));

    [TestMethod]
    public void All_HoldsTheTestersCasesWithoutRefused()
    {
        var names = LibcurlMimeCases.All.Select(c => c.Name).ToList();
        Assert.HasCount(64, names);
        Assert.AreEqual("simple", names[0]);
        Assert.AreEqual("data-then-file", names[^1]);
        CollectionAssert.Contains(names, "enc-None");
        CollectionAssert.Contains(names, "enc-");
        CollectionAssert.DoesNotContain(names, "refused");
        Assert.HasCount(names.Count, names.Distinct());
    }

    [TestMethod]
    public void All_KeepsTheTestersCallOrderAndNullArguments()
    {
        var nested = LibcurlMimeCases.All.Single(c => c.Name == "sub-nested").Parts[0].Steps;
        CollectionAssert.AreEqual(new[] { LibcurlMimeStepKind.Name, LibcurlMimeStepKind.Type, LibcurlMimeStepKind.SubParts }, nested.Select(s => s.Kind).ToArray());
        Assert.AreEqual("deep", nested[2].SubParts[0].Steps[1].SubParts[0].Steps[0].Text);
        Assert.IsNull(LibcurlMimeCases.All.Single(c => c.Name == "null-data").Parts[0].Steps[1].Text);
        Assert.AreEqual(string.Empty, LibcurlMimeCases.All.Single(c => c.Name == "empty-data").Parts[0].Steps[1].Text);
        Assert.AreEqual("76", LibcurlMimeCases.All[0].Parts[0].Steps[1].Text);
        CollectionAssert.AreEqual(new[] { "nocolon", string.Empty }, LibcurlMimeCases.All.Single(c => c.Name == "headers-bad").Parts[0].Steps[2].Headers.ToArray());
        Assert.HasCount(512, LibcurlMimeCases.All.Single(c => c.Name == "binary-data").Parts[0].Steps[1].Text!);
        Assert.HasCount(50, LibcurlMimeCases.All.Single(c => c.Name == "many-parts").Parts);
        Assert.IsEmpty(LibcurlMimeCases.All.Single(c => c.Name == "empty-mime").Parts);
    }

    [TestMethod]
    public void Compare_SameCodesAndWireButForTheBoundary_HasNoDifferences()
    {
        var outcome = LibcurlMimeComparer.Compare("simple", "name=0 rc=0", "name=0 rc=0", "--------------------------abcdef0123456789\r\n"u8.ToArray(), "------------------------ZZZZ9999zzzz\r\n"u8.ToArray());
        Assert.AreEqual("simple", outcome.Name);
        Assert.IsEmpty(outcome.Differences);
    }

    [TestMethod]
    public void Compare_DifferingCodes_ReportsBothLogsCut()
    {
        var outcome = LibcurlMimeComparer.Compare("a", "name=0", new string('x', 400), [], []);
        CollectionAssert.AreEqual(new[] { "  CODES OURS " + new string('x', 300), "  CODES REAL name=0" }, outcome.Differences.ToArray());
    }

    [TestMethod]
    public void Compare_DifferingWire_ReportsTheFirstDifferenceInContext()
    {
        var real = new string('a', 100) + "REAL" + new string('b', 200);
        var ours = new string('a', 100) + "OURS";
        var outcome = LibcurlMimeComparer.Compare("a", "rc=0", "rc=0", System.Text.Encoding.Latin1.GetBytes(real), System.Text.Encoding.Latin1.GetBytes(ours));
        Assert.HasCount(3, outcome.Differences);
        Assert.AreEqual("  WIRE first diff at 100 of 104 304", outcome.Differences[0]);
        Assert.AreEqual("   OURS b'" + new string('a', 60) + "OURS'", outcome.Differences[1]);
        Assert.AreEqual("   REAL b'" + new string('a', 60) + "REAL" + new string('b', 136) + "'", outcome.Differences[2]);
    }

    [TestMethod]
    public void Compare_OneWireAPrefixOfTheOther_ReportsTheShorterLength()
    {
        var outcome = LibcurlMimeComparer.Compare("a", "rc=0", "rc=0", "abc"u8.ToArray(), "ab"u8.ToArray());
        Assert.AreEqual("  WIRE first diff at 2 of 2 3", outcome.Differences[0]);
    }

    [TestMethod]
    public void Compare_NullLogs_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMimeComparer.Compare("a", null!, "x", [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMimeComparer.Compare("a", "x", null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMimeComparer.NormaliseWire(null!));
    }

    [TestMethod]
    public void Write_WritesOnlyTheCasesThatDiffer() =>
        Assert.AreEqual("## quote-name\nd1\nd2\n", LibcurlMimeReportWriter.Write(Outcomes));

    [TestMethod]
    public void Summary_CountsTheCases() =>
        Assert.AreEqual("cases 2 identical 1", LibcurlMimeReportWriter.Summary(Outcomes));

    [TestMethod]
    public void ReportWriter_NullOutcomes_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMimeReportWriter.Write(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => LibcurlMimeReportWriter.Summary(null!));
    }
}
