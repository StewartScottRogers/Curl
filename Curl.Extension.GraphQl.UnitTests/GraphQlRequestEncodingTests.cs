using System.Text;

namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class GraphQlRequestEncodingTests
{
    [TestMethod]
    public void Encode_RequestParameters_WithOnlyAQuery_WritesOnlyQuery()
    {
        byte[] body = GraphQlRequestEncoding.Encode("{ me { name } }", null, null);

        Assert.AreEqual("{\"query\":\"{ me { name } }\"}", Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void Encode_RequestParameters_WithOperationAndVariables_WritesAllThree()
    {
        byte[] body = GraphQlRequestEncoding.Encode(
            "query A($id: ID) { a(id: $id) } query B { b }",
            "A",
            GraphQlRequestEncoding.VariablesOf("{ \"id\": 7 }"));

        Assert.AreEqual(
            "{\"query\":\"query A($id: ID) { a(id: $id) } query B { b }\",\"operationName\":\"A\",\"variables\":{\"id\":7}}",
            Encoding.UTF8.GetString(body));
    }

    [TestMethod]
    public void VariablesOf_RequestParameters_AcceptsOnlyAMap()
    {
        Assert.IsNotNull(GraphQlRequestEncoding.VariablesOf("{}"));
        Assert.IsNull(GraphQlRequestEncoding.VariablesOf("[1]"));
        Assert.IsNull(GraphQlRequestEncoding.VariablesOf("{ not json"));
    }
}
