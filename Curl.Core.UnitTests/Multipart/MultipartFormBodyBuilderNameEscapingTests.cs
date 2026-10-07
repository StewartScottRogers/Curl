using System.Text;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the <c>Content-Disposition</c> names and file names <see cref="MultipartFormBodyBuilder" />
/// writes under each <see cref="MultipartNameEscaping" />, against curl 8.21.0
/// (<c>/mingw64/bin/curl</c>, Schannel) measured on 2026-09-29 with <c>Record-CurlExchange.ps1</c>,
/// with and without <c>--form-escape</c>; every recorded line is in BL-625's Notes.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderNameEscapingTests
{
    private const string Boundary = "------------------------0000000000000000000000";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("a\"b", MultipartNameEscaping.Percent, "a%22b")]
    [DataRow("a\"b", MultipartNameEscaping.Backslash, "a\\\"b")]
    [DataRow("a\\b", MultipartNameEscaping.Percent, "a\\b")]
    [DataRow("a\\b", MultipartNameEscaping.Backslash, "a\\\\b")]
    [DataRow("c\rd\ne", MultipartNameEscaping.Percent, "c%0Dd%0Ae")]
    [DataRow("c\rd\ne", MultipartNameEscaping.Backslash, "c\rd\ne")]
    public async Task TextPartNameIsEscapedAsCurlEscapesIt(string name, MultipartNameEscaping nameEscaping, string sentName)
    {
        // -F '<name>=1', with and without --form-escape
        MultipartFormPart part = new(name, MultipartFormPartKind.Text, "1", null, null, NoHeaders, NoParts);

        string body = await BuildAsync(nameEscaping, part);

        string expectedBody = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"{sentName}\"\r\n\r\n1\r\n--{Boundary}--\r\n";
        TestDiagnostics.For(TestContext).Diff("body", expectedBody, body);
        TestDiagnostics.For(TestContext).Assert("body", expectedBody, body);
        Assert.AreEqual(expectedBody, body);
    }

    [TestMethod]
    [DataRow("q\"x.txt", MultipartNameEscaping.Percent, "q%22x.txt")]
    [DataRow("q\"x.txt", MultipartNameEscaping.Backslash, "q\\\"x.txt")]
    [DataRow("q\\x.txt", MultipartNameEscaping.Percent, "q\\x.txt")]
    [DataRow("q\\x.txt", MultipartNameEscaping.Backslash, "q\\\\x.txt")]
    public async Task FileNameIsEscapedAsCurlEscapesIt(string fileName, MultipartNameEscaping nameEscaping, string sentFileName)
    {
        // -F 'f=@dir/x.txt;filename="<fileName>"', with and without --form-escape
        MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, "dir/x.txt", null, fileName, NoHeaders, NoParts);

        string body = await BuildAsync(nameEscaping, part);

        string expected =
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"{sentFileName}\"\r\n"
            + $"Content-Type: text/plain\r\n\r\nX\r\n--{Boundary}--\r\n";
        TestDiagnostics.For(TestContext).Diff("body", expected, body);
        Assert.AreEqual(expected, body);
    }

    [TestMethod]
    public async Task FileNameTakenFromThePathIsEscapedWithBackslashes()
    {
        // -F 'f=@dir/q"x' --form-escape, where the file system allows the quote
        MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, "dir/q\"x", null, null, NoHeaders, NoParts);

        string body = await BuildAsync(MultipartNameEscaping.Backslash, part);

        string expected =
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"q\\\"x\"\r\n"
            + $"Content-Type: application/octet-stream\r\n\r\nQ\r\n--{Boundary}--\r\n";
        TestDiagnostics.For(TestContext).Diff("body", expected, body);
        Assert.AreEqual(expected, body);
    }

    [TestMethod]
    public async Task PartsNestedInAMultipartAreEscapedWithBackslashes()
    {
        // -F 'm=(;type=multipart/form-data' -F 'a"b=1' -F '=)' --form-escape
        MultipartFormPart inner = new("a\"b", MultipartFormPartKind.Text, "1", null, null, NoHeaders, NoParts);
        MultipartFormPart outer = new("m\\", MultipartFormPartKind.Multipart, string.Empty, "multipart/form-data", null, NoHeaders, [inner]);

        string body = await BuildAsync(MultipartNameEscaping.Backslash, outer);

        string expected =
            $"--{Boundary}\r\nContent-Disposition: form-data; name=\"m\\\\\"\r\n"
            + $"Content-Type: multipart/form-data; boundary={Boundary}\r\n\r\n"
            + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\\\"b\"\r\n\r\n1\r\n--{Boundary}--\r\n"
            + $"\r\n--{Boundary}--\r\n";
        TestDiagnostics.For(TestContext).Diff("body", expected, body);
        Assert.AreEqual(expected, body);
    }

    [TestMethod]
    public async Task BuildingWithoutAnEscapingEscapesWithPercents()
    {
        MultipartFormPart part = new("a\"b", MultipartFormPartKind.Text, "1", null, null, NoHeaders, NoParts);
        MultipartFormBodyBuilder builder = new(Files(), Encoding.UTF8, () => Boundary);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("part name", part.Name);
        diagnostics.Arrange("escaping", "none given");

        MultipartFormBuildResult result = await builder.BuildAsync([part], TestContext.CancellationToken);
        diagnostics.Act("isBuilt", result.IsBuilt);

        string expected = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a%22b\"\r\n\r\n1\r\n--{Boundary}--\r\n";
        string actual = await ReadAsync(result);
        diagnostics.Diff("body", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private static FormFileSystem Files() =>
        new FormFileSystem().WithFile("dir/x.txt", "X").WithFile("dir/q\"x", "Q");

    private async Task<string> BuildAsync(MultipartNameEscaping nameEscaping, MultipartFormPart part)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("part", $"{part.Name}:{part.Kind}:{part.FileName}");
        diagnostics.Arrange("escaping", nameEscaping);
        MultipartFormBodyBuilder builder = new(Files(), Encoding.UTF8, () => Boundary);
        MultipartFormBuildResult result = await builder.BuildAsync([part], nameEscaping, TestContext.CancellationToken);
        diagnostics.Act("isBuilt", result.IsBuilt);
        return await ReadAsync(result);
    }

    private async Task<string> ReadAsync(MultipartFormBuildResult result)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("isBuilt", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        using MemoryStream copy = new();
        await using (result.Body.Content)
        {
            await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
        }

        diagnostics.Bytes("body", copy.ToArray());
        diagnostics.Assert("length", copy.Length, result.Body.Length);
        Assert.AreEqual(copy.Length, result.Body.Length);
        return Encoding.Latin1.GetString(copy.ToArray());
    }
}
