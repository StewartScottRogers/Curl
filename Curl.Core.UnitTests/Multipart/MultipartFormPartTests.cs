namespace Curl.Core.Multipart;

[TestClass]
public sealed class MultipartFormPartTests
{
    [TestMethod]
    public void WithReplacesEachPropertyAndKeepsTheRest()
    {
        MultipartFormPart inner = new("i", MultipartFormPartKind.Text, "v", null, null, [], []);
        MultipartFormPart part = new("a", MultipartFormPartKind.Text, "b", null, null, [], []);

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

        Assert.AreEqual("n", changed.Name);
        Assert.AreEqual(MultipartFormPartKind.Multipart, changed.Kind);
        Assert.AreEqual("c", changed.Content);
        Assert.AreEqual("x/y", changed.ContentType);
        Assert.AreEqual("f", changed.FileName);
        Assert.AreEqual("X-A: 1", changed.Headers.Single());
        Assert.AreSame(inner, changed.Parts.Single());
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
        MultipartFormPart part = new("a", kind, content, null, null, [], []);

        Assert.AreEqual(readsStandardInput, part.ReadsStandardInput);
    }
}
