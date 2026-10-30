using System.Xml.Linq;
using Curl.Extension.WebDavTools.Propfind;

namespace Curl.Extension.WebDavTools.UnitTests.Propfind;

[TestClass]
public sealed class WebDavPropfindRequestTests
{
    private static readonly XNamespace Dav = "DAV:";

    [TestMethod]
    public void Body_IsAPropfindInTheDavNamespace()
    {
        var root = XDocument.Parse(WebDavPropfindRequest.Body).Root!;

        Assert.AreEqual(Dav + "propfind", root.Name);
    }

    [TestMethod]
    public void Body_PropNamesTheFourListingPropertiesAndNothingElse()
    {
        var prop = XDocument.Parse(WebDavPropfindRequest.Body).Root!.Elements().Single();

        Assert.AreEqual(Dav + "prop", prop.Name);
        CollectionAssert.AreEqual(
            new[] { Dav + "resourcetype", Dav + "getcontentlength", Dav + "getlastmodified", Dav + "getetag" },
            prop.Elements().Select(element => element.Name).ToArray());
        Assert.IsTrue(prop.Elements().All(element => element.IsEmpty));
    }

    [TestMethod]
    [DataRow(WebDavDepth.Zero, "0")]
    [DataRow(WebDavDepth.One, "1")]
    [DataRow(WebDavDepth.Infinity, "infinity")]
    public void DepthHeaderValue_GivesRfc4918Section10_2Value(WebDavDepth depth, string expected) =>
        Assert.AreEqual(expected, WebDavPropfindRequest.DepthHeaderValue(depth));

    [TestMethod]
    public void DepthHeaderValue_UndefinedDepth_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WebDavPropfindRequest.DepthHeaderValue((WebDavDepth)7));
}
