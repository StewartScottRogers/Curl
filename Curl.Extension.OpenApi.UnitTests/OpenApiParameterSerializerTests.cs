using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>
/// Checks parameter serialization against OpenAPI 3.1 §4.8.12.4.1, Style Examples: the table's <c>color</c> rows,
/// with <c>blue</c> as a primitive and <c>blue,black,brown</c> as an array.
/// </summary>
[TestClass]
public sealed class OpenApiParameterSerializerTests
{
    private static readonly string[] Primitive = ["blue"];
    private static readonly string[] Array = ["blue", "black", "brown"];

    [TestMethod]
    [DataRow("path", "simple", false, "blue,black,brown")]
    [DataRow("path", "simple", true, "blue,black,brown")]
    [DataRow("path", "label", false, ".blue,black,brown")]
    [DataRow("path", "label", true, ".blue.black.brown")]
    [DataRow("path", "matrix", false, ";color=blue,black,brown")]
    [DataRow("path", "matrix", true, ";color=blue;color=black;color=brown")]
    [DataRow("query", "form", false, "color=blue,black,brown")]
    [DataRow("query", "form", true, "color=blue&color=black&color=brown")]
    [DataRow("query", "spaceDelimited", false, "color=blue%20black%20brown")]
    [DataRow("query", "spaceDelimited", true, "color=blue&color=black&color=brown")]
    [DataRow("query", "pipeDelimited", false, "color=blue|black|brown")]
    [DataRow("query", "pipeDelimited", true, "color=blue&color=black&color=brown")]
    [DataRow("header", "simple", false, "blue,black,brown")]
    [DataRow("cookie", "form", false, "color=blue,black,brown")]
    [DataRow("cookie", "form", true, "color=blue; color=black; color=brown")]
    public void Serialize_Array_MatchesTheStyleExamplesTable(string location, string style, bool explode, string expected) =>
        Assert.AreEqual(expected, OpenApiParameterSerializer.Serialize(new("color", location, style, explode, false, false), Array));

    [TestMethod]
    [DataRow("path", "simple", "blue")]
    [DataRow("path", "label", ".blue")]
    [DataRow("path", "matrix", ";color=blue")]
    [DataRow("query", "form", "color=blue")]
    public void Serialize_Primitive_MatchesTheStyleExamplesTable(string location, string style, string expected) =>
        Assert.AreEqual(expected, OpenApiParameterSerializer.Serialize(new("color", location, style, true, false, false), Primitive));

    [TestMethod]
    public void Serialize_PathAndQueryValues_ArePercentEncoded_AndAllowReservedKeepsReservedOnes()
    {
        Assert.AreEqual("a%2Fb%20c", OpenApiParameterSerializer.Serialize(new("p", "path", "simple", false, true, false), ["a/b c"]));
        Assert.AreEqual("q%20x=a%2Fb", OpenApiParameterSerializer.Serialize(new("q x", "query", "form", true, false, false), ["a/b"]));
        Assert.AreEqual("q=a/b%20c", OpenApiParameterSerializer.Serialize(new("q", "query", "form", true, false, true), ["a/b c"]));
        Assert.AreEqual("a/b c", OpenApiParameterSerializer.Serialize(new("H", "header", "simple", false, false, false), ["a/b c"]));
    }

    [TestMethod]
    [DataRow("path", "form")]
    [DataRow("query", "deepObject")]
    [DataRow("header", "form")]
    [DataRow("cookie", "simple")]
    [DataRow("body", "simple")]
    public void Serialize_StyleNotAllowedInItsLocation_FailsWithExit2(string location, string style)
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => OpenApiParameterSerializer.Serialize(new("color", location, style, false, false, false), Primitive));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual($"openapi: parameter color in {location} has style {style}, which is not supported there", failure.Message);
    }
}
