using System.Text;

namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class DifferentialCaseFileReaderTests
{
    [TestMethod]
    public void Read_FullCase_ReadsEveryProperty()
    {
        const string Json = """
            {"cases": [{"name": "cookies2-jar-send", "command": "-sS -c jar.txt {U}/cookies2", "stdin": "body",
              "files": {"in.txt": "é"}, "timeoutSeconds": 5, "compareStderr": false, "compareWire": false,
              "limitations": ["NoHttp2", "BrokenIdn"]}]}
            """;

        var differentialCase = DifferentialCaseFileReader.Read(Json).Single();

        Assert.AreEqual("cookies2-jar-send", differentialCase.Name);
        CollectionAssert.AreEqual(new[] { "-sS", "-c", "jar.txt", "{U}/cookies2" }, differentialCase.Arguments.ToArray());
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("body"), differentialCase.StandardInput);
        CollectionAssert.AreEqual(new byte[] { 0xc3, 0xa9 }, differentialCase.Files["in.txt"]);
        Assert.AreEqual(TimeSpan.FromSeconds(5), differentialCase.Timeout);
        Assert.IsFalse(differentialCase.ComparesStandardError);
        Assert.IsFalse(differentialCase.ComparesWire);
        CollectionAssert.AreEqual(new[] { ReferenceBuildLimitation.NoHttp2, ReferenceBuildLimitation.BrokenIdn }, differentialCase.Limitations.ToArray());
        Assert.IsFalse(differentialCase.RunsAgainstReferenceBuild);
    }

    [TestMethod]
    public void Read_MinimalCase_TakesTheDefaults()
    {
        var differentialCase = DifferentialCaseFileReader.Read("""{"cases": [{"name": "a", "command": "-s {U}/"}]}""").Single();

        Assert.IsNull(differentialCase.StandardInput);
        Assert.IsEmpty(differentialCase.Files);
        Assert.AreEqual(TimeSpan.FromSeconds(20), differentialCase.Timeout);
        Assert.IsTrue(differentialCase.ComparesStandardError);
        Assert.IsTrue(differentialCase.ComparesWire);
        Assert.IsTrue(differentialCase.RunsAgainstReferenceBuild);
    }

    [TestMethod]
    public void Read_NullName_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() => DifferentialCaseFileReader.Read("""{"cases": [{"name": null, "command": ""}]}"""));
    }
}
