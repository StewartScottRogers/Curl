using Curl.Api.Messages;
using Curl.Api.Mime;
using Curl.Api.Options;

namespace Curl.Api.RequestBodies;

[TestClass]
public sealed class UploadSchemeBodyTests
{
    private static readonly byte[] Hello = "hello world"u8.ToArray();

    private static async Task<UploadingEasyTransfer> SendAsync(HttpMethod method, string url, CurlContent body)
    {
        UploadingEasyTransfer easy = new();
        await using CurlClient client = new(new UploadingCurlEngine(easy));
        _ = await client.SendAsync(new CurlRequest(method, url) { Body = body }, CancellationToken.None);
        return easy;
    }

    [TestMethod]
    [DataRow("ftp://h/f")]
    [DataRow("FTPS://h/f")]
    [DataRow("sftp://h/f")]
    [DataRow("scp://h/f")]
    [DataRow("smb://h/s/f")]
    [DataRow("smbs://h/s/f")]
    [DataRow("smtp://h/")]
    [DataRow("smtps://h/")]
    [DataRow("imap://h/INBOX")]
    [DataRow("imaps://h/INBOX")]
    [DataRow("tftp://h/f")]
    [DataRow("telnet://h/")]
    [DataRow("s3://b/k")]
    [DataRow("az://c/b")]
    [DataRow("abfs://c@a.dfs.core.windows.net/f")]
    [DataRow("abfss://c@a.dfs.core.windows.net/f")]
    [DataRow("gs://b/k")]
    [DataRow("oci://r/p:t")]
    [DataRow("kafka://h/t")]
    [DataRow("kafkas://h/t")]
    [DataRow("grpc://h/p.S/M")]
    [DataRow("grpcs://h/p.S/M")]
    [DataRow("signalr://h/hub")]
    [DataRow("signalrs://h/hub")]
    [DataRow("amqp://h/q")]
    [DataRow("amqps://h/q")]
    [DataRow("amqp1://h/q")]
    [DataRow("amqp1s://h/q")]
    [DataRow("mqtt5://h/t")]
    [DataRow("mqtt5s://h/t")]
    [DataRow("mqtt5+ws://h/t")]
    [DataRow("stomp://h/q")]
    [DataRow("stomps://h/q")]
    [DataRow("nats://h/s")]
    [DataRow("natss://h/s")]
    [DataRow("redis://h/0/k")]
    [DataRow("rediss://h/0/k")]
    [DataRow("xmpp://h/j")]
    [DataRow("xmpps://h/j")]
    public async Task SendAsync_BytesBodyToAnUploadScheme_UploadsWithItsLength(string url)
    {
        UploadingEasyTransfer easy = await SendAsync(HttpMethod.Put, url, CurlContent.FromBytes(Hello));

        EasyOptions options = easy.PerformedOptions!;
        Assert.IsTrue(options.Transfer.Upload);
        Assert.AreEqual(Hello.Length, options.Transfer.InFileSize);
        Assert.IsNull(options.Transfer.CustomRequest);
        Assert.IsFalse(options.Http.Post);
        Assert.IsNull(options.Http.PostFields);
        CollectionAssert.AreEqual(Hello, easy.Uploads.Single());
    }

    [TestMethod]
    [DataRow("ftp://h/f", "POST")]
    [DataRow("smtp://h/", "GET")]
    [DataRow("telnet://h/", "PUT")]
    [DataRow("gs://b/k", "PUT")]
    [DataRow("oci://r/p:t", "PUT")]
    [DataRow("abfs://c@a.dfs.core.windows.net/f", "PUT")]
    [DataRow("imap://h/INBOX", "PUT")]
    [DataRow("kafka://h/t", "POST")]
    [DataRow("grpc://h/p.S/M", "POST")]
    [DataRow("signalr://h/hub", "POST")]
    public async Task SendAsync_StreamBodyToAnUploadScheme_UploadsIgnoringTheMethod(string url, string method)
    {
        using ScriptedStream stream = new(Hello) { Position = 6 };

        UploadingEasyTransfer easy = await SendAsync(new HttpMethod(method), url, CurlContent.FromStream(stream));

        EasyOptions options = easy.PerformedOptions!;
        Assert.IsTrue(options.Transfer.Upload);
        Assert.AreEqual(5, options.Transfer.InFileSize);
        Assert.IsFalse(options.Http.Post);
        CollectionAssert.AreEqual("world"u8.ToArray(), easy.Uploads.Single());
    }

    [TestMethod]
    [DataRow("mqtt5://h/t")]
    [DataRow("mqtt5s://h/t")]
    [DataRow("mqtt5+ws://h/t")]
    public async Task SendAsync_PostedBytesAndStreamBodiesToMqtt5_PublishAsAnUpload(string url)
    {
        UploadingEasyTransfer bytes = await SendAsync(HttpMethod.Post, url, CurlContent.FromBytes("21.5"u8.ToArray()));
        using ScriptedStream stream = new("21.5"u8.ToArray());
        UploadingEasyTransfer streamed = await SendAsync(HttpMethod.Post, url, CurlContent.FromStream(stream));

        foreach (UploadingEasyTransfer easy in new[] { bytes, streamed })
        {
            Assert.IsTrue(easy.PerformedOptions!.Transfer.Upload);
            Assert.IsFalse(easy.PerformedOptions.Http.Post);
            Assert.IsNull(easy.PerformedOptions.Http.PostFields);
            CollectionAssert.AreEqual("21.5"u8.ToArray(), easy.Uploads.Single());
        }
    }

    [TestMethod]
    public async Task SendAsync_MultipartBodyToSmtp_IsStillSentAsMime()
    {
        ScriptedEasyTransfer easy = new();
        await using CurlClient client = new(new MimeCurlEngine(easy));

        _ = await client.SendAsync(new CurlRequest(HttpMethod.Post, "smtp://h/") { Body = new CurlMultipartContent() }, CancellationToken.None);

        Assert.IsFalse(easy.PerformedOptions!.Transfer.Upload);
        Assert.IsNotNull(easy.PerformedOptions.Http.MimePost);
    }

    [TestMethod]
    public async Task SendAsync_BytesBodyPostedToHttp_IsStillPosted()
    {
        UploadingEasyTransfer easy = await SendAsync(HttpMethod.Post, "http://h/", CurlContent.FromBytes(Hello));

        Assert.IsFalse(easy.PerformedOptions!.Transfer.Upload);
        Assert.IsTrue(easy.PerformedOptions.Http.Post);
    }

    [TestMethod]
    public async Task SendAsync_BytesBodyPutToHttp_IsUploaded()
    {
        UploadingEasyTransfer easy = await SendAsync(HttpMethod.Put, "http://h/", CurlContent.FromBytes(Hello));

        Assert.IsTrue(easy.PerformedOptions!.Transfer.Upload);
        Assert.IsFalse(easy.PerformedOptions.Http.Post);
        Assert.IsNull(easy.PerformedOptions.Transfer.CustomRequest);
    }

    [TestMethod]
    [DataRow("nocolon", false)]
    [DataRow("://x", false)]
    [DataRow("http://x", false)]
    [DataRow("ftp://x", true)]
    public void UploadsWithBody_Url_ReportsTheScheme(string url, bool expected) =>
        Assert.AreEqual(expected, UploadSchemes.UploadsWithBody(url));
}
