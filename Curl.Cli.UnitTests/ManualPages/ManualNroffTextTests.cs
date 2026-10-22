using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualNroffTextTests
{
    [TestMethod]
    [DataRow("max-time", true, @"\fI\-\-max\-time\fP")]
    [DataRow("max-time.", true, @"\fI\-\-max\-time\fP.")]
    [DataRow("max-time", false, "--max-time")]
    [DataRow("max-time.", false, "--max-time.")]
    public void Manpageify_FormatsTheOptionForThePage(string name, bool manpage, string expected) =>
        Assert.AreEqual(expected, ManualNroffText.Manpageify(name, manpage));

    [TestMethod]
    [DataRow("7.65.9", true)]
    [DataRow("7.66.0", false)]
    [DataRow("7.65", true)]
    [DataRow("7.70", false)]
    [DataRow("unknown", false)]
    public void IsTooOld_ComparesWithVersion7660(string version, bool expected) =>
        Assert.AreEqual(expected, ManualNroffText.IsTooOld(version));

    [TestMethod]
    [DataRow("one two", "one ")]
    [DataRow("one twentyonecharacterslong", "one twentyonecharacterslong")]
    [DataRow("key:value", "key:")]
    [DataRow("plain", "plain")]
    public void MaybeSpace_CutsAtTheLastBreakWhenTheRestIsShort(string text, string expected) =>
        Assert.AreEqual(expected, ManualNroffText.MaybeSpace(text));
}
