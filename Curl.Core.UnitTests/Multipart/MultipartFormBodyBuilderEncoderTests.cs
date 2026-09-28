using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the <c>;encoder=</c> parts <see cref="MultipartFormBodyBuilder" /> builds against the
/// ones curl 8.21.0 (<c>/mingw64/bin/curl</c>, Schannel, code page 1252) sent on 2026-09-26, each
/// recorded as <c>Record-CurlExchange.ps1 -CurlArgs -s,-S,-F,&lt;spec&gt;,http://127.0.0.1:&lt;port&gt;/</c>
/// (with <c>-H Expect:</c> for the chunked ones) in a folder holding <c>data.txt</c>, the UTF-8
/// bytes of <see cref="DataText" />. Each test injects the boundary curl chose for that run.
/// </summary>
[TestClass]
public sealed class MultipartFormBodyBuilderEncoderTests
{
    private const string DataText =
        "The quick brown fox = jumps over the lazy dog; it keeps on jumping and jumping and jumping, far beyond seventy-six columns. "
        + "tab\tend \r\nsecond line with trailing space \nthird é line\r\n.\r\nFrom here\r\n";

    private static readonly string[] NoHeaders = [];

    private static readonly MultipartFormPart[] NoParts = [];

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("t=hello = world é;encoder=base64", "base64", "hello = world é", "------------------------BjS8ZMmQ76GqneCB2tZMPE", "base64", "aGVsbG8gPSB3b3JsZCDp", 203)]
    [DataRow("t=hello = world é;encoder=8bit", "8bit", "hello = world é", "------------------------BJjSewc4hb9rFOr85GwGZe", "8bit", "hello = world é", 196)]
    [DataRow("t=hello = world é;encoder=binary", "binary", "hello = world é", "------------------------HKZXTWzCcKlMM3ukjnMRtq", "binary", "hello = world é", 198)]
    [DataRow("t=hi there;encoder=7bit", "7bit", "hi there", "------------------------cSf4p93i1MJj4QC3Kfesvk", "7bit", "hi there", 189)]
    [DataRow("t=hi;encoder=BASE64", "BASE64", "hi", "------------------------vMiGqxWAGqF0biXnwewcGS", "base64", "aGk=", 187)]
    [DataRow("t=;encoder=base64", "base64", "", "------------------------ttkzoh9VSFT2XfkjxBCkJG", "base64", "", 183)]
    [DataRow("t=;encoder=quoted-printable", "quoted-printable", "", "------------------------WHI05472KRQTwAROwDjwA6", "quoted-printable", "", 193)]
    [DataRow(
        "t=<57 x>;encoder=base64",
        "base64",
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "------------------------aoOKVQruh5auRwhVS51KNe",
        "base64",
        "eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4",
        259)]
    [DataRow(
        "t=<58 x>;encoder=base64",
        "base64",
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "------------------------ZAWpxjd2ZAuaJvH9Tg2vWy",
        "base64",
        "eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4eHh4\r\neA==",
        265)]
    public async Task EncodedTextPartMatchesCurl(
        string spec,
        string encoder,
        string value,
        string boundary,
        string sentEncoderName,
        string sentBody,
        int expectedLength)
    {
        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), boundary, TextPart(value, encoder));

        string expected =
            $"--{boundary}\r\nContent-Disposition: form-data; name=\"t\"\r\nContent-Transfer-Encoding: {sentEncoderName}\r\n\r\n{sentBody}\r\n"
            + $"--{boundary}--\r\n";
        await AssertBodyAsync(result, expected, expectedLength, spec);
    }

    [TestMethod]
    public async Task QuotedPrintableTextPartLeavesTheLengthUnknownMatchingCurl()
    {
        // -H Expect: -F "t=hello = world é;encoder=quoted-printable" went chunked.
        const string B = "------------------------eE79G2bGAQVnkXlJ9dqDz2";
        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), B, TextPart("hello = world é", "quoted-printable"));

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"t\"\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nhello =3D world =E9\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, null, "qp text");
    }

    [TestMethod]
    public async Task OwnContentTransferEncodingHeaderReplacesTheEncodersButTheBodyIsStillEncoded()
    {
        // -F 't=hi;encoder=base64;headers="Content-Transfer-Encoding: x"'
        const string B = "------------------------tdekL89O1ovaPyqDugHdiq";
        MultipartFormPart part = new("t", MultipartFormPartKind.Text, "hi", null, null, ["Content-Transfer-Encoding: x"], NoParts)
        {
            Encoder = "base64",
        };
        MultipartFormBuildResult result = await BuildAsync(new FormFileSystem(), B, part);

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"t\"\r\nContent-Transfer-Encoding: x\r\n\r\naGk=\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 182, "own header");
    }

    [TestMethod]
    public async Task Base64FilePartIsSentInSeventySixColumnLinesMatchingCurl()
    {
        // -F "f=@data.txt;encoder=base64"
        const string B = "------------------------laJw255OheN9kyS2wz2QYu";
        MultipartFormBuildResult result = await BuildAsync(DataFiles(), B, FilePart("base64"));

        string expected =
            FileHeaders(B, "base64")
            + "VGhlIHF1aWNrIGJyb3duIGZveCA9IGp1bXBzIG92ZXIgdGhlIGxhenkgZG9nOyBpdCBrZWVwcyBv\r\n"
            + "biBqdW1waW5nIGFuZCBqdW1waW5nIGFuZCBqdW1waW5nLCBmYXIgYmV5b25kIHNldmVudHktc2l4\r\n"
            + "IGNvbHVtbnMuIHRhYgllbmQgDQpzZWNvbmQgbGluZSB3aXRoIHRyYWlsaW5nIHNwYWNlIAp0aGly\r\n"
            + "ZCDDqSBsaW5lDQouDQpGcm9tIGhlcmUNCg==\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, 500, "base64 file");
    }

    [TestMethod]
    public async Task QuotedPrintableFilePartMatchesCurlAndLeavesTheLengthUnknown()
    {
        // -F "f=@data.txt;encoder=quoted-printable" went chunked.
        const string B = "------------------------fqnA3Sv7t2lHKF5SFhqQBR";
        MultipartFormBuildResult result = await BuildAsync(DataFiles(), B, FilePart("quoted-printable"));

        string expected =
            FileHeaders(B, "quoted-printable")
            + "The quick brown fox =3D jumps over the lazy dog; it keeps on jumping and ju=\r\n"
            + "mping and jumping, far beyond seventy-six columns. tab\tend=20\r\n"
            + "second line with trailing space =0Athird =C3=A9 line\r\n"
            + ".\r\n"
            + "From here\r\n"
            + "\r\n"
            + $"--{B}--\r\n";
        await AssertBodyAsync(result, expected, null, "qp file");
    }

    [TestMethod]
    [DataRow("8bit", "------------------------ITnJ9uQO3q4j4XipKwikbz", 424)]
    [DataRow("binary", "------------------------GT6gasLMdWrqb1A026XcaT", 426)]
    public async Task EightBitAndBinaryFilePartsAreStreamedAsTheyAreMatchingCurl(string encoder, string boundary, int expectedLength)
    {
        // -F "f=@data.txt;encoder=<encoder>"
        FormFileSystem files = DataFiles();
        MultipartFormBuildResult result = await BuildAsync(files, boundary, FilePart(encoder));

        string expected = FileHeaders(boundary, encoder) + Windows1252.GetString(Encoding.UTF8.GetBytes(DataText)) + $"\r\n--{boundary}--\r\n";
        Assert.IsFalse(files.Opened.Single().IsDisposed, "The file is streamed, not read while building.");
        await AssertBodyAsync(result, expected, expectedLength, encoder);
    }

    [TestMethod]
    public async Task SevenBitDataAboveOneHundredTwentySevenIsReadErrorMatchingCurl()
    {
        // -F "t=hello = world é;encoder=7bit" and -F "f=@data.txt;encoder=7bit" both exit 26 with
        // "curl: (26) read error getting mime data".
        FormFileSystem files = DataFiles();
        MultipartFormBuildResult text = await BuildAsync(files, "------------------------qeWzJBSHPeBMTn23CunR06", TextPart("hello = world é", "7bit"));
        MultipartFormBuildResult file = await BuildAsync(files, "------------------------wJSaLIauxbMEuqsSoRUDKZ", FilePart("7bit"));

        foreach (MultipartFormBuildResult result in new[] { text, file })
        {
            Assert.IsFalse(result.IsBuilt);
            Assert.AreEqual(CurlExitCode.ReadError, result.Failure.ExitCode);
            Assert.AreEqual("read error getting mime data", result.Failure.ErrorMessage);
        }

        Assert.IsTrue(files.Opened.Single().IsDisposed, "The file read for checking is closed.");
    }

    [TestMethod]
    public async Task SevenBitRefusalWaitsForAFileThatCannotBeOpened()
    {
        // curl opens every file before it sends, and meets 7-bit data only while sending.
        FormFileSystem files = new FormFileSystem().WithFailure("nope.txt", FileAccessStatus.NotFound);
        MultipartFormBuildResult result = await BuildAsync(
            files,
            "------------------------0000000000000000000000",
            TextPart("é", "7bit"),
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "nope.txt", null, null, NoHeaders, NoParts));

        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(MultipartFormBodyBuilder.OpenFailedMessage, result.Failure.ErrorMessage);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("")]
    public async Task AnUnknownEncoderIsBadFunctionArgumentMatchingCurl(string encoder)
    {
        // -F "t=hi;encoder=bogus", -F "f=@data.txt;encoder=bogus" and -F "t=hi;encoder=" exit 43
        // with "curl: (43) A libcurl function was given a bad argument"; so does
        // -F "b=x;encoder=bogus" -F a=@missing.txt, while -F a=@missing.txt -F "b=x;encoder=bogus" exits 26.
        FormFileSystem files = DataFiles().WithFailure("missing.txt", FileAccessStatus.NotFound);
        MultipartFormPart missing = new("a", MultipartFormPartKind.FileUpload, "missing.txt", null, null, NoHeaders, NoParts);
        const string B = "------------------------0000000000000000000000";

        MultipartFormBuildResult text = await BuildAsync(files, B, TextPart("hi", encoder), missing);
        MultipartFormBuildResult file = await BuildAsync(files, B, FilePart(encoder));
        MultipartFormBuildResult missingFirst = await BuildAsync(files, B, missing, TextPart("x", encoder));

        foreach (MultipartFormBuildResult result in new[] { text, file })
        {
            Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.Failure!.ExitCode);
            Assert.AreEqual("A libcurl function was given a bad argument", result.Failure.ErrorMessage);
            Assert.AreEqual(MultipartFormBodyBuilder.UnknownEncoderMessage, result.Failure.ErrorMessage);
        }

        Assert.AreEqual(CurlExitCode.ReadError, missingFirst.Failure!.ExitCode);
        Assert.IsEmpty(files.Opened, "An unknown encoder fails before its file is opened.");
    }

    [TestMethod]
    public async Task AnEncodedFileThatCannotSeekLeavesTheLengthUnknown()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("pipe", "hi");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            B,
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "pipe", null, null, NoHeaders, NoParts) { Encoder = "base64" });

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"f\"\r\nContent-Transfer-Encoding: base64\r\n\r\naGk=\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, null, "pipe");
    }

    [TestMethod]
    public async Task AnEncodedFileThatCannotBeReadIsReadError()
    {
        FormFileSystem files = new FormFileSystem().WithUnreadableFile("dev");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            "------------------------0000000000000000000000",
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "dev", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        Assert.AreEqual(CurlExitCode.ReadError, result.Failure!.ExitCode);
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, result.Failure.ErrorMessage);
        Assert.IsTrue(files.Opened.Single().IsDisposed);
    }

    [TestMethod]
    [DataRow("base64")]
    [DataRow("quoted-printable")]
    public async Task AnEncodedFilePartIsNotReadBeforeTheBodyIsRead(string encoder)
    {
        FormFileSystem files = DataFiles();
        MultipartFormBuildResult result = await BuildAsync(files, "------------------------0000000000000000000000", FilePart(encoder));

        FormFileSystem.TrackedStream file = files.Opened.Single();
        Assert.IsTrue(result.IsBuilt);
        Assert.AreEqual(0, file.Position, "Nothing is read while building.");
        Assert.IsFalse(file.IsDisposed);

        await result.Body.Content.ReadExactlyAsync(new byte[300], TestContext.CancellationToken);
        Assert.AreNotEqual(0, file.Position, "The file is read as the body is read.");
        await result.Body.Content.DisposeAsync();
        Assert.IsTrue(file.IsDisposed);
    }

    [TestMethod]
    public async Task AStreamedEncodedFileThatCannotBeReadFailsTheBodysRead()
    {
        // A base64 file is read only while sending, so a failed read reaches whoever sends the
        // body, as it does for a file sent as it is.
        FormFileSystem files = new FormFileSystem().WithUnreadableFile("dev");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            "------------------------0000000000000000000000",
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "dev", null, null, NoHeaders, NoParts) { Encoder = "base64" });

        Assert.IsTrue(result.IsBuilt);
        await Assert.ThrowsExactlyAsync<IOException>(() => result.Body.Content.CopyToAsync(Stream.Null, TestContext.CancellationToken));
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    public async Task ASevenBitFileIsCheckedThenStreamedWithItsLength()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithFile("data.txt", "plain\r\nascii");
        MultipartFormBuildResult result = await BuildAsync(files, B, FilePart("7bit"));

        Assert.IsFalse(files.Opened.Single().IsDisposed, "The checked file is streamed, not read into memory.");
        Assert.AreEqual(0, files.Opened.Single().Position, "The check leaves the file at its start.");
        string expected = FileHeaders(B, "7bit") + "plain\r\nascii" + $"\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, "7bit file");
    }

    [TestMethod]
    public async Task ASevenBitFileThatCannotSeekIsNotReadWhileBuildingAndLeavesTheLengthUnknown()
    {
        const string B = "------------------------0000000000000000000000";
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("pipe", "hi");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            B,
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "pipe", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        FormFileSystem.TrackedStream pipe = files.Opened.Single();
        Assert.AreEqual(0, pipe.Position, "A pipe cannot be read twice, so nothing is read while building.");
        Assert.IsFalse(pipe.IsDisposed, "The pipe is streamed, not read into memory.");
        string expected = $"--{B}\r\nContent-Disposition: form-data; name=\"f\"\r\nContent-Transfer-Encoding: 7bit\r\n\r\nhi\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, null, "7bit pipe");
    }

    [TestMethod]
    public async Task ASevenBitFileThatCannotSeekFailsTheReadThatReachesARefusedByte()
    {
        // curl 8.21.0 meets the byte only while sending: exit 26, "read error getting mime data".
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("high", "x").WithFile("high", [0x41, 0xE9]);
        MultipartFormBuildResult result = await BuildAsync(
            files,
            "------------------------0000000000000000000000",
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "high", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        Assert.IsTrue(result.IsBuilt);
        RequestBodyReadFailedException refused = await Assert.ThrowsExactlyAsync<RequestBodyReadFailedException>(
            () => result.Body.Content.CopyToAsync(Stream.Null, TestContext.CancellationToken));
        Assert.AreEqual(MultipartFormBodyBuilder.ReadFailedMessage, refused.Message);
        await result.Body.Content.DisposeAsync();
    }

    [TestMethod]
    public async Task ASevenBitFileThatCannotSeekOrBeReadFailsTheBodysRead()
    {
        FormFileSystem files = new FormFileSystem().WithUnseekableFile("dev", "u").WithUnreadableFile("dev");
        MultipartFormBuildResult result = await BuildAsync(
            files,
            "------------------------0000000000000000000000",
            new MultipartFormPart("f", MultipartFormPartKind.FileContent, "dev", null, null, NoHeaders, NoParts) { Encoder = "7bit" });

        Assert.IsTrue(result.IsBuilt);
        await Assert.ThrowsExactlyAsync<IOException>(() => result.Body.Content.CopyToAsync(Stream.Null, TestContext.CancellationToken));
        await result.Body.Content.DisposeAsync();
        Assert.IsTrue(files.Opened.Single().IsDisposed);
    }

    [TestMethod]
    public async Task AnEncoderOnAMultipartPartIsIgnored()
    {
        const string B = "------------------------0000000000000000000000";
        const string Inner = "------------------------1111111111111111111111";
        Queue<string> boundaries = new([B, Inner]);
        MultipartFormBodyBuilder builder = new(new FormFileSystem(), Windows1252, boundaries.Dequeue);
        MultipartFormPart part = new("m", MultipartFormPartKind.Multipart, string.Empty, null, null, NoHeaders, NoParts) { Encoder = "bogus" };

        MultipartFormBuildResult result = await builder.BuildAsync([part], TestContext.CancellationToken);

        string expected =
            $"--{B}\r\nContent-Disposition: form-data; name=\"m\"\r\nContent-Type: multipart/mixed; boundary={Inner}\r\n\r\n"
            + $"--{Inner}--\r\n\r\n--{B}--\r\n";
        await AssertBodyAsync(result, expected, expected.Length, "multipart");
    }

    private static MultipartFormPart TextPart(string value, string encoder) =>
        new("t", MultipartFormPartKind.Text, value, null, null, NoHeaders, NoParts) { Encoder = encoder };

    private static MultipartFormPart FilePart(string encoder) =>
        new("f", MultipartFormPartKind.FileUpload, "data.txt", null, null, NoHeaders, NoParts) { Encoder = encoder };

    private static string FileHeaders(string boundary, string encoder) =>
        $"--{boundary}\r\nContent-Disposition: form-data; name=\"f\"; filename=\"data.txt\"\r\n"
        + $"Content-Type: text/plain\r\nContent-Transfer-Encoding: {encoder}\r\n\r\n";

    private static FormFileSystem DataFiles() => new FormFileSystem().WithFile("data.txt", Encoding.UTF8.GetBytes(DataText));

    /// <summary>Builds with no length for a file that cannot seek, as curl on Linux and macOS declares none.</summary>
    private async Task<MultipartFormBuildResult> BuildAsync(FormFileSystem files, string boundary, params MultipartFormPart[] parts)
    {
        MultipartFormBodyBuilder builder = new(files, Windows1252, () => boundary, standardInput: null, UnseekableFileLength.Unknown);
        return await builder.BuildAsync(parts, TestContext.CancellationToken);
    }

    private async Task AssertBodyAsync(MultipartFormBuildResult result, string expected, long? expectedLength, string spec)
    {
        Assert.IsTrue(result.IsBuilt, spec);
        Assert.AreEqual(expectedLength, result.Body.Length, spec);
        using MemoryStream copy = new();
        await result.Body.Content.CopyToAsync(copy, TestContext.CancellationToken);
        await result.Body.Content.DisposeAsync();
        byte[] bytes = copy.ToArray();
        Assert.AreEqual(expected, Windows1252.GetString(bytes), spec);
    }
}
