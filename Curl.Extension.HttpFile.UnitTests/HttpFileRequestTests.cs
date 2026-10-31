namespace Curl.Extension.HttpFile;

/// <summary>Checks the curl arguments <see cref="HttpFileRequest" /> gives for each method, header and body.</summary>
[TestClass]
public sealed class HttpFileRequestTests
{
    [TestMethod]
    public void Arguments_Head_UsesHeadNotX() =>
        CollectionAssert.AreEqual(new[] { "--head", "http://h/" }, Request("HEAD").Arguments().ToArray());

    [TestMethod]
    public void Arguments_GetWithABody_NamesTheMethod() =>
        CollectionAssert.AreEqual(new[] { "-X", "GET", "http://h/", "--data-raw", "@x" }, Request("GET", body: "@x").Arguments().ToArray());

    [TestMethod]
    public void Arguments_PostWithoutABody_NamesTheMethod() =>
        CollectionAssert.AreEqual(new[] { "-X", "POST", "http://h/" }, Request("POST").Arguments().ToArray());

    [TestMethod]
    public void Arguments_PostWithABodyFile_SendsTheFileAsIs() =>
        CollectionAssert.AreEqual(new[] { "http://h/", "--data-binary", "@body.bin" }, Request("POST", bodyFile: "body.bin").Arguments().ToArray());

    [TestMethod]
    public void Arguments_HeaderWithAnEmptyValue_IsSentEmptyWithASemicolon() =>
        CollectionAssert.AreEqual(
            new[] { "-H", "X-Empty;", "-H", "X-Full: 1" },
            new HttpFileRequest("GET", "http://h/", ["X-Empty :  ", "X-Full: 1"], null, null).Arguments().Skip(1).ToArray());

    private static HttpFileRequest Request(string method, string? body = null, string? bodyFile = null) =>
        new(method, "http://h/", [], body, bodyFile);
}
