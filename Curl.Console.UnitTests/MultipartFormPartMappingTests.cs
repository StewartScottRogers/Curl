using Curl.Cli;
using Curl.Core.Multipart;

namespace Curl.Console;

[TestClass]
public sealed class MultipartFormPartMappingTests
{
    [TestMethod]
    public void FromCommandLine_EachPartKind_CopiesEveryFieldAndTheKind()
    {
        IReadOnlyList<MultipartFormPart> parts = Map(
            "-F", "a=b;type=x/y;headers=X-A: 1",
            "-F", "f=@f.txt;filename=q",
            "-F", "c=<c.txt",
            "-F", "=anonymous");

        Assert.HasCount(4, parts);
        AssertPart(parts[0], "a", MultipartFormPartKind.Text, "b", "x/y", null, ["X-A: 1"]);
        AssertPart(parts[1], "f", MultipartFormPartKind.FileUpload, "f.txt", null, "q", []);
        AssertPart(parts[2], "c", MultipartFormPartKind.FileContent, "c.txt", null, null, []);
        AssertPart(parts[3], null, MultipartFormPartKind.Text, "anonymous", null, null, []);
    }

    [TestMethod]
    public void FromCommandLine_NestedMultipart_MapsItsPartsInOrder()
    {
        IReadOnlyList<MultipartFormPart> parts = Map("-F", "m=(;type=multipart/alternative", "-F", "x=1", "-F", "y=@t.txt", "-F", "=)");

        MultipartFormPart multipart = parts.Single();
        Assert.AreEqual(MultipartFormPartKind.Multipart, multipart.Kind);
        Assert.AreEqual("multipart/alternative", multipart.ContentType);
        Assert.HasCount(2, multipart.Parts);
        AssertPart(multipart.Parts[0], "x", MultipartFormPartKind.Text, "1", null, null, []);
        AssertPart(multipart.Parts[1], "y", MultipartFormPartKind.FileUpload, "t.txt", null, null, []);
    }

    private static IReadOnlyList<MultipartFormPart> Map(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://example.com/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);

        return MultipartFormPartMapping.FromCommandLine(parsed.Options.FormParts);
    }

    private static void AssertPart(
        MultipartFormPart part,
        string? name,
        MultipartFormPartKind kind,
        string content,
        string? contentType,
        string? fileName,
        string[] headers)
    {
        Assert.AreEqual(name, part.Name);
        Assert.AreEqual(kind, part.Kind);
        Assert.AreEqual(content, part.Content);
        Assert.AreEqual(contentType, part.ContentType);
        Assert.AreEqual(fileName, part.FileName);
        CollectionAssert.AreEqual(headers, part.Headers.ToArray());
        Assert.IsEmpty(part.Parts);
    }
}
