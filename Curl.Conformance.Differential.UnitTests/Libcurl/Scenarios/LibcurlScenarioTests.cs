using System.Text;
using Curl.Conformance.Differential.Libcurl.Scenarios;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Scenarios;

[TestClass]
public sealed class LibcurlScenarioTests
{
    [TestMethod]
    public void Read_EveryKind_ReadsItsInputs()
    {
        const string Json = """
            [["V"], ["S"], ["U", "http://a/", 4], ["P", "http://a/", 5, 128, "h"], ["P", "", 7, 0, null],
             ["E", "61FF"], ["N", ""], ["D", "Jan 1 2000"]]
            """;

        var scenarios = LibcurlScenarioFileReader.Read(Json);

        CollectionAssert.AreEqual(new[] { "V", "S", "U", "P", "P", "E", "N", "D" }, scenarios.Select(scenario => scenario.Kind).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(0, 8).ToArray(), scenarios.Select(scenario => scenario.Index).ToArray());
        Assert.AreEqual("http://a/", scenarios[2].Text);
        Assert.AreEqual(4, scenarios[2].Flags);
        Assert.AreEqual(5, scenarios[3].Part);
        Assert.AreEqual(128, scenarios[3].Flags);
        Assert.AreEqual("h", scenarios[3].Value);
        Assert.IsNull(scenarios[4].Value);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0xff }, scenarios[5].Bytes);
        Assert.IsEmpty(scenarios[6].Bytes);
        Assert.AreEqual("Jan 1 2000", scenarios[7].Text);
    }

    [TestMethod]
    public void Read_UnknownKind_ThrowsFormatException()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlScenarioFileReader.Read("""[["V"], ["X"]]"""));

        Assert.AreEqual("Scenario 1 has the unknown kind 'X'.", exception.Message);
    }

    [TestMethod]
    public void Read_NullKind_ThrowsFormatException()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => LibcurlScenarioFileReader.Read("[[null]]"));

        Assert.AreEqual("Scenario 0 has the unknown kind ''.", exception.Message);
    }

    [TestMethod]
    [DataRow("http://bücher.example/")]
    [DataRow("bücher.example")]
    [DataRow("http://u@日本語.jp:80?q")]
    public void Limitation_UrlWithNonAsciiHost_IsBrokenIdn(string url)
    {
        var scenario = new LibcurlScenario(0, "U", url, 0, 0, null, []);

        Assert.AreEqual(ReferenceBuildLimitation.BrokenIdn, scenario.Limitation);
        Assert.IsFalse(scenario.RunsAgainstReferenceBuild);
    }

    [TestMethod]
    [DataRow("http://a/é")]
    [DataRow("http://ü:ü@h/")]
    [DataRow("http://a#é")]
    public void Limitation_UrlWithAsciiHost_IsNone(string url)
    {
        var scenario = new LibcurlScenario(0, "U", url, 0, 0, null, []);

        Assert.IsNull(scenario.Limitation);
        Assert.IsTrue(scenario.RunsAgainstReferenceBuild);
    }

    [TestMethod]
    [DataRow(5, "é.example", true)]
    [DataRow(5, "example.org", false)]
    [DataRow(5, null, false)]
    [DataRow(7, "/é", false)]
    public void Limitation_PartScenario_IsBrokenIdnOnlyForNonAsciiHost(int part, string? value, bool brokenIdn)
    {
        var scenario = new LibcurlScenario(0, "P", "", part, 0, value, []);

        Assert.AreEqual(brokenIdn, scenario.Limitation is ReferenceBuildLimitation.BrokenIdn);
    }

    [TestMethod]
    public void Limitation_OtherKind_IsNone() =>
        Assert.IsNull(new LibcurlScenario(0, "E", null, 0, 0, null, Encoding.UTF8.GetBytes("é")).Limitation);
}
