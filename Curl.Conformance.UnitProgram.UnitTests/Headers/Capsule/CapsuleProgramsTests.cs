using Curl.Conformance.UnitProgram.Headers.Capsule;

namespace Curl.Conformance.UnitProgram.UnitTests.Headers.Capsule;

[TestClass]
public sealed class CapsuleProgramsTests
{
    [TestMethod]
    public async Task RunAsync_Unit3400_EveryCheckPassesOverCurlsCapsuleCode()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit3400")!;
        using MemoryStream standardOutput = new();
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(3400, []) { StandardOutput = standardOutput, StandardError = standardError }, CancellationToken.None);

        Assert.IsInstanceOfType<Unit3400Capsule>(program);
        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Unit3400NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3400Capsule().RunAsync(null!, CancellationToken.None));
    }
}
