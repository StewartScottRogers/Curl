using Curl.Conformance.UnitProgram.Wildcards;
using Curl.Original.Protocol.Ftp;

namespace Curl.Conformance.UnitProgram.UnitTests.Wildcards;

[TestClass]
public sealed class Unit1307FnmatchTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ThisPlatform_ExpectsNoFailure()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit1307")!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1307, []) { StandardError = standardError }, TestContext.CancellationToken);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.FailureCount);
    }

    [TestMethod]
    [DataRow(FtpWildcardMatcher.Curl)]
    [DataRow(FtpWildcardMatcher.Linux)]
    [DataRow(FtpWildcardMatcher.MacOs)]
    public void CountFailedRows_EveryPlatform_NoRowFails(FtpWildcardMatcher matcher)
    {
        using StringWriter standardError = new();

        Assert.AreEqual(0, Unit1307Fnmatch.CountFailedRows(standardError, matcher));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_WrongAnswer_WritesTheCLineAndFails()
    {
        using StringWriter standardError = new();
        Unit1307Fnmatch.Row row = new("a", "a", FtpNameMatchAnswer.NoMatch);

        bool passed = Unit1307Fnmatch.CheckRow(standardError, 7, row, FtpWildcardMatcher.Curl);

        Assert.IsFalse(passed);
        Assert.Contains("Curl_fnmatch(\"a\", \"a\") should return NOMATCH (returns MATCH) [7]", standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_FailAnswerExpected_NamesFail()
    {
        using StringWriter standardError = new();
        Unit1307Fnmatch.Row row = new("a", "a", FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.Fail);

        Assert.IsFalse(Unit1307Fnmatch.CheckRow(standardError, 0, row, FtpWildcardMatcher.MacOs));
        Assert.Contains("should return FAIL", standardError.ToString());
    }

    [TestMethod]
    public void CountFailedRows_OneWrongRowOfTwo_CountsOne()
    {
        using StringWriter standardError = new();
        Unit1307Fnmatch.Row[] rows = [new("a", "a", FtpNameMatchAnswer.Match), new("a", "a", FtpNameMatchAnswer.NoMatch)];

        Assert.AreEqual(1, Unit1307Fnmatch.CountFailedRows(standardError, FtpWildcardMatcher.Curl, rows));
    }

    [TestMethod]
    public void ResultFor_TwoFailedRows_ReportsTwoMismatches()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = new Unit1307Fnmatch().ResultFor(standardError, 2);

        Assert.AreEqual(2, result.FailureCount);
        Assert.Contains("pattern mismatch", standardError.ToString());
    }

    [TestMethod]
    public void CheckRow_NullStandardError_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Unit1307Fnmatch.CheckRow(null!, 0, new("a", "a", FtpNameMatchAnswer.Match), FtpWildcardMatcher.Curl));
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1307Fnmatch().RunAsync(null!, TestContext.CancellationToken));
    }
}
