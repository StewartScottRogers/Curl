using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1395DotSegmentRemovalTests
{
    [TestMethod]
    public async Task RunAsync_CurlsDedotdotify_PassesEveryRow()
    {
        using StringWriter standardError = new();
        UnitProgramContext context = new(1395, []) { StandardError = standardError };

        UnitProgramResult result = await new Unit1395DotSegmentRemoval().RunAsync(context, CancellationToken.None);

        Assert.AreEqual(UnitProgramResult.Success, result, standardError.ToString());
        Assert.AreEqual(106, Unit1395DotSegmentRemoval.Pairs.Count);
        StringAssert.Contains(standardError.ToString(), "Test 105: OK\n");
    }

    [TestMethod]
    public async Task RunAsync_DedotdotifyAlwaysNull_CountsEachMismatchPlusTheClosingFailIf()
    {
        using StringWriter standardError = new();
        UnitProgramContext context = new(1395, []) { StandardError = standardError };
        int rowsWithOutput = Unit1395DotSegmentRemoval.Pairs.Count(pair => pair.Output is not null);

        UnitProgramResult result = await new Unit1395DotSegmentRemoval(_ => null).RunAsync(context, CancellationToken.None);

        Assert.AreEqual(rowsWithOutput + 1, result.FailureCount);
        StringAssert.Contains(standardError.ToString(), "Test 0: '/%2f%2e%2e%2f/../a' gave '(null)' instead of '/a'\n");
    }

    [TestMethod]
    public async Task RunAsync_DedotdotifyAlwaysEmpty_ReportsANullExpectationAsNull()
    {
        using StringWriter standardError = new();
        UnitProgramContext context = new(1395, []) { StandardError = standardError };

        await new Unit1395DotSegmentRemoval(_ => string.Empty).RunAsync(context, CancellationToken.None);

        StringAssert.Contains(standardError.ToString(), "Test 3: '/%2f%2e%2e%2f/' gave '' instead of '(null)'\n");
    }

    [TestMethod]
    public void Constructor_NullDedotdotify_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Unit1395DotSegmentRemoval(null!));
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1395DotSegmentRemoval().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public void ProgramName_IsUnit1395_AndPorted()
    {
        Assert.AreEqual("unit1395", new Unit1395DotSegmentRemoval().ProgramName);
        Assert.IsNotNull(UnitPrograms.Ported.Find("unit1395"));
    }
}
