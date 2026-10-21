using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Names;
using Curl.Output;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Names;

[TestClass]
public sealed class Unit1676DhPublicKeyTests
{
    [TestMethod]
    public async Task RunAsync_PeerCertificateText_EveryCheckPassesAndNothingIsWritten()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(PeerCertificateText.Format, true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1676", new Unit1676DhPublicKey().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(_ => throw new AssertFailedException("not run"), false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since Curl_extract_certinfo() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1676DhPublicKey.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_CertificateNotRead_CountsOneFailure()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(_ => string.Empty, true), standardError, Stream.Null);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1676: Curl_extract_certinfo returned error" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    [DataRow("dh(g):34\ndh(pub_key):51\n", "dh(p) not found in certinfo")]
    [DataRow("dh(p):17\ndh(pub_key):51\n", "dh(g) not found in certinfo")]
    [DataRow("dh(p):17\ndh(g):34\n", "dh(pub_key) not found in certinfo")]
    public async Task RunAsync_RecordMissing_AbortsWithOneFailure(string records, string expected)
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(_ => records, true), standardError, Stream.Null);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1676: " + expected + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_GThatRereadsP_CountsTheSameValueAndTheWrongValues()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1676DhPublicKey(_ => "dh(p):1\ndh(g):1\ndh(pub_key):2\n", true), standardError, Stream.Null);

        string newLine = Environment.NewLine;
        Assert.AreEqual(4, result.ExitCode);
        Assert.AreEqual(
            "unit1676: dh(p) and dh(g) have the same value (bug: g re-reads p)" + newLine
            + "unit1676: dh(p) expected 17 (0x11)" + newLine
            + "unit1676: dh(g) expected 34 (0x22)" + newLine
            + "unit1676: dh(pub_key) expected 51 (0x33)" + newLine,
            standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1676DhPublicKey().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1676DhPublicKey program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1676, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
