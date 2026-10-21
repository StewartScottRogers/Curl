using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram.UnitTests.Hashes;

[TestClass]
public sealed class HashesProgramsTests
{
    public static IEnumerable<object[]> Programs =>
    [
        ["unit1302"], ["unit1600"], ["unit1601"], ["unit1610"], ["unit1611"], ["unit1612"], ["unit1615"], ["unit1979"], ["unit1980"],
    ];

    [TestMethod]
    [DynamicData(nameof(Programs))]
    public async Task RunAsync_PortedProgram_EveryCheckPassesAndNothingIsWritten(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(int.Parse(programName[4..], System.Globalization.CultureInfo.InvariantCulture), []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [DynamicData(nameof(Programs))]
    public async Task RunAsync_NullContext_Throws(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => program.RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_DigestRowThatDiffers_CountsOneFailureAndWritesIt()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await new WrongDigestProgram().RunAsync(new UnitProgramContext(1, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit0001: row 1: memory buffer FAILED match, got 03 instead of 04" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void FailUnless_FalseCondition_WritesTheLineAndCountsAFailure()
    {
        using StringWriter standardError = new();
        UnitChecks checks = new(standardError, "unit1302");

        checks.FailUnless(true, "not written");
        checks.FailUnless(false, "Test 3 encoded badly");

        Assert.AreEqual(1, checks.Result.FailureCount);
        Assert.AreEqual("unit1302: Test 3 encoded badly" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public void WarnUnless_FalseCondition_WritesTheLineButCountsNoFailure()
    {
        using StringWriter standardError = new();
        UnitChecks checks = new(standardError, "unit1302");

        checks.WarnUnless(true, "not written");
        checks.WarnUnless(false, "Test 1 URL encoded output length 3 instead of 2");

        Assert.AreEqual(0, checks.Result.FailureCount);
        Assert.AreEqual("unit1302: Test 1 URL encoded output length 3 instead of 2" + Environment.NewLine, standardError.ToString());
    }

    private sealed class WrongDigestProgram : DigestUnitProgram
    {
        public override string ProgramName => "unit0001";

        protected override IReadOnlyList<(byte[] Input, byte[] Expected)> Rows { get; } = [([1], [2]), ([2], [4])];

        protected override byte[] Digest(byte[] input) => Bytes(((char)(input[0] + 1)).ToString());
    }
}
