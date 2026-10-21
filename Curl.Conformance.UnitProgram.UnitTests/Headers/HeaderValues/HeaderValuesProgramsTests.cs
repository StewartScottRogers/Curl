using System.Text;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Conformance.UnitProgram.Headers.HeaderValues;

namespace Curl.Conformance.UnitProgram.UnitTests.Headers.HeaderValues;

[TestClass]
public sealed class HeaderValuesProgramsTests
{
    [TestMethod]
    [DataRow("unit1625", "33 invokes\n")]
    [DataRow("unit1626", "28 invokes\n")]
    public async Task RunAsync_PortedProgram_EveryRowPassesAndTheInvokeCountIsWritten(string programName, string expectedOutput)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;
        using MemoryStream standardOutput = new();
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1625, []) { StandardOutput = standardOutput, StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(expectedOutput, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    [DataRow("unit1625")]
    [DataRow("unit1626")]
    public async Task RunAsync_NullContext_Throws(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => program.RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task RunAsync_RowThatFails_WritesItAndTheRowsBeforeItAndReturnsFailedInit()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await new SecondRowFailsProgram().RunAsync(new UnitProgramContext(1, []) { StandardOutput = standardOutput }, CancellationToken.None);

        Assert.AreEqual("Input: b\n1 invokes\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(InvokeCountingUnitProgram<string>.FailedInitExitCode, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_CheckAfterRowsFails_CountsItAndWritesIt()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await new FailingAfterRowsProgram().RunAsync(new UnitProgramContext(1, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit0002: after rows" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    [DataRow("Encoding: gzip", true, "Input: Encoding: gzip\nHeader: Encoding:\nLook for: br\nReturned: FALSE\n0 invokes\n")]
    [DataRow("Encoding: br", false, "Input: Encoding: br\nHeader: Encoding:\nLook for: br\nReturned: TRUE\n0 invokes\n")]
    public async Task RunAsync_Unit1625RowThatDiffers_WritesWhatCurlReturnedAndFailsInit(string line, bool expect, string expectedOutput)
    {
        using MemoryStream standardOutput = new();
        Unit1625CompareHeader program = new([(line, "Encoding:", "br", expect)]);

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1625, []) { StandardOutput = standardOutput }, CancellationToken.None);

        Assert.AreEqual(expectedOutput, Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(InvokeCountingUnitProgram<string>.FailedInitExitCode, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Unit1626RowThatDiffers_WritesWhatCurlCopiedAndFailsInit()
    {
        using MemoryStream standardOutput = new();
        Unit1626CopyHeaderValue program = new([("Header: value", "value"), ("Header: one ", "two")]);

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1626, []) { StandardOutput = standardOutput }, CancellationToken.None);

        Assert.AreEqual("Input: Header: one \nGot: one\nExpected: two\n1 invokes\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(InvokeCountingUnitProgram<string>.FailedInitExitCode, result.ExitCode);
    }

    [TestMethod]
    public void Constructor_NullRows_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Unit1626CopyHeaderValue(null!));

    private sealed class SecondRowFailsProgram() : InvokeCountingUnitProgram<string>(["a", "b", "c"])
    {
        public override string ProgramName => "unit0001";

        protected override string? Mismatch(string row) => row == "a" ? null : $"Input: {row}\n";
    }

    private sealed class FailingAfterRowsProgram() : InvokeCountingUnitProgram<string>([])
    {
        public override string ProgramName => "unit0002";

        protected override string? Mismatch(string row) => null;

        protected override void CheckAfterRows(UnitChecks checks) => checks.FailUnless(false, "after rows");
    }
}
