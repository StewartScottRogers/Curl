using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins the ratchet of ADR-0013 decision 5, as <see cref="UpstreamCaseRatchet"/> applies it.</summary>
[TestClass]
public sealed class UpstreamCaseRatchetTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ReadPassingList_ReadsNumbersAndSkipsCommentsAndBlankLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string list = "# passing\n1\r\n\n  200 \n";
        diagnostics.Arrange("passing list", list);

        IReadOnlySet<int> listed = UpstreamCaseRatchet.ReadPassingList(list);

        int[] actual = listed.ToArray();
        diagnostics.Act("listed cases", string.Join(",", actual.Order()));
        diagnostics.Assert("listed cases", "1,200", string.Join(",", actual.Order()));
        CollectionAssert.AreEquivalent(new[] { 1, 200 }, actual);
    }

    [TestMethod]
    public void ReadPassingList_LineThatIsNotANumber_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("passing list", "12a\n");

        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCaseRatchet.ReadPassingList("12a\n"));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(FormatException), exception.GetType().Name);
    }

    [TestMethod]
    public void ReadPassingList_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("passing list", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseRatchet.ReadPassingList(null!));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Judge_ListedPass_Passes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Passed, true");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Passed, isListed: true);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Pass, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 passes", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Pass, verdict.Kind);
        Assert.AreEqual("test7 passes", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedPass_SaysItCanBeListed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Passed, false");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Passed, isListed: false);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 passes; add 7 to PassingUpstreamCases.txt", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 passes; add 7 to PassingUpstreamCases.txt", verdict.Message);
    }

    [TestMethod]
    public void Judge_ListedFailure_FailsWithTheDifference()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Failed(diff), true");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Failed("diff"), isListed: true);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Fail, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 is on PassingUpstreamCases.txt and now fails: diff", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Fail, verdict.Kind);
        Assert.AreEqual("test7 is on PassingUpstreamCases.txt and now fails: diff", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedFailure_IsInconclusiveWithTheDifference()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Failed(diff), false");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Failed("diff"), isListed: false);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 fails: diff", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 fails: diff", verdict.Message);
    }

    [TestMethod]
    public void Judge_ListedSkip_Fails()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Skipped(why), true");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Skipped("why"), isListed: true);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Fail, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 is on PassingUpstreamCases.txt and is now skipped: why", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Fail, verdict.Kind);
        Assert.AreEqual("test7 is on PassingUpstreamCases.txt and is now skipped: why", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedSkip_IsInconclusiveWithTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, Skipped(why), false");

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Skipped("why"), isListed: false);

        diagnostics.Act("verdict", $"{verdict.Kind}: {verdict.Message}");
        diagnostics.Assert("verdict kind", UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        diagnostics.Diff("verdict message", "test7 is skipped: why", verdict.Message);
        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 is skipped: why", verdict.Message);
    }

    [TestMethod]
    public void Judge_NullOutcome_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("case, outcome, listed", "7, null, true");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseRatchet.Judge(7, null!, isListed: true));

        diagnostics.Act("exception", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Outcome_CarriesItsKindAndDetail()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("outcomes", "Passed, Skipped(why)");

        UpstreamCaseOutcome passed = UpstreamCaseOutcome.Passed;
        UpstreamCaseOutcome skipped = UpstreamCaseOutcome.Skipped("why");

        diagnostics.Act("passed", $"{passed.Kind} detail '{passed.Detail}'");
        diagnostics.Act("skipped", $"{skipped.Kind} detail '{skipped.Detail}'");
        diagnostics.Assert("passed kind", UpstreamCaseOutcomeKind.Passed, passed.Kind);
        diagnostics.Assert("passed detail", string.Empty, passed.Detail);
        diagnostics.Assert("skipped kind", UpstreamCaseOutcomeKind.Skipped, skipped.Kind);
        diagnostics.Assert("skipped detail", "why", skipped.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, UpstreamCaseOutcome.Passed.Kind);
        Assert.AreEqual(string.Empty, UpstreamCaseOutcome.Passed.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, UpstreamCaseOutcome.Skipped("why").Kind);
        Assert.AreEqual("why", UpstreamCaseOutcome.Skipped("why").Detail);
    }
}
