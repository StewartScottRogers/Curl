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
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions(), "http", null, null, CancellationToken.None);

        Assert.IsNull(form);
    }

    [TestMethod]
    public async Task BuildAsync_HttpUrl_BuildsAFormDataBodyWithPercentEscapedNames()
    {
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions { MimePost = OnePart }, "http", null, null, CancellationToken.None);

        string body = await ContentOfAsync(form!);
        Assert.AreEqual("multipart/form-data; boundary=b0", form!.Body!.ContentType);
        StringAssert.Contains(body, "name=\"a%22b\"");
    }

    [TestMethod]
    public async Task BuildAsync_EmptyHttpPostForm_GivesAnEmptyBodyWithNoContentType()
    {
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions { HttpPost = new EasyMimeBody() }, "http", null, null, CancellationToken.None);

        Assert.AreEqual(0, form!.Body!.Length);
        Assert.AreEqual(string.Empty, form.Body.ContentType);
        Assert.AreEqual(string.Empty, await ContentOfAsync(form));
    }

    [TestMethod]
    public async Task BuildAsync_EmptyMimePost_StillBuildsAFormDataBody()
    {
        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, new EasyHttpOptions { MimePost = new EasyMimeBody() }, "http", null, null, CancellationToken.None);

        Assert.AreEqual("multipart/form-data; boundary=b0", form!.Body!.ContentType);
    }

    [TestMethod]
    public async Task BuildAsync_FormEscape_EscapesNamesWithABackslash()
    {
        EasyHttpOptions http = new() { MimePost = OnePart, MimeOptions = MimeOptions.FormEscape };

        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, http, "http", null, null, CancellationToken.None);

        StringAssert.Contains(await ContentOfAsync(form!), "name=\"a\\\"b\"");
    }

    [TestMethod]
    public async Task BuildAsync_SmtpUrl_BuildsAMailMessageHeadedByTheHeaders()
    {
        EasyHttpOptions http = new() { MimePost = OnePart, HttpHeaders = ["Subject: hi"] };

        MultipartFormBuildResult? form = await MimePostForm.BuildAsync(Builder, http, "smtp", null, null, CancellationToken.None);

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

    [TestMethod]
    public void BodyOf_MimePostAndHttpPost_GivesTheMimePost()
    {
        EasyHttpOptions http = new() { MimePost = OnePart, HttpPost = StreamForm(4) };

        Assert.AreSame(OnePart, MimePostForm.BodyOf(http, "http", null));
    }

    [TestMethod]
    public void BodyOf_HttpPostToAMailUrl_GivesNone()
    {
        Assert.IsNull(MimePostForm.BodyOf(new EasyHttpOptions { HttpPost = StreamForm(4) }, "smtp", _ => 0));
    }

    [TestMethod]
    public void BodyOf_HttpPostStreamOfKnownLength_ReadsItThroughTheReadCallbackAtThatSize()
    {
        Api.Callbacks.ReadCallback read = _ => 0;

        EasyMimeBody body = MimePostForm.BodyOf(new EasyHttpOptions { HttpPost = StreamForm(4) }, "http", read)!;

        Assert.AreEqual(new EasyMimeCallback(4, read, null), body.Parts[0].Data);
        Assert.AreEqual("s", body.Parts[0].Name);
    }

    [TestMethod]
    public void BodyOf_HttpPostStreamOfLengthZero_ReadsItAtAnUnknownSize()
    {
        EasyMimeBody body = MimePostForm.BodyOf(new EasyHttpOptions { HttpPost = StreamForm(0) }, "http", _ => 0)!;

        Assert.AreEqual(-1, ((EasyMimeCallback)body.Parts[0].Data!).Size);
    }

    [TestMethod]
    public void BodyOf_HttpPostStreamWithNoReadCallback_SendsItEmpty()
    {
        EasyMimeBody body = MimePostForm.BodyOf(new EasyHttpOptions { HttpPost = StreamForm(4) }, "http", null)!;

        Assert.IsNull(body.Parts[0].Data);
    }

    [TestMethod]
    public void BodyOf_HttpPostTextPart_KeepsIt()
    {
        EasyHttpOptions http = new() { HttpPost = OnePart };

        Assert.AreSame(OnePart.Parts[0], MimePostForm.BodyOf(http, "http", _ => 0)!.Parts[0]);
    }

    // A CURLOPT_HTTPPOST body of one CURLFORM_STREAM field named s with that CURLFORM_CONTENTSLENGTH.
    private static EasyMimeBody StreamForm(long contentsLength) =>
        new() { Parts = [new EasyMimePart { Name = "s", Data = new FormStreamData(contentsLength, new object()) }] };

    private static async Task<string> ContentOfAsync(MultipartFormBuildResult form)
    {
        Assert.IsTrue(form.IsBuilt);
        await using Stream content = form.Body.Content;
        using StreamReader reader = new(content, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
