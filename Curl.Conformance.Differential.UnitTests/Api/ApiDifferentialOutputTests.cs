using System.Text;
using Curl.Conformance.Differential.Api;

namespace Curl.Conformance.Differential.UnitTests.Api;

[TestClass]
public sealed class ApiDifferentialOutputTests
{
    [TestMethod]
    public void Render_BodyAndCode_WritesTheBodyANewlineAndTheThreeDigitCode()
    {
        var output = ApiDifferentialOutput.Render("ok"u8, 200);

        Assert.AreEqual("ok\n200", Encoding.ASCII.GetString(output));
    }

    [TestMethod]
    public void Render_NoResponse_WritesTheCodeAsThreeZeros()
    {
        var output = ApiDifferentialOutput.Render([], 0);

        Assert.AreEqual("\n000", Encoding.ASCII.GetString(output));
    }
}
