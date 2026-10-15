using Curl.Testing;

namespace Curl.Conformance;

/// <summary>Pins how <see cref="UpstreamTestAlternatives"/> resolves <c>%alternatives[A,B]</c> as <c>getpart.pm</c>'s <c>compareparts</c> does.</summary>
[TestClass]
public sealed class UpstreamTestAlternativesTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("plain\n", "other\n", "plain\n")]
    [DataRow("a %alternatives[1,2] b\n", "a 1 b\n", "a 1 b\n")]
    [DataRow("a %alternatives[1,2] b\n", "a 2 b\n", "a 2 b\n")]
    [DataRow("%alternatives[1,2]-%alternatives[3,4]\n", "2-3\n", "2-3\n")]
    [DataRow("%alternatives[a,ab]c", "abc", "abc")]
    [DataRow("%alternatives[1,2]-%alternatives[3,4]\n", "1-5\n", "2-4\n")]
    [DataRow("%alternatives[1\n", "1\n", "%alternatives[1\n")]
    [DataRow("%alternatives[1,2\n", "1\n", "%alternatives[1,2\n")]
    public void Choose_PicksTheAlternativesThatMatchElseTheSecond(string expected, string actual, string chosen)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("expected", expected);
        diagnostics.Arrange("actual", actual);

        string result = UpstreamTestAlternatives.Choose(expected, actual);

        diagnostics.Act("chosen", result);
        diagnostics.Assert("chosen", chosen, result);
        Assert.AreEqual(chosen, result);
    }

    [TestMethod]
    public void Choose_ManyAlternativesWithAMismatchAtTheEnd_ReturnsTheSecondOfEach()
    {
        string expected = string.Concat(Enumerable.Repeat("%alternatives[1,2]\n", 40)) + "end\n";
        string actual = string.Concat(Enumerable.Repeat("1\n", 40)) + "END\n";

        string result = UpstreamTestAlternatives.Choose(expected, actual);

        Assert.AreEqual(string.Concat(Enumerable.Repeat("2\n", 40)) + "end\n", result);
    }
}
