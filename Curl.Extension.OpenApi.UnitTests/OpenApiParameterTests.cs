using System.Text.Json;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>Checks the Parameter Object's fields and defaults against OpenAPI 3.1 §4.8.12.1 and §4.8.12.3.</summary>
[TestClass]
public sealed class OpenApiParameterTests
{
    [TestMethod]
    [DataRow("""{"name":"a","in":"query"}""", "form", true, false)]
    [DataRow("""{"name":"a","in":"cookie"}""", "form", true, false)]
    [DataRow("""{"name":"a","in":"path"}""", "simple", false, true)]
    [DataRow("""{"name":"a","in":"header"}""", "simple", false, false)]
    [DataRow("""{"name":"a","in":"query","style":"pipeDelimited","explode":true,"required":true}""", "pipeDelimited", true, true)]
    [DataRow("""{"name":"a","in":"path","explode":"yes","required":false}""", "simple", false, false)]
    public void From_FieldsOrTheirDefaults(string json, string style, bool explode, bool required)
    {
        OpenApiParameter parameter = OpenApiParameter.From(Parse(json));

        Assert.AreEqual(style, parameter.Style);
        Assert.AreEqual(explode, parameter.Explode);
        Assert.AreEqual(required, parameter.Required);
        Assert.IsFalse(parameter.AllowReserved);
    }

    [TestMethod]
    public void From_AllowReserved_IsRead() =>
        Assert.IsTrue(OpenApiParameter.From(Parse("""{"name":"a","in":"query","allowReserved":true}""")).AllowReserved);

    [TestMethod]
    [DataRow("""{"in":"query"}""", "openapi: a parameter has no name")]
    [DataRow("""{"name":"a","in":5}""", "openapi: parameter a has no in")]
    [DataRow("[]", "openapi: a parameter has no name")]
    public void From_WithoutNameOrIn_FailsWithExit2(string json, string message) =>
        Assert.AreEqual(message, Assert.ThrowsExactly<ArgumentExpansionException>(() => OpenApiParameter.From(Parse(json))).Message);

    [TestMethod]
    [DataRow("header", "accept", true)]
    [DataRow("header", "Content-Type", true)]
    [DataRow("header", "Authorization", true)]
    [DataRow("header", "X-Trace", false)]
    [DataRow("query", "Accept", false)]
    public void IsIgnored_HeaderNamedAcceptContentTypeOrAuthorization(string location, string name, bool ignored) =>
        Assert.AreEqual(ignored, new OpenApiParameter(name, location, "simple", false, false, false).IsIgnored);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
