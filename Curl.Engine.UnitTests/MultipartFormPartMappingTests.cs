using System.Text;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Console;
using Curl.Core.Multipart;
using Curl.Testing;

namespace Curl.Engine;

[TestClass]
public sealed class MultipartFormPartMappingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromMimeBody_EachPartKind_CopiesEveryFieldAndTheKind()
    {
        Diagnostics.Arrange("form arguments", "a=b;type=x/y;headers=X-A: 1 | f=@f.txt;filename=q | c=<c.txt | =anonymous");

        IReadOnlyList<MultipartFormPart> parts = Map(
            "-F", "a=b;type=x/y;headers=X-A: 1",
            "-F", "f=@f.txt;filename=q",
            "-F", "c=<c.txt",
            "-F", "=anonymous");
        Diagnostics.Act("part count", parts.Count);

        Diagnostics.Assert("part count", 4, parts.Count);
        Assert.HasCount(4, parts);
        AssertPart(parts[0], "a", MultipartFormPartKind.Text, "b", "x/y", null, ["X-A: 1"]);
        AssertPart(parts[1], "f", MultipartFormPartKind.FileUpload, "f.txt", null, "q", []);
        AssertPart(parts[2], "c", MultipartFormPartKind.FileContent, "c.txt", null, null, []);
        AssertPart(parts[3], null, MultipartFormPartKind.Text, "anonymous", null, null, []);
    }

    [TestMethod]
    public void FromMimeBody_NestedMultipart_MapsItsPartsInOrder()
    {
        Diagnostics.Arrange("form arguments", "m=(;type=multipart/alternative | x=1 | y=@t.txt | =)");

        IReadOnlyList<MultipartFormPart> parts = Map("-F", "m=(;type=multipart/alternative", "-F", "x=1", "-F", "y=@t.txt", "-F", "=)");
        MultipartFormPart multipart = parts.Single();
        Diagnostics.Act("kind", multipart.Kind);
        Diagnostics.Act("content type", multipart.ContentType);
        Diagnostics.Act("nested part count", multipart.Parts.Count);

        Diagnostics.Assert("kind", MultipartFormPartKind.Multipart, multipart.Kind);
        Assert.AreEqual(MultipartFormPartKind.Multipart, multipart.Kind);
        Diagnostics.Assert("content type", "multipart/alternative", multipart.ContentType);
        Assert.AreEqual("multipart/alternative", multipart.ContentType);
        Diagnostics.Assert("nested part count", 2, multipart.Parts.Count);
        Assert.HasCount(2, multipart.Parts);
        AssertPart(multipart.Parts[0], "x", MultipartFormPartKind.Text, "1", null, null, []);
        AssertPart(multipart.Parts[1], "y", MultipartFormPartKind.FileUpload, "t.txt", null, "t.txt", []);
    }

    [TestMethod]
    public void FromMimeBody_PartWithEncoder_CopiesTheEncoder()
    {
        Diagnostics.Arrange("form arguments", "t=hi;encoder=base64 | u=hi");

        IReadOnlyList<MultipartFormPart> parts = Map("-F", "t=hi;encoder=base64", "-F", "u=hi");
        Diagnostics.Act("first encoder", parts[0].Encoder);
        Diagnostics.Act("second encoder", parts[1].Encoder);

        Diagnostics.Assert("first encoder", "base64", parts[0].Encoder);
        Assert.AreEqual("base64", parts[0].Encoder);
        Diagnostics.Assert("second encoder", null, parts[1].Encoder);
        Assert.IsNull(parts[1].Encoder);
    }

    [TestMethod]
    public void FromMimeBody_StandardInputParts_MapToTheStandardInputPath()
    {
        Diagnostics.Arrange("form arguments", "u=@- | c=<- | n=@-;filename=up.bin");

        IReadOnlyList<MultipartFormPart> parts = Map("-F", "u=@-", "-F", "c=<-", "-F", "n=@-;filename=up.bin");
        Diagnostics.Act("part count", parts.Count);

        Assert.HasCount(3, parts);
        AssertPart(parts[0], "u", MultipartFormPartKind.FileUpload, "-", null, "-", []);
        AssertPart(parts[1], "c", MultipartFormPartKind.FileContent, "-", null, null, []);
        AssertPart(parts[2], "n", MultipartFormPartKind.FileUpload, "-", null, "up.bin", []);
        Assert.IsTrue(parts[0].ReadsStandardInput);
        Assert.IsTrue(parts[1].ReadsStandardInput);
    }

    [TestMethod]
    public void FromMimeBody_PartWithNoData_IsAnEmptyTextPart()
    {
        Diagnostics.Arrange("part", "a part fresh from curl_mime_addpart, named e");

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(new EasyMimeBody { Parts = [new EasyMimePart { Name = "e" }] });
        Diagnostics.Act("part count", parts.Count);

        Assert.HasCount(1, parts);
        AssertPart(parts[0], "e", MultipartFormPartKind.Text, string.Empty, null, null, []);
    }

    [TestMethod]
    [DataRow(-1L, null)]
    [DataRow(5L, 5L)]
    public void FromMimeBody_CallbackData_IsAReaderPartOverTheCallbackKeptInCallbackReads(long size, long? expectedLength)
    {
        Diagnostics.Arrange("part", $"a curl_mime_data_cb part of size {size}");
        EasyMimeBody body = new() { Parts = [new EasyMimePart { Name = "c", Data = new EasyMimeCallback(size, _ => 0, null) }] };
        List<CallbackReadStream> reads = [];

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body, reads);
        Diagnostics.Act("kind", parts[0].Kind);

        Assert.AreEqual(MultipartFormPartKind.Reader, parts[0].Kind);
        Assert.AreEqual(expectedLength, parts[0].ContentLength);
        Assert.IsEmpty(reads);
        using Stream opened = parts[0].OpenContent!();
        Assert.IsInstanceOfType<MimeCallbackReadStream>(opened);
        Assert.AreSame(opened, reads.Single());
    }

    [TestMethod]
    public void FromMimeBody_CallbackDataWithoutCallbackReads_OpensAStreamKeptNowhere()
    {
        Diagnostics.Arrange("part", "a curl_mime_data_cb part inside a multipart, no list given");
        EasyMimeBody inner = new() { Parts = [new EasyMimePart { Data = new EasyMimeCallback(-1, _ => 0, null) }] };
        EasyMimeBody body = new() { Parts = [new EasyMimePart { Name = "m", SubParts = inner }] };

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body);
        Diagnostics.Act("inner kind", parts[0].Parts[0].Kind);

        using Stream opened = parts[0].Parts[0].OpenContent!();
        Assert.IsInstanceOfType<MimeCallbackReadStream>(opened);
    }

    [TestMethod]
    public void FromMimeBody_BytesThatAreNotUtf8_IsABytesPartHoldingThemUnchanged()
    {
        byte[] bytes = [0x41, 0xFF, 0x00, 0x80];
        Diagnostics.Arrange("bytes", Convert.ToHexString(bytes));
        EasyMimeBody body = new() { Parts = [new EasyMimePart { Name = "b", Data = new EasyMimeBytes(bytes) }] };

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body);
        Diagnostics.Act("kind", parts[0].Kind);

        Assert.AreEqual(MultipartFormPartKind.Bytes, parts[0].Kind);
        CollectionAssert.AreEqual(bytes, parts[0].Bytes.ToArray());
        Assert.IsNull(parts[0].OpenContent);
        Assert.IsNull(parts[0].ContentLength);
    }

    [TestMethod]
    public void FromMimeBody_BytesThatAreUtf8_IsTextWithNoBytes()
    {
        Diagnostics.Arrange("bytes", "UTF-8 of é");
        EasyMimeBody body = new() { Parts = [new EasyMimePart { Name = "t", Data = new EasyMimeBytes(Encoding.UTF8.GetBytes("é")) }] };

        IReadOnlyList<MultipartFormPart> parts = MultipartFormPartMapping.FromMimeBody(body);
        Diagnostics.Act("kind", parts[0].Kind);

        Assert.AreEqual(MultipartFormPartKind.Text, parts[0].Kind);
        Assert.AreEqual("é", parts[0].Content);
        Assert.IsTrue(parts[0].Bytes.IsEmpty);
    }

    [TestMethod]
    [DataRow("smtp", false, MultipartNameEscaping.Backslash)]
    [DataRow("SMTPS", false, MultipartNameEscaping.Backslash)]
    [DataRow("imap", false, MultipartNameEscaping.Backslash)]
    [DataRow("imaps", false, MultipartNameEscaping.Backslash)]
    [DataRow("http", false, MultipartNameEscaping.Percent)]
    [DataRow("https", false, MultipartNameEscaping.Percent)]
    [DataRow("http", true, MultipartNameEscaping.Backslash)]
    [DataRow("smtp", true, MultipartNameEscaping.Backslash)]
    public void NameEscapingOf_SchemeAndFormEscape_PicksBackslashForMailOrFormEscapeElsePercent(
        string scheme, bool formEscape, MultipartNameEscaping expected)
    {
        Diagnostics.Arrange("scheme", scheme);
        Diagnostics.Arrange("--form-escape", formEscape);
        string[] arguments = formEscape ? ["--form-escape", "-F", "a=b", "http://example.com/"] : ["-F", "a=b", "http://example.com/"];
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        MultipartNameEscaping escaping = MultipartFormPartMapping.NameEscapingOf(parsed.Options.FormEscape, scheme);
        Diagnostics.Act("escaping", escaping);

        Diagnostics.Assert("escaping", expected, escaping);
        Assert.AreEqual(expected, escaping);
    }

    private IReadOnlyList<MultipartFormPart> Map(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.com/"], _ => true);
        Diagnostics.Assert("parsed.IsAccepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);

        return MultipartFormPartMapping.FromMimeBody(EasyOptionsFromCommandLine.HttpOf(parsed.Options).MimePost!);
    }

    private void AssertPart(
        MultipartFormPart part,
        string? name,
        MultipartFormPartKind kind,
        string content,
        string? contentType,
        string? fileName,
        string[] headers)
    {
        Diagnostics.Assert("part name", name, part.Name);
        Assert.AreEqual(name, part.Name);
        Diagnostics.Assert("part kind", kind, part.Kind);
        Assert.AreEqual(kind, part.Kind);
        Diagnostics.Assert("part content", content, part.Content);
        Assert.AreEqual(content, part.Content);
        Diagnostics.Assert("part content type", contentType, part.ContentType);
        Assert.AreEqual(contentType, part.ContentType);
        Diagnostics.Assert("part file name", fileName, part.FileName);
        Assert.AreEqual(fileName, part.FileName);
        Diagnostics.Assert("part headers", string.Join("|", headers), string.Join("|", part.Headers));
        CollectionAssert.AreEqual(headers, part.Headers.ToArray());
        Diagnostics.Assert("nested part count", 0, part.Parts.Count);
        Assert.IsEmpty(part.Parts);
    }
}
