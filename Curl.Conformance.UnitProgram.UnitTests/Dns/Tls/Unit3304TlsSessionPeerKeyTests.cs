using Curl.Conformance.UnitProgram.Dns.Tls;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Tls;

[TestClass]
public sealed class Unit3304TlsSessionPeerKeyTests
{
    [TestMethod]
    public async Task RunAsync_OverThePeerKey_EveryCheckHolds()
    {
        IUnitProgram program = UnitPrograms.Ported.Find("unit3304")!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(3304, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_PeerKeyThatIsAlwaysTheSame_FailsTheThreeChecksThatWantADifferentKey()
    {
        Unit3304TlsSessionPeerKey program = new(_ => "same");
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(3304, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(3, result.ExitCode);
        Assert.AreEqual(
            "unit3304.c Assertion FAILED: different key must produce different peer key\n"
            + "unit3304.c Assertion FAILED: different key_type must produce different peer key\n"
            + "unit3304.c Assertion FAILED: different cert_type must produce different peer key\n",
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3304TlsSessionPeerKey().RunAsync(null!, CancellationToken.None));
    }
}
