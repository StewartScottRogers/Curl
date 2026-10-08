using Curl.Cli;
using Curl.Core.Multipart;
using Curl.Testing;

namespace Curl.Console;

[TestClass]
public sealed class MultipartFormPartMappingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromCommandLine_EachPartKind_CopiesEveryFieldAndTheKind()
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
    public void FromCommandLine_NestedMultipart_MapsItsPartsInOrder()
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
        AssertPart(multipart.Parts[1], "y", MultipartFormPartKind.FileUpload, "t.txt", null, null, []);
    }

    [TestMethod]
    public void FromCommandLine_PartWithEncoder_CopiesTheEncoder()
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

    private IReadOnlyList<MultipartFormPart> Map(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.com/"], _ => true);
        Diagnostics.Assert("parsed.IsAccepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);

        return MultipartFormPartMapping.FromCommandLine(parsed.Options.FormParts);
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
