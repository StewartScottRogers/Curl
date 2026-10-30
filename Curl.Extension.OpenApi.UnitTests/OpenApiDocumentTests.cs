using System.Text;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>
/// Checks reading a description against OpenAPI 3.1 §4.8.1 (OpenAPI Object), §4.8.5 and §4.8.6 (Server and Server
/// Variable Objects), §4.8.9 (Path Item Object), §4.8.10 (Operation Object), §4.8.13 (Request Body Object) and §4.8.23
/// (Reference Object, with RFC 6901 JSON Pointer).
/// </summary>
[TestClass]
public sealed class OpenApiDocumentTests
{
    [TestMethod]
    public void Parse_ByteOrderMark_IsSkipped()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("""{"openapi":"3.0.3","paths":{"/":{"get":{"operationId":"a"}}}}""")];

        Assert.AreEqual("/", OpenApiDocument.Parse(bytes, "a.json").FindOperation("a").ServerUrl);
    }

    [TestMethod]
    [DataRow(" {\"openapi\":", "openapi: a.yaml is not a JSON OpenAPI description")]
    [DataRow("openapi: [3", "openapi: a.yaml is not a YAML OpenAPI description")]
    [DataRow("openapi: &v 3.1.0", "openapi: a.yaml uses YAML anchors, aliases or tags, which --openapi does not read")]
    [DataRow("openapi: 2.0", "openapi: a.yaml is not an OpenAPI 3 description")]
    [DataRow("""{"swagger":"2.0"}""", "openapi: a.yaml is not an OpenAPI 3 description")]
    [DataRow("""{"openapi":"2.0"}""", "openapi: a.yaml is not an OpenAPI 3 description")]
    [DataRow("""{"openapi":3.1}""", "openapi: a.yaml is not an OpenAPI 3 description")]
    public void Parse_NotAnOpenApi3JsonDescription_FailsWithExit2(string text, string message)
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => OpenApiDocument.Parse(Encoding.UTF8.GetBytes(text), "a.yaml"));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public void Parse_YamlDescription_GivesTheSameArgumentsAsItsJsonTwin()
    {
        const string Json = """{"openapi":"3.1.0","servers":[{"url":"http://h/{v}","variables":{"v":{"default":"v1"}}}],"paths":{"/items/{id}":{"parameters":[{"name":"id","in":"path","required":true}],"post":{"operationId":"add","parameters":[{"name":"tags","in":"query","schema":{"type":"array"}}],"requestBody":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Item"}}}}}}},"components":{"schemas":{"Item":{"properties":{"count":{"type":"integer"},"name":{"type":"string"}}}}}}""";
        const string Yaml = """
            openapi: 3.1.0
            servers:
            - url: 'http://h/{v}'
              variables:
                v: {default: v1}
            paths:
              /items/{id}:
                parameters:
                  - name: id
                    in: path
                    required: true
                post:
                  operationId: add
                  parameters: [{name: tags, in: query, schema: {type: array}}]
                  requestBody:
                    content:
                      application/json:
                        schema:
                          $ref: "#/components/schemas/Item"
            components:
              schemas:
                Item:
                  properties:
                    count: {type: integer}
                    name:
                      type: string
            """;
        static OrderedDictionary<string, List<string>> Values() => new() { ["id"] = ["3"], ["tags"] = ["a", "b"], ["count"] = ["2"], ["name"] = ["x"] };

        IReadOnlyList<string> fromJson = OpenApiRequest.Arguments(OpenApiDocument.Parse(Encoding.UTF8.GetBytes(Json), "a.json").FindOperation("add"), null, Values());
        IReadOnlyList<string> fromYaml = OpenApiRequest.Arguments(OpenApiDocument.Parse(Encoding.UTF8.GetBytes(Yaml), "a.yaml").FindOperation("add"), null, Values());

        Assert.IsNotEmpty(fromJson);
        CollectionAssert.AreEqual(fromJson.ToArray(), fromYaml.ToArray());
    }

    [TestMethod]
    public void FindOperation_UnknownId_FailsWithExit2() =>
        Assert.AreEqual(
            "openapi: no operation nope in the description",
            Assert.ThrowsExactly<ArgumentExpansionException>(() => Document("""{"paths":{"/":{"get":5,"summary":"x"}}}""").FindOperation("nope")).Message);

    [TestMethod]
    public void FindOperation_WithoutPaths_FailsWithExit2() =>
        Assert.ThrowsExactly<ArgumentExpansionException>(() => Document("""{"info":{}}""").FindOperation("a"));

    [TestMethod]
    public void FindOperation_PathItemReference_IsFollowed_AndTheMethodIsItsMember()
    {
        OpenApiOperation operation = Document("""{"paths":{"/a":{"$ref":"#/components/pathItems/A"}},"components":{"pathItems":{"A":{"patch":{"operationId":"p"}}}}}""").FindOperation("p");

        Assert.AreEqual("patch", operation.Method);
        Assert.AreEqual("/a", operation.Path);
    }

    [TestMethod]
    public void FindOperation_Servers_OperationsWinOverThePathItemsAndThoseOverTheRoots()
    {
        OpenApiDocument document = Document("""
            {"servers":[{"url":"http://root"}],"paths":{
              "/a":{"servers":[{"url":"http://item"}],"get":{"operationId":"a","servers":[{"url":"http://op/{v}","variables":{"v":{"default":"1"},"w":{}}}]},"put":{"operationId":"b"}},
              "/c":{"servers":[],"get":{"operationId":"c","servers":["http://not-an-object"]}}}}
            """);

        Assert.AreEqual("http://op/1", document.FindOperation("a").ServerUrl);
        Assert.AreEqual("http://item", document.FindOperation("b").ServerUrl);
        Assert.AreEqual("http://root", document.FindOperation("c").ServerUrl);
    }

    [TestMethod]
    public void FindOperation_ServerWithoutUrl_IsTheDefaultSlash() =>
        Assert.AreEqual("/", Document("""{"servers":[{"description":"x"}],"paths":{"/":{"get":{"operationId":"a"}}}}""").FindOperation("a").ServerUrl);

    [TestMethod]
    public void FindOperation_Parameters_TheOperationsOverrideThePathItemsOfTheSameNameAndIn_AndIgnoredOnesAreLeftOut()
    {
        OpenApiOperation operation = Document("""
            {"paths":{"/{id}":{
              "parameters":[{"name":"id","in":"path"},{"name":"id","in":"query"},{"name":"Accept","in":"header"}],
              "get":{"operationId":"a","parameters":[{"name":"id","in":"path","style":"label"}]}}}}
            """).FindOperation("a");

        CollectionAssert.AreEqual(new[] { "path label", "query form" }, operation.Parameters.Select(parameter => $"{parameter.In} {parameter.Style}").ToArray());
    }

    [TestMethod]
    public void FindOperation_RequestBody_PrefersASupportedMediaType_AndReadsPropertyTypes()
    {
        OpenApiRequestBody? body = Document("""
            {"paths":{"/":{"post":{"operationId":"a","requestBody":{"content":{
              "application/xml":{},
              "application/json":{"schema":{"properties":{
                "n":{"type":["integer","null"]},"m":{"type":["null"]},"k":{"type":[5]},"t":{"type":"array","items":{"$ref":"#/components/schemas/S"}},"u":{}}}}}}}}},
             "components":{"schemas":{"S":{"type":"boolean"}}}}
            """).FindOperation("a").RequestBody;

        Assert.IsNotNull(body);
        Assert.AreEqual("application/json", body.MediaType);
        Assert.AreEqual(new OpenApiPropertyType("integer", null), body.Properties["n"]);
        Assert.AreEqual(new OpenApiPropertyType(null, null), body.Properties["m"]);
        Assert.AreEqual(new OpenApiPropertyType(null, null), body.Properties["k"]);
        Assert.AreEqual(new OpenApiPropertyType("array", "boolean"), body.Properties["t"]);
        Assert.AreEqual(OpenApiPropertyType.Untyped, body.Properties["u"]);
    }

    [TestMethod]
    public void FindOperation_RequestBodyWithOnlyUnsupportedMediaTypes_KeepsTheFirst_WithNoSchema()
    {
        OpenApiRequestBody? body = Document("""{"paths":{"/":{"post":{"operationId":"a","requestBody":{"content":{"text/plain":{},"application/xml":{}}}}}}}""").FindOperation("a").RequestBody;

        Assert.AreEqual("text/plain", body?.MediaType);
        Assert.IsEmpty(body!.Properties);
    }

    [TestMethod]
    [DataRow("""{"paths":{"/":{"post":{"operationId":"a"}}}}""")]
    [DataRow("""{"paths":{"/":{"post":{"operationId":"a","requestBody":{"content":{}}}}}}""")]
    public void FindOperation_NoRequestBodyOrNoContent_TakesNoBody(string json) =>
        Assert.IsNull(Document(json).FindOperation("a").RequestBody);

    [TestMethod]
    public void Resolve_PointerIntoAnArrayAndEscapedNames_IsFollowed()
    {
        OpenApiOperation operation = Document("""
            {"x":{"a/b~c d":[{"name":"q","in":"query"}]},
             "paths":{"/":{"get":{"operationId":"a","parameters":[{"$ref":"#/x/a~1b~0c%20d/0"}]}}}}
            """).FindOperation("a");

        Assert.AreEqual("q", operation.Parameters.Single().Name);
    }

    [TestMethod]
    [DataRow("other.json#/x", "openapi: $ref other.json#/x is not in the description; only local references are read")]
    [DataRow("#/x/1", "openapi: $ref #/x/1 does not resolve")]
    [DataRow("#/x/first", "openapi: $ref #/x/first does not resolve")]
    [DataRow("#/missing", "openapi: $ref #/missing does not resolve")]
    [DataRow("#/paths/~1/get/operationId/z", "openapi: $ref #/paths/~1/get/operationId/z does not resolve")]
    [DataRow("#/loop", "openapi: a $ref chain is circular or too long")]
    public void Resolve_ReferenceThatCannotBeFollowed_FailsWithExit2(string reference, string message)
    {
        OpenApiDocument document = Document("""{"x":[{}],"loop":{"$ref":"#/loop"},"paths":{"/":{"get":{"operationId":"a","parameters":[{"$ref":"REF"}]}}}}""".Replace("REF", reference, StringComparison.Ordinal));

        Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(() => document.FindOperation("a")).Message);
    }

    private static OpenApiDocument Document(string json) =>
        OpenApiDocument.Parse(Encoding.UTF8.GetBytes(json.StartsWith("{\"openapi\"", StringComparison.Ordinal) ? json : "{\"openapi\":\"3.1.0\"," + json.TrimStart()[1..]), "a.json");
}
