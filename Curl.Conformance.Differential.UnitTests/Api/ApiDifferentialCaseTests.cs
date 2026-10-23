using Curl.Conformance.Differential.Api;

namespace Curl.Conformance.Differential.UnitTests.Api;

[TestClass]
public sealed class ApiDifferentialCaseTests
{
    [TestMethod]
    public void ToDifferentialCase_DigestCommand_PutsTheLeadingArgumentsBeforeTheSplitCommand()
    {
        var apiCase = new ApiDifferentialCase("api-auth-digest", "-s --digest -u 'u:p' {U}/digest", new("GET", "/digest"));

        var differentialCase = apiCase.ToDifferentialCase();

        Assert.AreEqual("api-auth-digest", differentialCase.Name);
        CollectionAssert.AreEqual(
            new[] { "-A", "", "-w", "\\n%{response_code}", "-s", "--digest", "-u", "u:p", "{U}/digest" },
            differentialCase.Arguments.ToArray());
        Assert.IsNull(differentialCase.StandardInput);
        Assert.IsEmpty(differentialCase.Files);
        Assert.AreEqual(TimeSpan.FromSeconds(20), differentialCase.Timeout);
        Assert.IsTrue(differentialCase.ComparesStandardError);
        Assert.IsTrue(differentialCase.ComparesWire);
        Assert.IsTrue(differentialCase.RunsAgainstReferenceBuild);
    }
}
