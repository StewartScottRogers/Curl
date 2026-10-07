using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the <c>@-</c> and <c>&lt;-</c> parts <see cref="MultipartFormBodyBuilder" /> builds
/// against the bodies curl 8.21.0 (<c>/mingw64/bin/curl</c>, Schannel) sent on 2026-09-26 for
/// <c>printf 'hello\nworld' | curl -s -o /dev/null -H Expect: -F &lt;spec&gt; http://127.0.0.1:&lt;port&gt;/</c>,
/// each read off a loopback listener. Each test injects the boundary curl chose for that run, so
/// the body and its <c>Content-Length</c> are the recorded ones byte for byte.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderStandardInputTests
{
    private const string PipedInput = "hello\nworld";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UploadFromStandardInputIsNamedDashWithNoContentTypeMatchingCurl()
    {
        // -F a=@-
        const string B = "------------------------Q7DWhE4bjdusOhY9dHZ5ul";
        MultipartFormBuildResult result = await BuildAsync(PipedInput, B, StandardInput("a", MultipartFormPartKind.FileUpload));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"-\"\r\n\r\nhello\nworld\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 173, B);
    }

    [TestMethod]
    public async Task ContentFromStandardInputHasNoFileNameMatchingCurl()
    {
        // -F a=<-
        const string B = "------------------------qasapVUoQCpX0CWRoWZbsA";
        MultipartFormBuildResult result = await BuildAsync(PipedInput, B, StandardInput("a", MultipartFormPartKind.FileContent));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nhello\nworld\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 159, B);
    }

    [TestMethod]
    public async Task ASecondStandardInputPartGetsWhatTheFirstLeftMatchingCurl()
    {
        // -F a=@- -F b=<-
        const string B = "------------------------lTr4ja01goXNa4jZJSVZmx";
        MultipartFormBuildResult result = await BuildAsync(
            PipedInput,
            B,
            StandardInput("a", MultipartFormPartKind.FileUpload),
            StandardInput("b", MultipartFormPartKind.FileContent));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"-\"\r\n\r\nhello\nworld\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 269, B);
    }

    [TestMethod]
    public async Task AFileNameGivesStandardInputTheContentTypeOfItsExtensionMatchingCurl()
    {
        // -F "a=@-;filename=x.txt"
        const string B = "------------------------48n39e7Bme98KDxqQt9gUR";
        MultipartFormBuildResult result = await BuildAsync(
            PipedInput,
            B,
            StandardInput("a", MultipartFormPartKind.FileUpload) with { FileName = "x.txt" });

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"x.txt\"\r\nContent-Type: text/plain\r\n\r\nhello\nworld\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 203, B);
    }

    [TestMethod]
    public async Task AnEncoderEncodesStandardInputMatchingCurl()
    {
        // -F "a=@-;encoder=base64"
        const string B = "------------------------g0HLmaFnyF7KlZqUWD2Uqx";
        MultipartFormBuildResult result = await BuildAsync(
            PipedInput,
            B,
            StandardInput("a", MultipartFormPartKind.FileUpload) with { Encoder = "base64" });

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"-\"\r\nContent-Transfer-Encoding: base64\r\n\r\naGVsbG8Kd29ybGQ=\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 213, B);
    }

    [TestMethod]
    public async Task WithoutStandardInputADashPartOpensTheFileNamedDash()
    {
        const string B = "------------------------qasapVUoQCpX0CWRoWZbsA";
        FormFileSystem files = new FormFileSystem().WithFile(MultipartFormPart.StandardInputPath, "yy");
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, () => B);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("boundary", B);
        diagnostics.Arrange("file named dash", MultipartFormPart.StandardInputPath);

        MultipartFormBuildResult result = await builder.BuildAsync(
            [StandardInput("a", MultipartFormPartKind.FileContent)],
            TestContext.CancellationToken);
        diagnostics.Act("isBuilt", result.IsBuilt);

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nyy\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
        diagnostics.Assert("opened files", 1, files.Opened.Count);
        Assert.HasCount(1, files.Opened);
    }

    [TestMethod]
    public async Task StandardInputIsReadWithoutOpeningAFileAndIsLeftOpen()
    {
        FormFileSystem files = new();
        using FormFileSystem.TrackedStream input = new(Encoding.ASCII.GetBytes(PipedInput), refusesSeek: true, refusesRead: false);
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, MultipartBoundary.CreateRandom, input);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("standard input", PipedInput);

        MultipartFormBuildResult result = await builder.BuildAsync(
            [StandardInput("a", MultipartFormPartKind.FileUpload)],
            TestContext.CancellationToken);
        diagnostics.Act("isBuilt", result.IsBuilt);

        diagnostics.Assert("isBuilt", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        await result.Body.Content.DisposeAsync();
        diagnostics.Assert("opened files", 0, files.Opened.Count);
        Assert.IsEmpty(files.Opened);
        diagnostics.Assert("input disposed", false, input.IsDisposed);
        Assert.IsFalse(input.IsDisposed, "Standard input belongs to the caller.");
    }

    [TestMethod]
    public async Task StandardInputThatCannotBeReadFailsWithExit26()
    {
        using FormFileSystem.TrackedStream input = new([1], refusesSeek: true, refusesRead: true);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, MultipartBoundary.CreateRandom, input);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("standard input", "one byte, refuses reads");

        MultipartFormBuildResult result = await builder.BuildAsync(
            [StandardInput("a", MultipartFormPartKind.FileUpload)],
            TestContext.CancellationToken);
        diagnostics.Act("isBuilt", result.IsBuilt);

        diagnostics.Assert("isBuilt", false, result.IsBuilt);
        Assert.IsFalse(result.IsBuilt);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure.ExitCode);
        diagnostics.Assert("error message", MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
    }

    private static MultipartFormPart StandardInput(string name, MultipartFormPartKind kind) =>
        new(name, kind, MultipartFormPart.StandardInputPath, null, null, NoHeaders, NoParts);

    private async Task<MultipartFormBuildResult> BuildAsync(string input, string boundary, params MultipartFormPart[] parts)
    {
        using MemoryStream standardInput = new(Encoding.ASCII.GetBytes(input), writable: false);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("standard input", input);
        diagnostics.Arrange("boundary", boundary);
        diagnostics.Arrange("parts", string.Join(", ", parts.Select(part => $"{part.Name}:{part.Kind}:{part.FileName}:{part.Encoder}")));
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, () => boundary, standardInput);
        MultipartFormBuildResult built = await builder.BuildAsync(parts, TestContext.CancellationToken);
        diagnostics.Act("isBuilt", built.IsBuilt);
        return built;
    }

    private async Task AssertBodyAsync(MultipartFormBuildResult result, string expected, long expectedLength, string boundary)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("isBuilt", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("content type", $"multipart/form-data; boundary={boundary}", result.Body.ContentType);
        Assert.AreEqual($"multipart/form-data; boundary={boundary}", result.Body.ContentType);
        diagnostics.Assert("length", expectedLength, result.Body.Length);
        Assert.AreEqual(expectedLength, result.Body.Length);
        using MemoryStream copy = new();
        await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
        await result.Body.Content.DisposeAsync();
        diagnostics.Bytes("body", copy.ToArray());
        diagnostics.Diff("body", expected, Encoding.Latin1.GetString(copy.ToArray()));
        Assert.AreEqual(expected, Encoding.Latin1.GetString(copy.ToArray()));
    }
}
