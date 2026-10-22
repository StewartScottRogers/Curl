using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualPerlValuesTests
{
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("0", false)]
    [DataRow("1", true)]
    [DataRow("yes", true)]
    public void IsTrue_FollowsPerlTruth(string? value, bool expected) =>
        Assert.AreEqual(expected, ManualPerlValues.IsTrue(value));

    [TestMethod]
    public void Split_DropsTrailingEmptyFields() =>
        CollectionAssert.AreEqual(new[] { "a", "", "b" }, ManualPerlValues.Split("a,,b,,", ","));

    [TestMethod]
    public void Split_OfOnlySeparators_IsEmpty() =>
        Assert.AreEqual(0, ManualPerlValues.Split(",,", ",").Count);

    [TestMethod]
    [DataRow("42abc", 42)]
    [DataRow("-7", -7)]
    [DataRow("abc", 0)]
    public void Number_ReadsTheLeadingInteger(string text, int expected) =>
        Assert.AreEqual(expected, ManualPerlValues.Number(text));

    [TestMethod]
    public void Lines_KeepsNewlinesAndTheUnterminatedLastLine() =>
        CollectionAssert.AreEqual(new[] { "a\n", "b\n", "c" }, ManualPerlValues.Lines("a\r\nb\nc"));

    [TestMethod]
    public void Lines_OfTerminatedText_HasNoEmptyLastLine() =>
        CollectionAssert.AreEqual(new[] { "a\n" }, ManualPerlValues.Lines("a\n"));

    [TestMethod]
    [DataRow("a\n", "a")]
    [DataRow("a", "a")]
    public void Chomp_RemovesOneTrailingNewline(string text, string expected) =>
        Assert.AreEqual(expected, ManualPerlValues.Chomp(text));
}
