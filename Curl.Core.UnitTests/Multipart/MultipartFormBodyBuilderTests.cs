using System.Text;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the bodies <see cref="MultipartFormBodyBuilder" /> builds against the ones curl 8.21.0
/// (<c>/mingw64/bin/curl</c>, Schannel) sent on 2026-09-26, each recorded as
/// <c>Record-CurlExchange.ps1 -Port 18205 -CurlArgs --no-progress-meter,-F,&lt;spec&gt;,...,http://127.0.0.1:18205/</c>
/// in a folder holding <c>f.py</c> (<c>hi</c> LF), <c>g</c> (<c>yy</c>), <c>h.png</c> (bytes 1, 2)
/// and <c>t.txt</c> (<c>tt</c>). Each test injects the boundaries curl chose for that run, so
/// the body and its <c>Content-Length</c> are the recorded ones byte for byte.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderTests
{
    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TextAndFileUploadMatchCurl()
    {
        // -F a=b -F f=@f.py
        const string B = "------------------------sZzYYdqX3BOdVrrbjrqV22";
        MultipartFormBuildResult result = await BuildAsync(
            [B],
            Text("a", "b"),
            File("f", MultipartFormPartKind.FileUpload, "f.py"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nb\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"f.py\"\r\nContent-Type: application/octet-stream\r\n\r\nhi\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 305, B);
    }

    [TestMethod]
    public async Task FileKindsTypesFileNamesHeadersAndUnnamedPartsMatchCurl()
    {
        // -F a=@g -F b=@h.png -F c=<t.txt -F d=<f.py -F "e=@t.txt;type=x/y;filename=q" -F "h=v;headers=X-A: 1" -F =anon
        const string B = "------------------------qEeWrg8C9gvOxAqyHTI0VB";
        MultipartFormBuildResult result = await BuildAsync(
            [B],
            File("a", MultipartFormPartKind.FileUpload, "g"),
            File("b", MultipartFormPartKind.FileUpload, "h.png"),
            File("c", MultipartFormPartKind.FileContent, "t.txt"),
            File("d", MultipartFormPartKind.FileContent, "f.py"),
            new MultipartFormPart("e", MultipartFormPartKind.FileUpload, "t.txt", "x/y", "q", NoHeaders, NoParts),
            new MultipartFormPart("h", MultipartFormPartKind.Text, "v", null, null, ["X-A: 1"], NoParts),
            Text(null, "anon"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"g\"\r\nContent-Type: application/octet-stream\r\n\r\nyy\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"b\"; filename=\"h.png\"\r\nContent-Type: image/png\r\n\r\n\u0001\u0002\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"c\"\r\n\r\ntt\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"d\"\r\n\r\nhi\n\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"e\"; filename=\"q\"\r\nContent-Type: x/y\r\n\r\ntt\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"h\"\r\nX-A: 1\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: form-data\r\n\r\nanon\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 868, B);
    }

    [TestMethod]
    public async Task FileGroupIsAMultipartMixedOfAttachmentsMatchingCurl()
    {
        // -F a=@t.txt,g
        const string B = "------------------------JU0Jib3SlHa9KICZXI6rCC";
        const string Inner = "------------------------AM3LucNQsiHOLzTtB9owOs";
        MultipartFormBuildResult result = await BuildAsync(
            [B, Inner],
            Multipart(
                "a",
                null,
                File(null, MultipartFormPartKind.FileUpload, "t.txt"),
                File(null, MultipartFormPartKind.FileUpload, "g")));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Disposition: attachment; filename=\"t.txt\"\r\nContent-Type: text/plain\r\n\r\ntt\r\n"
            + $"--{Inner}\r\nContent-Disposition: attachment; filename=\"g\"\r\nContent-Type: application/octet-stream\r\n\r\nyy\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 564, B);
    }

    [TestMethod]
    public async Task UpstreamTest1315ThreeFilesOneTypedMatchCurl()
    {
        // Measured 2026-10-08 (BL-1847), log/test1315.txt holding foo LF:
        // -F name=value -F "file=@log/test1315.txt,log/test1315.txt;type=magic/content,log/test1315.txt"
        const string B = "------------------------t0mvyLsCSTbstGTvmA2Z1l";
        const string Inner = "------------------------DYrKNSMbnJRxIkC7I1djEf";
        const string Path = "log/test1315.txt";
        FormFileSystem files = new FormFileSystem().WithFile(Path, "foo\n");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            [B, Inner],
            Text("name", "value"),
            Multipart(
                "file",
                null,
                File(null, MultipartFormPartKind.FileUpload, Path),
                new MultipartFormPart(null, MultipartFormPartKind.FileUpload, Path, "magic/content", null, NoHeaders, NoParts),
                File(null, MultipartFormPartKind.FileUpload, Path)));

        const string Attachment = "Content-Disposition: attachment; filename=\"test1315.txt\"\r\n";
        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"name\"\r\n\r\nvalue\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"file\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\n{Attachment}Content-Type: text/plain\r\n\r\nfoo\n\r\n"
            + $"--{Inner}\r\n{Attachment}Content-Type: magic/content\r\n\r\nfoo\n\r\n"
            + $"--{Inner}\r\n{Attachment}Content-Type: text/plain\r\n\r\nfoo\n\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 824, B);
    }

    [TestMethod]
    public async Task NestedMultipartWithATypeMatchesCurl()
    {
        // -F "m=(;type=multipart/alternative" -F x=1 -F y=@t.txt -F "=)" -F z=2
        const string B = "------------------------0VXvuUNJy72NddjnbOb7Sv";
        const string Inner = "------------------------SvglzaJjtvvMravxBBVzvW";
        MultipartFormBuildResult result = await BuildAsync(
            [B, Inner],
            Multipart("m", "multipart/alternative", Text("x", "1"), File("y", MultipartFormPartKind.FileUpload, "t.txt")),
            Text("z", "2"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"m\"\r\nContent-Type: multipart/alternative; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Disposition: attachment; name=\"x\"\r\n\r\n1\r\n"
            + $"--{Inner}\r\nContent-Disposition: attachment; name=\"y\"; filename=\"t.txt\"\r\nContent-Type: text/plain\r\n\r\ntt\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"z\"\r\n\r\n2\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 632, B);
    }

    [TestMethod]
    public async Task NestedMultipartWithoutATypeIsMultipartMixedMatchingCurl()
    {
        // -F "m=(" -F x=1 -F "=)"
        const string B = "------------------------hMVaYynQwkwHKXXCRYSbeQ";
        const string Inner = "------------------------OTCHragScI5Z9y9Bvcn29J";
        MultipartFormBuildResult result = await BuildAsync([B, Inner], Multipart("m", null, Text("x", "1")));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"m\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Disposition: attachment; name=\"x\"\r\n\r\n1\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 386, B);
    }

    [TestMethod]
    public async Task NestedMultipartFormDataGivesItsPartsFormDataMatchingCurl()
    {
        // -F "m=(;type=multipart/form-data" -F x=1 -F "=)"
        const string B = "------------------------tBKqgDhFQt1M1d9OACeqcC";
        const string Inner = "------------------------J8kzscwf37ICKlWiQIV0dN";
        MultipartFormBuildResult result = await BuildAsync([B, Inner], Multipart("m", "multipart/form-data", Text("x", "1")));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"m\"\r\nContent-Type: multipart/form-data; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n1\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 389, B);
    }

    [TestMethod]
    public async Task NamesAndFileNamesAreEscapedAndExplicitTypesKeptMatchingCurl()
    {
        // -F 'a"b\c=v' -F 'f=@t.txt;filename="q\"r"' -F 'c=<t.txt;filename=zz' -F 't=v;type=text/html'
        // The parser drops the filename of a '<' part with a warning, so c arrives without one.
        const string B = "------------------------GZoplGwPFadjIwak0WxyZW";
        MultipartFormBuildResult result = await BuildAsync(
            [B],
            Text("a\"b\\c", "v"),
            new MultipartFormPart("f", MultipartFormPartKind.FileUpload, "t.txt", null, "q\"r", NoHeaders, NoParts),
            File("c", MultipartFormPartKind.FileContent, "t.txt"),
            new MultipartFormPart("t", MultipartFormPartKind.Text, "v", "text/html", null, NoHeaders, NoParts));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a%22b\\c\"\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"q%22r\"\r\nContent-Type: text/plain\r\n\r\ntt\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"c\"\r\n\r\ntt\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"t\"\r\nContent-Type: text/html\r\n\r\nv\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 517, B);
    }

    [TestMethod]
    public async Task OwnContentTypeAndDispositionHeadersAndTextFileNamesMatchCurl()
    {
        // -F "a=v;headers=content-type: x/z;headers=X-B: 2" -F "b=v;headers=Content-Disposition: inline"
        // -F "c=<h.png" -F "d=v;filename=x.txt" -F "e=v;filename=x.py"
        const string B = "------------------------9wAleIb6vrPnGmVaCZt2et";
        MultipartFormBuildResult result = await BuildAsync(
            [B],
            new MultipartFormPart("a", MultipartFormPartKind.Text, "v", null, null, ["content-type: x/z", "X-B: 2"], NoParts),
            new MultipartFormPart("b", MultipartFormPartKind.Text, "v", null, null, ["Content-Disposition: inline"], NoParts),
            File("c", MultipartFormPartKind.FileContent, "h.png"),
            new MultipartFormPart("d", MultipartFormPartKind.Text, "v", null, "x.txt", NoHeaders, NoParts),
            new MultipartFormPart("e", MultipartFormPartKind.Text, "v", null, "x.py", NoHeaders, NoParts));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: x/z\r\nX-B: 2\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: inline\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"c\"\r\nContent-Type: image/png\r\n\r\n\u0001\u0002\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"d\"; filename=\"x.txt\"\r\nContent-Type: text/plain\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"e\"; filename=\"x.py\"\r\n\r\nv\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 638, B);
    }

    [TestMethod]
    public async Task FileUploadNameIsTheLastPathComponentAfterEitherSlash()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithFile(@"C:\x/y\t.txt", "tt").WithFile("d/u.txt", "u");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            [B],
            File("a", MultipartFormPartKind.FileUpload, @"C:\x/y\t.txt"),
            File("b", MultipartFormPartKind.FileUpload, "d/u.txt"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"; filename=\"t.txt\"\r\nContent-Type: text/plain\r\n\r\ntt\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"b\"; filename=\"u.txt\"\r\nContent-Type: text/plain\r\n\r\nu\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
    }

    [TestMethod]
    public async Task NameAndFileNameLineBreaksAreEscaped()
    {
        const string B = "------------------------0000000000000000000000";
        MultipartFormBuildResult result = await BuildAsync(
            [B],
            new MultipartFormPart("a\r\nb", MultipartFormPartKind.Text, "v", null, "c\nd", NoHeaders, NoParts));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a%0D%0Ab\"; filename=\"c%0Ad\"\r\n\r\nv\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
    }

    [TestMethod]
    public async Task UnnamedNestedPartsWithoutAFileNameCarryNoDisposition()
    {
        const string B = "------------------------0000000000000000000000";
        const string Inner = "------------------------1111111111111111111111";
        const string Innermost = "------------------------2222222222222222222222";
        MultipartFormBuildResult result = await BuildAsync(
            [B, Inner, Innermost],
            Multipart(null, null, new MultipartFormPart(null, MultipartFormPartKind.Text, "v", "x/y", null, NoHeaders, NoParts), Multipart(null, null)));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Type: x/y\r\n\r\nv\r\n"
            + $"--{Inner}\r\nContent-Type: multipart/mixed; boundary={Innermost}\r\n\r\n--{Innermost}--\r\n\r\n"
            + $"--{Inner}--\r\n\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
    }

    [TestMethod]
    public async Task ContentTypeWithParametersIsSentAsGivenAndTextPlainWithParametersIsKept()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithFile("t.TXT", "TT");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            [B],
            new MultipartFormPart("a", MultipartFormPartKind.Text, "v", "text/plain; charset=utf-8", null, NoHeaders, NoParts),
            File("b", MultipartFormPartKind.FileContent, "t.TXT"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nv\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\nTT\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
    }

    [TestMethod]
    public async Task OnlyAMultipartFormDataTypeGivesNestedPartsFormData()
    {
        const string B = "------------------------0000000000000000000000";
        const string Inner = "------------------------1111111111111111111111";
        const string Other = "------------------------2222222222222222222222";
        MultipartFormBuildResult result = await BuildAsync(
            [B, Inner, Other],
            Multipart("m", "Multipart/Form-Data; charset=x", Text("x", "1")),
            Multipart("n", "multipart/form-datax", Text("y", "2")));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"m\"\r\nContent-Type: Multipart/Form-Data; charset=x; boundary={Inner}\r\n\r\n"
            + $"--{Inner}\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n1\r\n--{Inner}--\r\n"
            + $"\r\n--{B}\r\nContent-Disposition: form-data; name=\"n\"\r\nContent-Type: multipart/form-datax; boundary={Other}\r\n\r\n"
            + $"--{Other}\r\nContent-Disposition: attachment; name=\"y\"\r\n\r\n2\r\n--{Other}--\r\n"
            + $"\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, B);
    }

    [TestMethod]
    public async Task AnEmptyFormIsTheClosingDelimiterAlone()
    {
        const string B = "------------------------0000000000000000000000";
        MultipartFormBuildResult result = await BuildAsync([B]);

        await AssertBodyAsync(result, $"--{B}--\r\n", 52, B);
    }

    [TestMethod]
    public async Task TextIsSentInTheGivenEncodingAndCountedInBytes()
    {
        const string B = "------------------------0000000000000000000000";
        MultipartFormBuildResult result = await BuildAsync([B], Text("é", "€"));

        string expected = $"--{B}\r\nContent-Disposition: form-data; name=\"é\"\r\n\r\n€\r\n--{B}--\r\n";
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("length", expectedBytes.Length, result.Body!.Length);
        Assert.AreEqual(expectedBytes.Length, result.Body!.Length);
        byte[] actualBytes = await ReadAllAsync(result.Body.Content);
        diagnostics.Diff("body", expectedBytes, actualBytes);
        CollectionAssert.AreEqual(expectedBytes, actualBytes);
    }

    [TestMethod]
    public async Task TheWindowsAnsiCodePageSendsOneByteACharacterAsCurlDoes()
    {
        // Measured on Windows (code page 1252): -F "né=vé" sent name="n" E9 "" and the value v E9.
        const string B = "------------------------QvntAxMduySrjN2ByXB7K5";
        Encoding windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code page", 1252);
        diagnostics.Arrange("part", "name=né value=vé");
        MultipartFormBodyBuilder builder = new(MeasuredFiles(), windows1252, () => B);

        MultipartFormBuildResult result = await builder.BuildAsync([Text("né", "vé")], TestContext.CancellationToken);

        diagnostics.Act("is built", result.IsBuilt);
        byte[] bytes = await ReadAllAsync(result.Body!.Content);
        string expected = $"--{B}\r\nContent-Disposition: form-data; name=\"né\"\r\n\r\nvé\r\n--{B}--\r\n";
        diagnostics.Diff("body", Encoding.Latin1.GetBytes(expected), bytes);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(expected), bytes);
        diagnostics.Assert("length", bytes.Length, result.Body.Length);
        Assert.AreEqual(bytes.Length, result.Body.Length);
    }

    [TestMethod]
    public async Task AFileThatCannotSeekLeavesTheLengthUnknown()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("pipe", "p");
        MultipartFormBuildResult result = await BuildAsync(files, [B], Text("a", "b"), File("f", MultipartFormPartKind.FileContent, "pipe"));

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("length", null, result.Body.Length);
        Assert.IsNull(result.Body.Length);
        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nb\r\n"
            + $"--{B}\r\nContent-Disposition: form-data; name=\"f\"\r\n\r\np\r\n"
            + $"--{B}--\r\n";
        string actual = Encoding.Latin1.GetString(await ReadAllAsync(result.Body.Content));
        diagnostics.Diff("body", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("abcde", "1PQGfpXEKWOBJUw2cVmmCb", "bl401m-18412-5", "abcde\r\n--{B}")]
    [DataRow("", "hPRLUVz4YTDnuhseH9gMXT", "bl401m-18413-0", "\r\n--{B}--\r\n")]
    public async Task APipeFileDeclaresTheLengthWindowsStatGivesItAndItsFirstBytesMatchCurl(
        string delivered,
        string boundaryLetters,
        string fileName,
        string measuredEnd)
    {
        // Measured (BL-401 Notes): -F f=@\\.\pipe\<name> against a one-instance pipe delivering
        // `delivered` sent Content-Length: 216, the length of a 1-byte file, and a body cut off
        // at it; with nothing delivered the body was one byte short of it.
        string b = "------------------------" + boundaryLetters;
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("pipe", delivered);
        MultipartFormPart part = new("f", MultipartFormPartKind.FileUpload, "pipe", null, fileName, NoHeaders, NoParts);
        MultipartFormBuildResult result = await BuildAsync(files, [b], _ => 1, part);

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("length", 216, result.Body!.Length);
        Assert.AreEqual(216, result.Body!.Length);
        string measured =
            $"--{b}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"{fileName}\"\r\nContent-Type: application/octet-stream\r\n\r\n"
            + measuredEnd.Replace("{B}", b, StringComparison.Ordinal);
        string sent = Encoding.Latin1.GetString(await ReadAllAsync(result.Body.Content));
        string sentPrefix = sent[..(int)Math.Min(sent.Length, result.Body.Length!.Value)];
        diagnostics.Diff("body prefix", measured, sentPrefix);
        Assert.AreEqual(measured, sentPrefix);
    }

    [TestMethod]
    public async Task TheNullDeviceDeclaresNoBytesOnWindowsAsCurlSendsIt()
    {
        // Measured (BL-401 Notes): -F f=@NUL sent Content-Length: 204 and the whole body.
        const string B = "------------------------XM7E1XhXQENTASVd3j0BuN";
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("NUL", string.Empty);
        MultipartFormBuildResult result = await BuildAsync(
            files,
            [B],
            UnseekableFileLength.AsWindowsStatReportsIt,
            File("f", MultipartFormPartKind.FileUpload, "NUL"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"NUL\"\r\nContent-Type: application/octet-stream\r\n\r\n"
            + $"\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, 204, B);
    }

    [TestMethod]
    public async Task TheNullDeviceDeclaresNoLengthOffWindowsAsCurlSendsItChunked()
    {
        // Measured (BL-445 Notes): the OpenSSL build sent -F f=@/dev/null with
        // Transfer-Encoding: chunked, one 0xcd-byte chunk holding this body.
        const string B = "------------------------vqgGmrr7xDndwoOS2J8VDy";
        FormFileSystem files = new FormFileSystem().WithFile("/dev/null", string.Empty);
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file", "/dev/null (empty, POSIX stat)");
        MultipartFormBodyBuilder builder = new(
            files,
            Encoding.UTF8,
            () => B,
            standardInput: null,
            UnseekableFileLength.Unknown,
            SeekableFileLength.AsPosixStatReportsIt);

        MultipartFormBuildResult result = await builder.BuildAsync(
            [File("f", MultipartFormPartKind.FileUpload, "/dev/null")],
            TestContext.CancellationToken);

        diagnostics.Act("is built", result.IsBuilt);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("length", null, result.Body.Length);
        Assert.IsNull(result.Body.Length);
        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"null\"\r\nContent-Type: application/octet-stream\r\n\r\n"
            + $"\r\n--{B}--\r\n";
        byte[] bytes = await ReadAllAsync(result.Body.Content);
        diagnostics.Diff("body", expected, Encoding.Latin1.GetString(bytes));
        Assert.AreEqual(expected, Encoding.Latin1.GetString(bytes));
        diagnostics.Assert("body byte count", 0xcd, bytes.Length);
        Assert.HasCount(0xcd, bytes);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task TheRealNullDeviceIsSentChunkedByDefaultOffWindows()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file", "/dev/null (real device)");
        MultipartFormBodyBuilder builder = new(new PhysicalFileSystem(), Encoding.UTF8, () => "b");

        MultipartFormBuildResult result = await builder.BuildAsync(
            [File("f", MultipartFormPartKind.FileUpload, "/dev/null")],
            TestContext.CancellationToken);

        diagnostics.Act("is built", result.IsBuilt);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("length", null, result.Body.Length);
        Assert.IsNull(result.Body.Length);
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.AccessDenied)]
    [DataRow(FileAccessStatus.IoError)]
    public async Task AFileThatCannotBeOpenedIsReadErrorBeforeAnythingIsSent(FileAccessStatus status)
    {
        // Measured: -F a=b -F f=@nope.txt and -F f=<nope.txt both exit 26 with
        // "curl: (26) Failed to open/read local data from file/application" and no connection.
        FormFileSystem files = new FormFileSystem().WithFile("t.txt", "tt").WithFailure("nope.txt", status);
        MultipartFormBuildResult result = await BuildAsync(
            files,
            ["------------------------0000000000000000000000"],
            File("a", MultipartFormPartKind.FileUpload, "t.txt"),
            File("f", MultipartFormPartKind.FileContent, "nope.txt"));

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("failure status", status);
        diagnostics.Assert("is built", false, result.IsBuilt);
        Assert.IsFalse(result.IsBuilt);
        diagnostics.Assert("body", null, result.Body);
        Assert.IsNull(result.Body);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure.ExitCode);
        diagnostics.Assert("error message", "Failed to open/read local data from file/application", result.Failure.ErrorMessage);
        Assert.AreEqual("Failed to open/read local data from file/application", result.Failure.ErrorMessage);
        diagnostics.Assert("error message constant", MultipartFormBodyBuilder.OpenFailedMessage, result.Failure.ErrorMessage);
        Assert.AreEqual(MultipartFormBodyBuilder.OpenFailedMessage, result.Failure.ErrorMessage);
        diagnostics.Assert("opened file disposed", true, files.Opened.Single().IsDisposed);
        Assert.IsTrue(files.Opened.Single().IsDisposed, "The file opened before the failure is closed.");
    }

    [TestMethod]
    public async Task ADirectoryIsReadErrorWithCurlsReadMessage()
    {
        // Measured: -F f=@<a directory> exits 26 with "curl: (26) read error getting mime data".
        FormFileSystem files = new FormFileSystem().WithFailure("dir", FileAccessStatus.IsDirectory);
        MultipartFormBuildResult result = await BuildAsync(
            files,
            ["------------------------0000000000000000000000", "------------------------1111111111111111111111"],
            Multipart("m", null, File("f", MultipartFormPartKind.FileUpload, "dir")));

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        diagnostics.Assert("error message", "read error getting mime data", result.Failure.ErrorMessage);
        Assert.AreEqual("read error getting mime data", result.Failure.ErrorMessage);
        diagnostics.Assert("error message constant", MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
    }

    [TestMethod]
    public async Task TheCancellationTokenReachesEveryFileOpen()
    {
        using CancellationTokenSource source = new();
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("file", "t.txt");
        FormFileSystem files = new FormFileSystem().WithFile("t.txt", "tt");
        MultipartFormBodyBuilder builder = new(files, Encoding.UTF8, () => "------------------------0000000000000000000000");

        MultipartFormBuildResult result = await builder.BuildAsync([File("a", MultipartFormPartKind.FileUpload, "t.txt")], source.Token);

        diagnostics.Act("open token count", files.OpenTokens.Count);
        diagnostics.Assert("open token", source.Token, files.OpenTokens.Single());
        Assert.AreEqual(source.Token, files.OpenTokens.Single());
        await result.Body!.Content.DisposeAsync();
        diagnostics.Assert("opened file disposed", true, files.Opened.Single().IsDisposed);
        Assert.IsTrue(files.Opened.Single().IsDisposed, "Disposing the body closes its files.");
    }

    [TestMethod]
    public async Task NullPartsAreRefused()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("parts", null);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Encoding.UTF8, MultipartBoundary.CreateRandom);

        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => builder.BuildAsync(null!, TestContext.CancellationToken).AsTask());
        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void ARandomBoundaryIsTwentyFourDashesAndTwentyTwoAlphanumerics()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("boundary source", "MultipartBoundary.CreateRandom");
        string boundary = MultipartBoundary.CreateRandom();

        diagnostics.Act("boundary length", boundary.Length);
        diagnostics.Assert("length", 46, boundary.Length);
        Assert.AreEqual(46, boundary.Length);
        diagnostics.Assert("length constant", MultipartBoundary.Length, boundary.Length);
        Assert.AreEqual(MultipartBoundary.Length, boundary.Length);
        diagnostics.Assert("first 24 are dashes", true, boundary[..24].All(character => character == '-'));
        Assert.IsTrue(boundary[..24].All(character => character == '-'));
        diagnostics.Assert("rest alphanumeric", true, boundary[24..].All(char.IsAsciiLetterOrDigit));
        Assert.IsTrue(boundary[24..].All(char.IsAsciiLetterOrDigit));
        diagnostics.Assert("second boundary differs", true, boundary != MultipartBoundary.CreateRandom());
        Assert.AreNotEqual(boundary, MultipartBoundary.CreateRandom());
    }

    private static MultipartFormPart Text(string? name, string text) =>
        new(name, MultipartFormPartKind.Text, text, null, null, NoHeaders, NoParts);

    private static MultipartFormPart File(string? name, MultipartFormPartKind kind, string path) =>
        new(name, kind, path, null, null, NoHeaders, NoParts);

    private static MultipartFormPart Multipart(string? name, string? contentType, params MultipartFormPart[] parts) =>
        new(name, MultipartFormPartKind.Multipart, string.Empty, contentType, null, NoHeaders, parts);

    private static FormFileSystem MeasuredFiles() =>
        new FormFileSystem()
            .WithFile("f.py", "hi\n")
            .WithFile("g", "yy")
            .WithFile("h.png", [1, 2])
            .WithFile("t.txt", "tt");

    private Task<MultipartFormBuildResult> BuildAsync(string[] boundaries, params MultipartFormPart[] parts) =>
        BuildAsync(MeasuredFiles(), boundaries, parts);

    /// <summary>Builds with no length for a file that cannot seek, as curl on Linux and macOS declares none.</summary>
    private Task<MultipartFormBuildResult> BuildAsync(FormFileSystem files, string[] boundaries, params MultipartFormPart[] parts) =>
        BuildAsync(files, boundaries, UnseekableFileLength.Unknown, parts);

    private async Task<MultipartFormBuildResult> BuildAsync(
        FormFileSystem files,
        string[] boundaries,
        Func<string, long?> unseekableFileLength,
        params MultipartFormPart[] parts)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("boundaries", string.Join(",", boundaries));
        diagnostics.Arrange("part count", parts.Length);
        Queue<string> queue = new(boundaries);
        MultipartFormBodyBuilder builder = new(
            files,
            Encoding.UTF8,
            () => queue.Count > 1 ? queue.Dequeue() : queue.Peek(),
            standardInput: null,
            unseekableFileLength);
        MultipartFormBuildResult result;
        using (diagnostics.Phase("build"))
        {
            result = await builder.BuildAsync(parts, TestContext.CancellationToken);
        }

        diagnostics.Act("is built", result.IsBuilt);
        return result;
    }

    private async Task AssertBodyAsync(MultipartFormBuildResult result, string expected, long expectedLength, string boundary)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("is built", true, result.IsBuilt);
        Assert.IsTrue(result.IsBuilt);
        diagnostics.Assert("failure", null, result.Failure);
        Assert.IsNull(result.Failure);
        diagnostics.Assert("content type", $"multipart/form-data; boundary={boundary}", result.Body.ContentType);
        Assert.AreEqual($"multipart/form-data; boundary={boundary}", result.Body.ContentType);
        diagnostics.Assert("length", expectedLength, result.Body.Length);
        Assert.AreEqual(expectedLength, result.Body.Length);
        byte[] bytes = await ReadAllAsync(result.Body.Content);
        diagnostics.Diff("body", expected, Encoding.Latin1.GetString(bytes));
        Assert.AreEqual(expected, Encoding.Latin1.GetString(bytes));
        diagnostics.Assert("body byte count", (int)expectedLength, bytes.Length);
        Assert.HasCount((int)expectedLength, bytes);
    }

    private async Task<byte[]> ReadAllAsync(Stream content)
    {
        using MemoryStream copy = new();
        await content.CopyToAsync(copy, TestContext.CancellationToken);
        await content.DisposeAsync();
        byte[] bytes = copy.ToArray();
        TestDiagnostics.For(TestContext).Bytes("body", bytes);
        return bytes;
    }
}
