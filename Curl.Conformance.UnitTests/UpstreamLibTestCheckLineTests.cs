namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamLibTestCheckLineTests
{
    [TestMethod]
    public void Parse_Test678Precheck_GivesTheProgramAndItsUrl()
    {
        (string Program, string[] Arguments)? check = UpstreamLibTestCheckLine.Parse("./libtest/libtests lib678 check");

        Assert.IsNotNull(check);
        Assert.AreEqual("lib678", check.Value.Program);
        CollectionAssert.AreEqual(new[] { "check" }, check.Value.Arguments);
    }

    [TestMethod]
    public void Parse_ProgramWithNoArguments_GivesNoArguments()
    {
        (string Program, string[] Arguments)? check = UpstreamLibTestCheckLine.Parse("./libtest/libtests lib500");

        Assert.IsNotNull(check);
        Assert.IsEmpty(check.Value.Arguments);
    }

    [TestMethod]
    [DataRow("./libtest/libtests")]
    [DataRow("./libtest/libtests unit1300 check")]
    [DataRow("./libtest/libtests lib check")]
    [DataRow("./libtest/libtests lib67x check")]
    [DataRow("./libtest/libtests libABC check")]
    [DataRow("perl -e 'print 1'")]
    [DataRow("")]
    public void Parse_LineNotRunningALibTestProgram_IsNull(string line)
    {
        Assert.IsNull(UpstreamLibTestCheckLine.Parse(line));
    }
}
