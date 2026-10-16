namespace Curl.Api.Stores;

[TestClass]
public sealed class CurlAltSvcEntryTests
{
    [TestMethod]
    public void ToFileLine_WritesCurlsTenFields()
    {
        CurlAltSvcEntry entry = new("h2", "a.example", 443, "h3", "b.example", 8443, "20271231 01:02:03", Persist: true, Priority: 0);

        Assert.AreEqual("h2 a.example 443 h3 b.example 8443 \"20271231 01:02:03\" 1 0", entry.ToFileLine());
    }

    [TestMethod]
    public void FromFileLine_ReadsCurlsTenFields()
    {
        CurlAltSvcEntry? entry = CurlAltSvcEntry.FromFileLine("  h1 a.example 80 h2 [::1] 8080 \"20271231 01:02:03\" 0 7 ");

        Assert.AreEqual(new CurlAltSvcEntry("h1", "a.example", 80, "h2", "[::1]", 8080, "20271231 01:02:03", Persist: false, Priority: 7), entry);
    }

    [TestMethod]
    [DataRow("h2 a.example 443 h3 b.example 443 \"20271231 01:02:03\" 0")]
    [DataRow("h2 a.example x h3 b.example 443 \"20271231 01:02:03\" 0 0")]
    [DataRow("h2 a.example 443 h3 b.example x \"20271231 01:02:03\" 0 0")]
    [DataRow("h2 a.example 443 h3 b.example 443 \"20271231 01:02:03\" x 0")]
    [DataRow("h2 a.example 443 h3 b.example 443 \"20271231 01:02:03\" 0 x")]
    public void FromFileLine_MalformedLine_IsNull(string line)
    {
        Assert.IsNull(CurlAltSvcEntry.FromFileLine(line));
    }

    [TestMethod]
    public void FromFileLine_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlAltSvcEntry.FromFileLine(null!));
    }
}
