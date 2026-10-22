using System.Text;
using Curl.Conformance.UnitProgram.Asn1.Certinfo;

namespace Curl.Conformance.UnitProgram.UnitTests.Asn1.Certinfo;

[TestClass]
public sealed class Unit1651CertificateExtractionTests
{
    [TestMethod]
    public async Task RunAsync_CurlExtractor_ReadsTheCertificateSurvivesTheFuzzingAndWritesNothing()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1651CertificateExtraction(true), standardError, standardOutput);

        Assert.AreEqual(string.Empty, standardError.ToString());
        Assert.AreEqual(0L, standardOutput.Length);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("unit1651", new Unit1651CertificateExtraction().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_OpenSslBuild_PrintsNotTestedAndPasses()
    {
        using StringWriter standardError = new();
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1651CertificateExtraction(false), standardError, standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("not tested since Curl_extract_certinfo() is not built in\n", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.AreEqual(string.Empty, standardError.ToString());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOnWindows_RunsTheChecks()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1651CertificateExtraction(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_RegisteredPortOffWindows_PrintsNotTested()
    {
        using MemoryStream standardOutput = new();

        UnitProgramResult result = await RunAsync(new Unit1651CertificateExtraction(), new StringWriter(), standardOutput);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(Unit1651CertificateExtraction.NotTestedLine, Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ExtractorThatRefusesEverything_FailsOnlyTheFirstCheck()
    {
        using StringWriter standardError = new();

        UnitProgramResult result = await RunAsync(new Unit1651CertificateExtraction(_ => string.Empty, true), standardError, Stream.Null);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1651: Curl_extract_certinfo returned error" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ExtractorThatThrowsOnOneFuzzedCertificate_CountsThatOneFailure()
    {
        using StringWriter standardError = new();
        byte[] original = Unit1651Certificate.Create();
        Unit1651CertificateExtraction program = new(
            certificate => certificate.Span[44] == 239 && original[44] != 239 ? throw new IndexOutOfRangeException("past the end") : "Subject:x\n",
            true);

        UnitProgramResult result = await RunAsync(program, standardError, Stream.Null);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("unit1651: Curl_extract_certinfo threw IndexOutOfRangeException: past the end" + Environment.NewLine, standardError.ToString());
    }

    [TestMethod]
    public async Task RunAsync_FuzzingLoop_ExtractsOnceThenFifteenValuesAtEachOfTheFirst45BytesAndRestoresEach()
    {
        byte[] original = Unit1651Certificate.Create();
        List<byte[]> extracted = [];
        Unit1651CertificateExtraction program = new(
            certificate =>
            {
                extracted.Add(certificate.ToArray());
                return "Subject:x\n";
            },
            true);

        await RunAsync(program, new StringWriter(), Stream.Null);

        Assert.HasCount(1 + (15 * 45), extracted);
        CollectionAssert.AreEqual(original, extracted[0]);
        byte[] last = extracted[^1];
        Assert.AreEqual((byte)239, last[44]);
        last[44] = original[44];
        CollectionAssert.AreEqual(original, last);
        Assert.AreEqual((byte)1, extracted[1][0]);
        Assert.AreEqual((byte)18, extracted[46][0]);
    }

    [TestMethod]
    public async Task RunAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => new Unit1651CertificateExtraction().RunAsync(null!, CancellationToken.None));
    }

    private static Task<UnitProgramResult> RunAsync(Unit1651CertificateExtraction program, StringWriter standardError, Stream standardOutput) =>
        program.RunAsync(new UnitProgramContext(1651, []) { StandardError = standardError, StandardOutput = standardOutput }, CancellationToken.None);
}
