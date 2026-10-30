using System.Text;

namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class GraphQlErrorListingTests
{
    [TestMethod]
    public void LinesOf_ResponseErrors_ListsMessageLocationsAndPath()
    {
        IReadOnlyList<string> lines = LinesOf(
            "{\"data\":null,\"errors\":[{\"message\":\"Unknown field\",\"locations\":[{\"line\":2,\"column\":3},{\"line\":4,\"column\":1}],\"path\":[\"users\",0,\"name\"]},"
            + "{\"message\":\"Second\"}]}");

        CollectionAssert.AreEqual(
            new[] { "GraphQL error: Unknown field (line 2, column 3; line 4, column 1) at path users.0.name", "GraphQL error: Second" },
            lines.ToArray());
    }

    [TestMethod]
    public void LinesOf_MalformedErrorEntries_ListsWhatIsReadable()
    {
        IReadOnlyList<string> lines = LinesOf(
            "{\"errors\":[1,{\"message\":5,\"locations\":[1,{\"line\":\"x\",\"column\":1},{\"line\":1},{\"line\":1,\"column\":\"y\"}],\"path\":[]},"
            + "{\"locations\":{},\"path\":\"a\"}]}");

        CollectionAssert.AreEqual(new[] { "GraphQL error: (no message)", "GraphQL error: (no message)" }, lines.ToArray());
    }

    [TestMethod]
    public void LinesOf_ResponseWithoutErrors_ListsNothing()
    {
        Assert.IsEmpty(LinesOf("{\"data\":{\"a\":1}}"));
        Assert.IsEmpty(LinesOf("{\"errors\":{}}"));
        Assert.IsEmpty(LinesOf("[1]"));
        Assert.IsEmpty(LinesOf("<html>"));
    }

    private static IReadOnlyList<string> LinesOf(string body) => GraphQlErrorListing.LinesOf(Encoding.UTF8.GetBytes(body));
}
