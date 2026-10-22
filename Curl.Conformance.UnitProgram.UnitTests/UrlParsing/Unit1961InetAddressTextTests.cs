using System.Net;
using Curl.Conformance.UnitProgram.UrlParsing;

namespace Curl.Conformance.UnitProgram.UnitTests.UrlParsing;

[TestClass]
public sealed class Unit1961InetAddressTextTests
{
    [TestMethod]
    public async Task RunAsync_CurlsAddressTextAndParsing_BothChecksPassAndNothingIsWritten()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1961InetAddressText(), standardError);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1961", new Unit1961InetAddressText().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_NtopWritesTheMappedFormInsteadOfTheCompatibleOne_FailsOnlyTheNtopCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1961InetAddressText(bytes => bytes[0] == 0 ? "::ffff:127.0.0.1" : new IPAddress(bytes).ToString(), (text, _) => IPAddress.Parse(text)),
            standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1961: curlx_inet_ntop()" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_PtonRefusesEveryAddress_FailsOnlyThePtonCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1961InetAddressText(bytes => new IPAddress(bytes).ToString(), (_, _) => null),
            standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1961: curlx_inet_pton()" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_PtonReadsTheWrongBytes_FailsThePtonCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(
            new Unit1961InetAddressText(bytes => new IPAddress(bytes).ToString(), (_, _) => IPAddress.Loopback),
            standardError);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1961: curlx_inet_pton()" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1961InetAddressText().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1961InetAddressText program, StringWriter standardError) =>
        program.RunAsync(new UnitProgramContext(1961, []) { StandardError = standardError }, CancellationToken.None);
}
