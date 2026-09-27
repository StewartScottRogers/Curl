namespace Curl.Conformance;

/// <summary>Pins <see cref="UpstreamTestSection"/>'s attribute reading.</summary>
[TestClass]
public sealed class UpstreamTestSectionTests
{
    [TestMethod]
    [DataRow("yes", true)]
    [DataRow("no", true)]
    [DataRow("headers", true)]
    [DataRow("0", false)]
    [DataRow("", false)]
    public void IsAttributeSet_Value_FollowsPerlTruthiness(string value, bool expected)
    {
        UpstreamTestSection section = new("verify", "stdout", new Dictionary<string, string> { ["nonewline"] = value }, ReadOnlyMemory<byte>.Empty, 1);

        Assert.AreEqual(expected, section.IsAttributeSet("nonewline"));
    }

    [TestMethod]
    public void IsAttributeSet_MissingAttribute_IsFalse()
    {
        UpstreamTestSection section = new("verify", "stdout", new Dictionary<string, string>(), ReadOnlyMemory<byte>.Empty, 1);

        Assert.IsFalse(section.IsAttributeSet("crlf"));
        Assert.IsNull(section.GetAttribute("crlf"));
    }
}
