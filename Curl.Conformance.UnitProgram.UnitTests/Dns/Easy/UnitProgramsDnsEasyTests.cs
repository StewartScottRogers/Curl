using Curl.Api.Codes;
using Curl.Api.Options.Authentication;
using Curl.Conformance.UnitProgram.Dns.Easy;

namespace Curl.Conformance.UnitProgram.UnitTests.Dns.Easy;

/// <summary>Runs the ports of unit1652, unit2413, unit3219 and unit3302, against Curl and against broken stand-ins.</summary>
[TestClass]
public sealed class UnitProgramsDnsEasyTests
{
    [TestMethod]
    [DataRow("unit1652")]
    [DataRow("unit2413")]
    [DataRow("unit3219")]
    [DataRow("unit3302")]
    public async Task RunAsync_OverCurl_EveryCheckHolds(string programName)
    {
        IUnitProgram program = UnitPrograms.Ported.Find(programName)!;
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(1, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, standardError.ToString());
        Assert.DoesNotContain("FAILED", standardError.ToString());
    }

    [TestMethod]
    public async Task Unit1652_DeliveryThatNeverCuts_FailsEveryCheck()
    {
        Unit1652InfoLineTruncation program = new(line => line);

        UnitProgramResult result = await Run(program);

        Assert.AreEqual(12, result.ExitCode);
    }

    [TestMethod]
    public async Task Unit3219_ReaderThatNeverMasks_FailsTheThreeExcludedBits()
    {
        Unit3219ProtocolMask program = new(bit => bit);

        UnitProgramResult result = await Run(program);

        Assert.AreEqual(3, result.ExitCode);
    }

    [TestMethod]
    public async Task Unit3302_SetterThatFailsAndStoresNothingUseful_FailsBothChecksOfEachRow()
    {
        Unit3302GssApiDelegation program = new((_, _) => (CurlCode.UnknownOption, (GssApiDelegation)7));

        UnitProgramResult result = await Run(program);

        Assert.AreEqual(10, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1652InfoLineTruncation().RunAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit2413ConnectionPeer().RunAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3219ProtocolMask().RunAsync(null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit3302GssApiDelegation().RunAsync(null!, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("http", "", "", 1234, false, null, "peer1: has wrong scheme", 7)]
    [DataRow("https", "x", "", 1234, false, null, "peer1: user_hostname=x, expected test.curl.se", 7)]
    [DataRow("https", null, "x", 1234, false, null, "peer1: hostname=x, expected test.curl.se", 7)]
    [DataRow("https", null, null, 80, false, null, "peer1: port=80, expected 1234", 7)]
    [DataRow("https", null, null, 1234, true, null, "peer1: ipv6=1, expected 0", 6)]
    [DataRow("https", null, null, 1234, false, "z", "peer1: zoneid=z, expected nothing", 7)]
    public async Task Unit2413_PeerWrongInOneField_WritesTheCHelpersMismatchForEachRow(string scheme, string? user, string? hostname, int port, bool isIpv6, string? zoneId, string firstMismatch, int failures)
    {
        Unit2413ConnectionPeer program = new(host => new Unit2413ConnectionPeer.Peer(scheme, user ?? host, hostname ?? host, port, isIpv6, zoneId));
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(2413, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(failures, result.ExitCode);
        StringAssert.StartsWith(standardError.ToString(), firstMismatch);
    }

    [TestMethod]
    public async Task Unit2413_PeerWithNoZone_NamesTheExpectedZone()
    {
        Unit2413ConnectionPeer program = new(host => new Unit2413ConnectionPeer.Peer("https", host, host.Trim('[', ']').Split('%')[0], 1234, host.Contains(':'), null));
        using StringWriter standardError = new();

        UnitProgramResult result = await program.RunAsync(new UnitProgramContext(2413, []) { StandardError = standardError }, CancellationToken.None);

        Assert.AreEqual(2, result.ExitCode);
        StringAssert.Contains(standardError.ToString(), "peer6: zoneid=, expected tada");
    }

    private static async Task<UnitProgramResult> Run(IUnitProgram program)
    {
        using StringWriter standardError = new();
        return await program.RunAsync(new UnitProgramContext(1, []) { StandardError = standardError }, CancellationToken.None);
    }
}
