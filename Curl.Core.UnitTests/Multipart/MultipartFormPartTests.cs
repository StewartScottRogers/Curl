using Curl.Testing;

namespace Curl.Core.Multipart;

[TestClass]
public sealed class MultipartFormPartTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void WithReplacesEachPropertyAndKeepsTheRest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        MultipartFormPart inner = new("i", MultipartFormPartKind.Text, "v", null, null, [], []);
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "b", null, null, [], []);
        diagnostics.Arrange("original part", "Name a, Kind Text, Content b");

        MultipartFormPart changed = part with
        {
            Name = "n",
            Kind = MultipartFormPartKind.Multipart,
            Content = "c",
            ContentType = "x/y",
            FileName = "f",
            Headers = ["X-A: 1"],
            Parts = [inner],
        };

        diagnostics.Act("changed", $"Name {changed.Name}, Kind {changed.Kind}, Content {changed.Content}, ContentType {changed.ContentType}, FileName {changed.FileName}");
        diagnostics.Assert("Name", "n", changed.Name);
        Assert.AreEqual("n", changed.Name);
        diagnostics.Assert("Kind", MultipartFormPartKind.Multipart, changed.Kind);
        Assert.AreEqual(MultipartFormPartKind.Multipart, changed.Kind);
        diagnostics.Assert("Content", "c", changed.Content);
        Assert.AreEqual("c", changed.Content);
        diagnostics.Assert("ContentType", "x/y", changed.ContentType);
        Assert.AreEqual("x/y", changed.ContentType);
        diagnostics.Assert("FileName", "f", changed.FileName);
        Assert.AreEqual("f", changed.FileName);
        diagnostics.Assert("Headers", "X-A: 1", changed.Headers.Single());
        Assert.AreEqual("X-A: 1", changed.Headers.Single());
        diagnostics.Assert("Parts holds inner", true, ReferenceEquals(inner, changed.Parts.Single()));
        Assert.AreSame(inner, changed.Parts.Single());
        diagnostics.Assert("original Name", "a", part.Name);
        Assert.AreEqual("a", part.Name);
    }

    [TestMethod]
    [DataRow(MultipartFormPartKind.FileUpload, "-", true)]
    [DataRow(MultipartFormPartKind.FileContent, "-", true)]
    [DataRow(MultipartFormPartKind.FileUpload, "-.txt", false)]
    [DataRow(MultipartFormPartKind.Text, "-", false)]
    [DataRow(MultipartFormPartKind.Multipart, "-", false)]
    public void OnlyAFilePartWhosePathIsADashReadsStandardInput(MultipartFormPartKind kind, string content, bool readsStandardInput)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("kind", kind);
        diagnostics.Arrange("content", content);
        MultipartFormPart part = new("a", kind, content, null, null, [], []);

        diagnostics.Act("ReadsStandardInput", part.ReadsStandardInput);
        diagnostics.Assert("ReadsStandardInput", readsStandardInput, part.ReadsStandardInput);
        Assert.AreEqual(readsStandardInput, part.ReadsStandardInput);
    }
}
