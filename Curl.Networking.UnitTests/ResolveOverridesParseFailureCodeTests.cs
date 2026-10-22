using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the exit code <see cref="ResolveOverrides.ParseFailureCodeWith" /> gives a connect whose
/// <c>--resolve</c> entries or <c>--connect-to</c> destination do not parse (upstream test1454, BL-2996).
/// </summary>
[TestClass]
public sealed class ResolveOverridesParseFailureCodeTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ParseFailureCodeWith_EntriesParsedAndDestinationHidesIpv6_IsTheDestinationsExit4()
    {
        Diagnostics.Arrange("--connect-to", "localhost:80:[::1]:80 with IPv6 hidden");
        var destination = new ConnectToMappings(["localhost:80:[::1]:80"], hidesIpv6: true).Map("localhost", 80);

        var code = ResolveOverrides.None.ParseFailureCodeWith(destination);

        Diagnostics.Act("code", code);
        Diagnostics.Assert("code", CurlExitCode.NotBuiltIn, code);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, code);
    }

    [TestMethod]
    public void ParseFailureCodeWith_UnparsableEntryAndDestinationHidesIpv6_IsExit49()
    {
        Diagnostics.Arrange("--resolve", "bad");
        var destination = new ConnectToMappings(["localhost:80:[::1]:80"], hidesIpv6: true).Map("localhost", 80);

        var code = ResolveOverrides.Parse(["bad"]).ParseFailureCodeWith(destination);

        Diagnostics.Act("code", code);
        Diagnostics.Assert("code", CurlExitCode.SetoptOptionSyntax, code);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, code);
    }

    [TestMethod]
    public void ParseFailureCodeWith_EntriesParsedAndDestinationPortUnparsable_IsExit49()
    {
        Diagnostics.Arrange("--connect-to", "localhost:80:mapped.test:x");
        var destination = new ConnectToMappings(["localhost:80:mapped.test:x"]).Map("localhost", 80);

        var code = ResolveOverrides.None.ParseFailureCodeWith(destination);

        Diagnostics.Act("code", code);
        Diagnostics.Assert("code", CurlExitCode.SetoptOptionSyntax, code);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, code);
    }
}
