using System.Text;

namespace Curl.Conformance.Differential.UnitTests.Cases.CliHttp;

[TestClass]
public sealed class CliHttpCasesTests
{
    private const string CasePath = "CliHttp/CliHttpCases.json";

    [TestMethod]
    public void ReadEmbedded_CliHttpCases_HoldsEveryCaseTheTestersCasesPyProduces()
    {
        var cases = DifferentialCaseFileReader.ReadEmbedded(CasePath);

        Assert.AreEqual(454, cases.Count);
        Assert.AreEqual("get", cases[0].Name);
        Assert.AreEqual("login-options", cases[^1].Name);
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpCases_RepeatsOnlyTheTwoNamesTheTestersCasesPyRepeats()
    {
        var repeatedNames = DifferentialCaseFileReader.ReadEmbedded(CasePath)
            .GroupBy(differentialCase => differentialCase.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "w-onerror", "w-stderr" }, repeatedNames);
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpCases_ReadsTheDataUrlencodeFileCaseArgumentsAndFile()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(CasePath).Single(candidate => candidate.Name == "data-urlencode-3");

        CollectionAssert.AreEqual(new[] { "-s", "--data-urlencode", "n@in.txt", "{U}/echo" }, differentialCase.Arguments.ToArray());
        Assert.AreEqual("a b\n", Encoding.UTF8.GetString(differentialCase.Files["in.txt"]));
        Assert.AreEqual(TimeSpan.FromSeconds(20), differentialCase.Timeout);
        Assert.IsTrue(differentialCase.ComparesStandardError);
        Assert.IsTrue(differentialCase.ComparesWire);
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpCases_ExpandsTheWriteOutLoopWithItsSubstitutedName()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(CasePath).Single(candidate => candidate.Name == "w-header_x_a__1_");

        CollectionAssert.AreEqual(new[] { "-s", "-o", "NUL", "-w", "%{header{x-a}[1]}", "{U}/dupheaders" }, differentialCase.Arguments.ToArray());
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpCases_KeepsTheRepeatedByteBodyTheTimeoutAndTheHttp2Limitation()
    {
        var cases = DifferentialCaseFileReader.ReadEmbedded(CasePath);

        Assert.AreEqual(2_000_000, cases.Single(candidate => candidate.Name == "post-big-expect").Files["big.bin"].Length);
        var http2PriorKnowledge = cases.Single(candidate => candidate.Name == "http2-prior");
        Assert.AreEqual(TimeSpan.FromSeconds(8), http2PriorKnowledge.Timeout);
        CollectionAssert.AreEqual(new[] { ReferenceBuildLimitation.NoHttp2 }, http2PriorKnowledge.Limitations.ToArray());
        Assert.IsFalse(cases.Single(candidate => candidate.Name == "aws-sigv4-missing").ComparesWire);
        Assert.IsFalse(cases.Single(candidate => candidate.Name == "progress-bar-out").ComparesStandardError);
    }
}
