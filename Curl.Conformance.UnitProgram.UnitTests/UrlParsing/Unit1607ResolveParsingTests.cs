using Curl.Conformance.UnitProgram.UrlParsing;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1607ResolveParsingTests
{
    [TestMethod]
    public async Task RunAsync_ResolveOverridesParse_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1607ResolveParsing(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1607", new Unit1607ResolveParsing().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_NoAddressesFound_EveryRowExpectingOneFails()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1607ResolveParsing(_ => ResolveOverrides.None), standardError);

        Assert.AreEqual(10, result.ExitCode);
        StringAssert.Contains(standardError.ToString(), "unit1607: tests[1] failed. the retrieved addr is NULL but tests[1].address[0] is 127.0.0.1.");
    }

    [TestMethod]
    public async Task RunAsync_DifferentAndExtraAddresses_EveryRowFails()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1607ResolveParsing(_ => ResolveOverrides.Parse(["+test.com:80:127.0.0.1,::2", "+test.com:0:127.0.0.1"])),
            standardError);

        string written = standardError.ToString();
        Assert.AreEqual(11, result.ExitCode);
        StringAssert.Contains(written, "tests[0] failed. the retrieved addr is 127.0.0.1 but tests[0].address[0] is NULL.");
        StringAssert.Contains(written, "tests[1] failed. the retrieved addr ::2 is not equal to tests[1].address[1] 127.0.0.2.");
        StringAssert.Contains(written, "tests[9] failed. the timestamp is not zero but tests[9].permanent is TRUE");
    }

    [TestMethod]
    public async Task RunAsync_PermanentEntryWhereThePlusRowExpectsNone_OnlyThatRowFails()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1607ResolveParsing(value => value.Single().StartsWith('+')
                ? ResolveOverrides.Parse(["-other.com:80", "other.com:80:127.0.0.9", "test.com:81:127.0.0.9", "test.com:80:127.0.0.1"])
                : ResolveOverrides.Parse(value)),
            standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1607: tests[10] failed. the timestamp is zero but tests[10].permanent is FALSE" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1607ResolveParsing().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1607ResolveParsing program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1607, []) { StandardError = standardError }, CancellationToken.None);
}
