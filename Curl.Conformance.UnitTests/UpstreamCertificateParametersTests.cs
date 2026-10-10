namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamCertificateParameters"/> reads an upstream <c>.prm</c> file.</summary>
[TestClass]
public sealed class UpstreamCertificateParametersTests
{
    [TestMethod]
    public void Parse_ParametersWithComments_KeepsSectionsInOrder()
    {
        var parameters = UpstreamCertificateParameters.Parse("# c\na = 1\n\n[ s ]\nb = 2\nc = x=y\n");
        Assert.AreEqual("1", parameters.Find(string.Empty, "a"));
        Assert.AreEqual("b,c", string.Join(",", parameters.Section("s").Select(pair => pair.Key)));
        Assert.AreEqual("x=y", parameters.Find("s", "c"));
        Assert.IsNull(parameters.Find("s", "z"));
        Assert.IsEmpty(parameters.Section("missing"));
    }

    [TestMethod]
    [DataRow("no equals sign")]
    [DataRow("= value without a key")]
    [DataRow("[ unclosed section")]
    public void Parse_LineWithoutKey_Throws(string line)
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => UpstreamCertificateParameters.Parse(line));
        StringAssert.Contains(exception.Message, line);
    }
}
