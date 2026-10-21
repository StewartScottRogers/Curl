using Curl.Conformance.UnitProgram.UrlParsing;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1614NoProxyMatchingTests
{
    [TestMethod]
    public async Task RunAsync_NoProxyMatcher_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1614NoProxyMatching(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1614", new Unit1614NoProxyMatching().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OneCidrAndOneListRowDisagree_WritesBothRowsAndFailsOnce()
    {
        using StringWriter standardError = new();
        Unit1614NoProxyMatching program = new((host, noProxy) =>
            noProxy is "192.160.0.1/33" or "barfoo" || NoProxyMatcher.Matches(host, noProxy));

        UnitProgramResult result = await RunAsync(program, standardError);

        string newLine = Environment.NewLine;
        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(
            "192.160.0.1 in 192.160.0.1/33 should not match" + newLine
            + "foobar in barfoo should not match" + newLine
            + "unit1614: errors" + newLine,
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NothingMatches_WritesEveryExpectedMatchAsShouldMatch()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1614NoProxyMatching((_, _) => false), standardError);

        string written = standardError.ToString();
        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(written, "::1 in 0:0::1/128 should match");
        StringAssert.Contains(written, "BAr in foo,,,,,              bar, ::1/64 should match");
        Assert.DoesNotContain("should not match", written);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1614NoProxyMatching().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1614NoProxyMatching program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1614, []) { StandardError = standardError }, CancellationToken.None);
}
