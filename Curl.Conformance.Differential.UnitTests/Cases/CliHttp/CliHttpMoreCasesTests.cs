using System.Text;

namespace Curl.Conformance.Differential.UnitTests.Cases.CliHttp;

[TestClass]
public sealed class CliHttpMoreCasesTests
{
    private const string CasePath = "CliHttp/CliHttpMoreCases.json";

    [TestMethod]
    public void ReadEmbedded_CliHttpMoreCases_HoldsEveryCaseOfTheSourceFileOnceEach()
    {
        var cases = DifferentialCaseFileReader.ReadEmbedded(CasePath);

        Assert.HasCount(431, cases);
        Assert.HasCount(431, cases.Select(differentialCase => differentialCase.Name).Distinct(StringComparer.Ordinal));
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpMoreCases_SplitsAWriteOutCaseAsTheSourceDid()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(CasePath).Single(c => c.Name == "redir-write-out-multi");

        CollectionAssert.AreEqual(
            new[] { "-sSL", "-w", "%{url_effective}\\n%{num_redirects}\\n%{time_redirect}", "-o", "NUL", "{U}/r301" },
            differentialCase.Arguments.ToArray());
        Assert.IsFalse(differentialCase.ComparesStandardError);
        Assert.AreEqual(TimeSpan.FromSeconds(20), differentialCase.Timeout);
    }

    [TestMethod]
    public void ReadEmbedded_CliHttpMoreCases_ReadsByteContentAndTimeouts()
    {
        var cases = DifferentialCaseFileReader.ReadEmbedded(CasePath).ToDictionary(c => c.Name, StringComparer.Ordinal);

        Assert.HasCount(3000000, cases["expect-417-big"].Files["big.bin"]);
        CollectionAssert.AreEqual("a\0b\nc"u8.ToArray(), cases["post-d-file-nul"].Files["in.txt"]);
        Assert.AreEqual("stdin-up", Encoding.UTF8.GetString(cases["redir-307-stdin"].StandardInput!));
        Assert.AreEqual(TimeSpan.FromSeconds(60), cases["url-glob-huge"].Timeout);
    }
}
