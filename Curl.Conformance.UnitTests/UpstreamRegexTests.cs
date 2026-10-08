using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Pins how <see cref="UpstreamRegex"/> compiles the patterns of a case's strip parts.
/// </summary>
[TestClass]
public sealed class UpstreamRegexTests
{
    [TestMethod]
    public void TryCreate_WithoutATimeout_GivesEachMatchTenSecondsSoARunnerStallCannotCutItShort()
    {
        Regex? regex = UpstreamRegex.TryCreate("^Date:", RegexOptions.None);

        Assert.IsNotNull(regex);
        Assert.AreEqual(TimeSpan.FromSeconds(10), regex.MatchTimeout);
    }

    [TestMethod]
    public void TryCreate_WithATimeout_GivesEachMatchThatTime()
    {
        Regex? regex = UpstreamRegex.TryCreate("^Date:", RegexOptions.None, TimeSpan.FromMilliseconds(250));

        Assert.IsNotNull(regex);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), regex.MatchTimeout);
    }

    [TestMethod]
    public void TryCreate_PatternDotNetCannotRead_ReturnsNull()
    {
        Regex? regex = UpstreamRegex.TryCreate("(", RegexOptions.None);

        Assert.IsNull(regex);
    }
}
