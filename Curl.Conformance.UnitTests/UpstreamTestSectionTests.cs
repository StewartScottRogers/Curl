using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins <see cref="UpstreamTestSection"/>'s attribute reading.</summary>
[TestClass]
public sealed class UpstreamTestSectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("yes", true)]
    [DataRow("no", true)]
    [DataRow("headers", true)]
    [DataRow("0", false)]
    [DataRow("", false)]
    public void IsAttributeSet_Value_FollowsPerlTruthiness(string value, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("nonewline attribute", value);
        UpstreamTestSection section = new("verify", "stdout", new Dictionary<string, string> { ["nonewline"] = value }, ReadOnlyMemory<byte>.Empty, 1);

        bool actual = section.IsAttributeSet("nonewline");

        diagnostics.Act("is set", actual);
        diagnostics.Assert("is set", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void IsAttributeSet_MissingAttribute_IsFalse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attributes", "(none)");
        UpstreamTestSection section = new("verify", "stdout", new Dictionary<string, string>(), ReadOnlyMemory<byte>.Empty, 1);

        bool isSet = section.IsAttributeSet("crlf");
        string? attribute = section.GetAttribute("crlf");

        diagnostics.Act("is set", isSet);
        diagnostics.Act("attribute", attribute);
        diagnostics.Assert("is set", false, isSet);
        diagnostics.Assert("attribute", null, attribute);
        Assert.IsFalse(section.IsAttributeSet("crlf"));
        Assert.IsNull(section.GetAttribute("crlf"));
    }
}
