using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>Checks the request built for an operation against OpenAPI 3.1 §4.8.8.1 (Patterned Fields: the path appended to the server URL), §4.8.12 (Parameter Object) and §4.8.13 (Request Body Object).</summary>
[TestClass]
public sealed class OpenApiRequestTests
{
    private static readonly OpenApiParameter Id = new("id", "path", "simple", false, true, false);
    private static readonly OpenApiParameter Page = new("page", "query", "form", true, false, false);
    private static readonly OpenApiRequestBody JsonBody = new("application/json", new Dictionary<string, OpenApiPropertyType>());

    [TestMethod]
    public void Arguments_ServerUrlWithATrailingSlash_IsJoinedToThePathWithOne() =>
        CollectionAssert.AreEqual(new[] { "http://h/v1/items/3" }, Arguments(Operation("get", "http://h/v1/", Id), null, ("id", "3")).ToArray());

    [TestMethod]
    public void Arguments_OptionalParameterLeftOut_IsNotSent_AndAQueryIsJoinedWithAmpersands()
    {
        OpenApiParameter sort = new("sort", "query", "form", true, false, false);

        CollectionAssert.AreEqual(new[] { "http://h/items/3" }, Arguments(Operation("get", "http://h", Id, Page), null, ("id", "3")).ToArray());
        CollectionAssert.AreEqual(new[] { "http://h/items/3?page=2&sort=a" }, Arguments(Operation("get", "http://h", Id, Page, sort), null, ("id", "3"), ("sort", "a"), ("page", "2")).ToArray());
    }

    [TestMethod]
    public void Arguments_RequiredParameterLeftOut_FailsWithExit2()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => Arguments(Operation("get", "http://h", Id), null));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("openapi: operation op needs parameter id", failure.Message);
    }

    [TestMethod]
    public void Arguments_UrlWithGlobCharacters_TurnsGlobbingOff()
    {
        OpenApiParameter filter = new("f[a]", "query", "form", true, false, true);

        CollectionAssert.AreEqual(new[] { "http://h/items/1?f[a]=%7Bx%7D", "--globoff" }, Arguments(Operation("get", "http://h", Id, filter), null, ("id", "1"), ("f[a]", "{x}")).ToArray());
    }

    [TestMethod]
    public void Arguments_RelativeServerUrl_FailsAskingForOpenApiServer() =>
        Assert.AreEqual(
            "openapi: server URL /api is relative; give --openapi-server <url>",
            Assert.ThrowsExactly<ArgumentExpansionException>(() => Arguments(Operation("get", "/api", Id), null, ("id", "1"))).Message);

    [TestMethod]
    public void Arguments_RelativeServerUrlWithOpenApiServer_UsesIt() =>
        CollectionAssert.AreEqual(new[] { "https://x/items/1" }, Arguments(Operation("get", "/api", Id), "https://x", ("id", "1")).ToArray());

    [TestMethod]
    public void Arguments_ValueForNoParameterWithoutARequestBody_FailsWithExit2() =>
        Assert.AreEqual(
            "openapi: operation op has no parameter or request body for colour",
            Assert.ThrowsExactly<ArgumentExpansionException>(() => Arguments(Operation("get", "http://h", Id), null, ("id", "1"), ("colour", "red"))).Message);

    [TestMethod]
    [DataRow("get", false, new[] { "http://h/items/1" })]
    [DataRow("get", true, new[] { "-X", "GET", "http://h/items/1", "-H", "Content-Type: application/json", "--data-binary", """{"a":"b"}""" })]
    [DataRow("post", true, new[] { "http://h/items/1", "-H", "Content-Type: application/json", "--data-binary", """{"a":"b"}""" })]
    [DataRow("post", false, new[] { "-X", "POST", "http://h/items/1" })]
    [DataRow("put", true, new[] { "-X", "PUT", "http://h/items/1", "-H", "Content-Type: application/json", "--data-binary", """{"a":"b"}""" })]
    [DataRow("head", false, new[] { "-I", "http://h/items/1" })]
    public void Arguments_Method_IsLeftToCurlOnlyWhereItsDefaultMatches(string method, bool withBody, string[] expected)
    {
        OpenApiOperation operation = Operation(method, "http://h", Id) with { RequestBody = JsonBody };

        IReadOnlyList<string> arguments = withBody ? Arguments(operation, null, ("id", "1"), ("a", "b")) : Arguments(operation, null, ("id", "1"));

        CollectionAssert.AreEqual(expected, arguments.ToArray());
    }

    [TestMethod]
    public void Arguments_SeveralCookies_AreJoinedIntoOneCookieArgument()
    {
        OpenApiParameter a = new("a", "cookie", "form", true, false, false);
        OpenApiParameter b = new("b", "cookie", "form", true, false, false);

        CollectionAssert.AreEqual(new[] { "http://h/items/1", "-b", "a=1; b=2" }, Arguments(Operation("get", "http://h", Id, a, b), null, ("id", "1"), ("a", "1"), ("b", "2")).ToArray());
    }

    private static OpenApiOperation Operation(string method, string serverUrl, params OpenApiParameter[] parameters) =>
        new("op", method, "/items/{id}", serverUrl, parameters, null);

    private static IReadOnlyList<string> Arguments(OpenApiOperation operation, string? serverUrl, params (string Name, string Value)[] values)
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

        return OpenApiRequest.Arguments(operation, serverUrl, named);
    }
}
