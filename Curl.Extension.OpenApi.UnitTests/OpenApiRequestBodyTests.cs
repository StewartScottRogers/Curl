using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>Checks the request body built from <c>name=value</c> arguments against OpenAPI 3.1 §4.8.13 and §4.8.14 (Request Body and Media Type Objects).</summary>
[TestClass]
public sealed class OpenApiRequestBodyTests
{
    private static readonly Dictionary<string, OpenApiPropertyType> Types = new(StringComparer.Ordinal)
    {
        ["count"] = new("integer", null),
        ["price"] = new("number", null),
        ["done"] = new("boolean", null),
        ["gone"] = new("null", null),
        ["meta"] = new("object", null),
        ["ids"] = new("array", "integer"),
        ["name"] = new("string", null),
    };

    [TestMethod]
    [DataRow("application/json", true)]
    [DataRow("application/merge-patch+json", true)]
    [DataRow("application/x-www-form-urlencoded", true)]
    [DataRow("multipart/form-data", true)]
    [DataRow("application/xml", false)]
    public void IsSupported_JsonFormAndMultipart(string mediaType, bool supported) =>
        Assert.AreEqual(supported, OpenApiRequestBody.IsSupported(mediaType));

    [TestMethod]
    public void Arguments_NoValues_SendsNoBody() =>
        Assert.IsEmpty(new OpenApiRequestBody("application/xml", Types).Arguments(Values()));

    [TestMethod]
    public void Arguments_Json_WritesEachValueAsItsSchemaType()
    {
        IReadOnlyList<string> arguments = new OpenApiRequestBody("application/json", Types).Arguments(
            Values(("count", "3"), ("price", "1.50"), ("done", "true"), ("gone", "null"), ("meta", """{"a":1}"""), ("ids", "1"), ("ids", "x"), ("name", "5"), ("other", "é")));

        CollectionAssert.AreEqual(
            new[] { "-H", "Content-Type: application/json", "--data-binary", """{"count":3,"price":1.50,"done":true,"gone":null,"meta":{"a":1},"ids":[1,"x"],"name":"5","other":"é"}""" },
            arguments.ToArray());
    }

    [TestMethod]
    public void Arguments_JsonValueNotOfItsType_IsAString()
    {
        IReadOnlyList<string> arguments = new OpenApiRequestBody("application/vnd.x+json", Types).Arguments(Values(("count", "three"), ("done", "1"), ("untyped", "a"), ("untyped", "b")));

        CollectionAssert.AreEqual(
            new[] { "-H", "Content-Type: application/vnd.x+json", "--data-binary", """{"count":"three","done":"1","untyped":["a","b"]}""" },
            arguments.ToArray());
    }

    [TestMethod]
    public void Arguments_Form_UrlencodesEachValue() =>
        CollectionAssert.AreEqual(
            new[] { "--data-urlencode", "a=1 2", "--data-urlencode", "a=3", "--data-urlencode", "b=@x" },
            new OpenApiRequestBody("application/x-www-form-urlencoded", Types).Arguments(Values(("a", "1 2"), ("a", "3"), ("b", "@x"))).ToArray());

    [TestMethod]
    public void Arguments_Multipart_SendsEachValueAsAFormString() =>
        CollectionAssert.AreEqual(
            new[] { "--form-string", "a=<x" },
            new OpenApiRequestBody("multipart/form-data", Types).Arguments(Values(("a", "<x"))).ToArray());

    [TestMethod]
    public void Arguments_UnsupportedMediaType_FailsWithExit2() =>
        Assert.AreEqual(
            "openapi: request body media type application/xml is not supported",
            Assert.ThrowsExactly<ArgumentExpansionException>(() => new OpenApiRequestBody("application/xml", Types).Arguments(Values(("a", "1")))).Message);

    private static OrderedDictionary<string, List<string>> Values(params (string Name, string Value)[] values)
    {
        OrderedDictionary<string, List<string>> named = new(StringComparer.Ordinal);
        foreach ((string name, string value) in values)
        {
            if (!named.TryGetValue(name, out List<string>? list))
            {
                list = [];
                named.Add(name, list);
            }

            list.Add(value);
        }

        return named;
    }
}
