namespace Curl.Conformance;

/// <summary>Pins the ratchet of ADR-0013 decision 5, as <see cref="UpstreamCaseRatchet"/> applies it.</summary>
[TestClass]
public sealed class UpstreamCaseRatchetTests
{
    [TestMethod]
    public void ReadPassingList_ReadsNumbersAndSkipsCommentsAndBlankLines()
    {
        IReadOnlySet<int> listed = UpstreamCaseRatchet.ReadPassingList("# passing\n1\r\n\n  200 \n");

        CollectionAssert.AreEquivalent(new[] { 1, 200 }, listed.ToArray());
    }

    [TestMethod]
    public void ReadPassingList_LineThatIsNotANumber_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => UpstreamCaseRatchet.ReadPassingList("12a\n"));
    }

    [TestMethod]
    public void ReadPassingList_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseRatchet.ReadPassingList(null!));
    }

    [TestMethod]
    public void Judge_ListedPass_Passes()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Passed, isListed: true);

        Assert.AreEqual(UpstreamCaseVerdictKind.Pass, verdict.Kind);
        Assert.AreEqual("test7 passes", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedPass_SaysItCanBeListed()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Passed, isListed: false);

        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 passes; add 7 to PassingUpstreamCases.txt", verdict.Message);
    }

    [TestMethod]
    public void Judge_ListedFailure_FailsWithTheDifference()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Failed("diff"), isListed: true);

        Assert.AreEqual(UpstreamCaseVerdictKind.Fail, verdict.Kind);
        Assert.AreEqual("test7 is on PassingUpstreamCases.txt and now fails: diff", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedFailure_IsInconclusiveWithTheDifference()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Failed("diff"), isListed: false);

        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 fails: diff", verdict.Message);
    }

    [TestMethod]
    public void Judge_ListedSkip_Fails()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Skipped("why"), isListed: true);

        Assert.AreEqual(UpstreamCaseVerdictKind.Fail, verdict.Kind);
        Assert.AreEqual("test7 is on PassingUpstreamCases.txt and is now skipped: why", verdict.Message);
    }

    [TestMethod]
    public void Judge_UnlistedSkip_IsInconclusiveWithTheReason()
    {
        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(7, UpstreamCaseOutcome.Skipped("why"), isListed: false);

        Assert.AreEqual(UpstreamCaseVerdictKind.Inconclusive, verdict.Kind);
        Assert.AreEqual("test7 is skipped: why", verdict.Message);
    }

    [TestMethod]
    public void Judge_NullOutcome_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamCaseRatchet.Judge(7, null!, isListed: true));
    }

    [TestMethod]
    public void Outcome_CarriesItsKindAndDetail()
    {
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, UpstreamCaseOutcome.Passed.Kind);
        Assert.AreEqual(string.Empty, UpstreamCaseOutcome.Passed.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Skipped, UpstreamCaseOutcome.Skipped("why").Kind);
        Assert.AreEqual("why", UpstreamCaseOutcome.Skipped("why").Detail);
    }
}
