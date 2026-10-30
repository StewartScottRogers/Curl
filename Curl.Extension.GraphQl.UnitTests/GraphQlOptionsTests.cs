using Curl.Extension.Abstractions;

namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class GraphQlOptionsTests
{
    [TestMethod]
    public void Description_NamesEveryOptionAndTheSpecification()
    {
        CollectionAssert.AreEqual(
            new[] { "graphql", "graphql-variables", "graphql-operation", "graphql-introspect" },
            GraphQlOptions.Description.Options.Select(option => option.Name).ToArray());
        Assert.AreEqual(ExtensionOptionArgument.None, GraphQlOptions.Introspect.Argument);
        Assert.AreEqual(ExtensionOptionArgument.Value, GraphQlOptions.Document.Argument);
        Assert.IsEmpty(GraphQlOptions.Description.Schemes);
        Assert.AreEqual("https://graphql.github.io/graphql-over-http/draft/", GraphQlOptions.Description.Specification.AbsoluteUri);
    }
}
