using Curl.Conformance.UnitProgram.Asn1.Names;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Names;

[TestClass]
public sealed class Unit1397CertHostCheckTests
{
    [TestMethod]
    public async Task RunAsync_SchannelCommonNameCheck_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1397CertHostCheck(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1397", new Unit1397CertHostCheck().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_CheckThatAlwaysMatches_WritesEveryNonMatchingRowAsDidMatch()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1397CertHostCheck((_, _) => true), standardError);

        string written = standardError.ToString();
        Assert.AreEqual(27, result.ExitCode);
        StringAssert.StartsWith(written, "unit1397: HOST: .hello.com PTRN: *.hello.com did MATCH" + Environment.NewLine);
        Assert.DoesNotContain("did NOT MATCH", written);
    }

    [TestMethod]
    public async Task RunAsync_CheckThatNeverMatches_WritesEveryMatchingRowAsDidNotMatch()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1397CertHostCheck((_, _) => false), standardError);

        string written = standardError.ToString();
        Assert.AreEqual(19, result.ExitCode);
        StringAssert.StartsWith(written, "unit1397: HOST: a.hello.com PTRN: *.hello.com did NOT MATCH" + Environment.NewLine);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1397CertHostCheck().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1397CertHostCheck program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1397, []) { StandardError = standardError }, CancellationToken.None);
}
