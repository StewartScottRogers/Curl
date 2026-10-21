using Curl.Conformance.UnitProgram.Dns.Tls;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Tls;

[TestClass]
public sealed class Unit3303MutualTlsConnectionReuseTests
{
    [TestMethod]
    public async Task RunAsync_OverTheConnectionSettings_EveryCheckHolds()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit3303")!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(3303, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ReuseThatAlwaysHappens_FailsTheFourChecksThatWantANewConnection()
    {
        Unit3303MutualTlsConnectionReuse program = new((_, _) => true);
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(3303, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(4, result.ExitCode);
        Assert.AreEqual(
            "unit3303.c Assertion FAILED: different key_passwd must not reuse conn\n"
            + "unit3303.c Assertion FAILED: different key must not reuse conn\n"
            + "unit3303.c Assertion FAILED: different key_type must not reuse conn\n"
            + "unit3303.c Assertion FAILED: different cert_type must not reuse conn\n",
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3303MutualTlsConnectionReuse().RunAsync(null!, CancellationToken.None));
    }
}
