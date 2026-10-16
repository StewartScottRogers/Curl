using System.Text;
using Curl.Api.Options.Http;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class MimePostFormTests
{
    private static readonly MultipartFormBodyBuilder Builder = new(new PhysicalFileSystem(), Encoding.UTF8, () => "b0");

    private static readonly EasyMimeBody OnePart = new()
    {
        Parts = [new EasyMimePart { Name = "a\"b", Data = new EasyMimeBytes(Encoding.UTF8.GetBytes("1")) }],
    };

    [TestMethod]
    public async Task BuildAsync_NoMimePost_GivesNull()
    {
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions(), "http", CancellationToken.None);

        Assert.IsNull(form);
    }

    [TestMethod]
    public async Task BuildAsync_HttpUrl_BuildsAFormDataBodyWithPercentEscapedNames()
    {
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions { MimePost = OnePart }, "http", CancellationToken.None);

        string body = await ContentOfAsync(form!);
        Assert.AreEqual("multipart/form-data; boundary=b0", form!.Body!.ContentType);
        StringAssert.Contains(body, "name=\"a%22b\"");
    }

    [TestMethod]
    public async Task BuildAsync_FormEscape_EscapesNamesWithABackslash()
    {
        EasyHttpOptions http = new() { MimePost = OnePart, MimeOptions = MimeOptions.FormEscape };

        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, http, "http", CancellationToken.None);

        StringAssert.Contains(await ContentOfAsync(form!), "name=\"a\\\"b\"");
    }

    [TestMethod]
    public async Task BuildAsync_SmtpUrl_BuildsAMailMessageHeadedByTheHeaders()
    {
        EasyHttpOptions http = new() { MimePost = OnePart, HttpHeaders = ["Subject: hi"] };

        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, http, "smtp", CancellationToken.None);

        string message = await ContentOfAsync(form!);
        StringAssert.StartsWith(message, "Content-Type: multipart/mixed; boundary=b0\r\nMime-Version: 1.0\r\nSubject: hi\r\n");
    }

    [TestMethod]
    public void WithBody_NoBody_GivesTheInputsAsTheyWere()
    {
        EngineToolTransferInputs inputs = new(ResumeFrom: 3);

        Assert.AreSame(inputs, MimePostForm.WithBody(inputs, null, "http"));
    }

    [TestMethod]
    public void WithBody_HttpUrl_CarriesTheBodyAsTheFormBody()
    {
        StreamBody body = new(new MemoryStream(), 0, "multipart/form-data; boundary=b0");

        EngineToolTransferInputs inputs = MimePostForm.WithBody(new EngineToolTransferInputs(), body, "https");

        Assert.AreSame(body, inputs.FormBody);
        Assert.IsNull(inputs.Upload);
    }

    [TestMethod]
    public void WithBody_ImapUrl_CarriesTheMessageAsTheUpload()
    {
        StreamBody body = new(new MemoryStream(), 0, "multipart/mixed; boundary=b0");

        EngineToolTransferInputs inputs = MimePostForm.WithBody(new EngineToolTransferInputs(), body, "imap");

        Assert.AreSame(body.Content, inputs.Upload);
        Assert.IsNull(inputs.FormBody);
    }

    private static async Task<string> ContentOfAsync(MultipartFormBuildResult form)
    {
        Assert.IsTrue(form.IsBuilt);
        await using Stream content = form.Body.Content;
        using StreamReader reader = new(content, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
