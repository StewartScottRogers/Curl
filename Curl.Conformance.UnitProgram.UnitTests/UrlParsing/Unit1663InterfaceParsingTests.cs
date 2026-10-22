using Curl.Cli;
using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1663InterfaceParsingTests
{
    [TestMethod]
    public async Task RunAsync_InterfaceBindingParse_EveryRowPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1663InterfaceParsing(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1663", new Unit1663InterfaceParsing().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_DeviceTextDiffers_FailsOnlyTheEqualityCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1663InterfaceParsing(value => InterfaceBinding.Parse(value == "dev" ? "other" : value)),
            standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1663: dev should be equal to exp_dev" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_EverythingParsesAsBothParts_ResultAndPresenceChecksFailAndTextIsNotCompared()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1663InterfaceParsing(_ => InterfaceBinding.Parse("ifhost!x!y")), standardError);

        string written = standardError.ToString();
        Assert.AreEqual(27, result.ExitCode);
        StringAssert.Contains(written, "unit1663: Curl_parse_interface() failed");
        StringAssert.Contains(written, "unit1663: dev expectation failed.");
        StringAssert.Contains(written, "unit1663: iface expectation failed");
        StringAssert.Contains(written, "unit1663: host expectation failed");
        Assert.DoesNotContain("should be equal", written);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1663InterfaceParsing().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1663InterfaceParsing program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1663, []) { StandardError = standardError }, CancellationToken.None);
}
