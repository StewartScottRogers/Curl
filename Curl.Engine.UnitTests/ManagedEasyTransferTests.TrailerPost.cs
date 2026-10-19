using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// The trailer callback on a CURLOPT_POSTFIELDS post made chunked by its header (upstream test1598, BL-2665).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_PostFieldsChunkedByHeaderWithTrailerCallback_ClosesTheChunkedBodyWithItsLines()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = PostFieldsTrailerOptions(["Transfer-Encoding: chunked"], trailers =>
        {
            trailers.Add("my-super-awesome-trailer: trail1");
            return TrailerResult.Ok;
        });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "\r\n\r\n7\r\nxxx=yyy\r\n0\r\nmy-super-awesome-trailer: trail1\r\n\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_PostFieldsNotChunkedWithTrailerCallback_SendsContentLengthAndNeverAsksForTrailers()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        int calls = 0;
        easy.Options = PostFieldsTrailerOptions([], _ =>
        {
            calls++;
            return TrailerResult.Ok;
        });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0, calls);
        string written = Encoding.ASCII.GetString(connector.Written);
        StringAssert.Contains(written, "Content-Length: 7\r\n");
        StringAssert.EndsWith(written, "\r\n\r\nxxx=yyy");
    }

    [TestMethod]
    public async Task PerformAsync_PostFieldsChunkedByHeaderWithTrailerCallbackAborting_ReturnsAbortedByCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = PostFieldsTrailerOptions(["Transfer-Encoding: chunked"], _ => TrailerResult.Abort);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("operation aborted by trailing headers callback", easy.ErrorText);
    }

    [TestMethod]
    [DataRow("http://h/x", false, true)]
    [DataRow("https://h/x", false, true)]
    [DataRow("smtp://h/x", false, false)]
    [DataRow("http://h/x", true, false)]
    public void TrailerPostFieldsBodyOf_SchemeAndUpload_GivesAStreamBodyOnlyForAnHttpPost(string url, bool upload, bool expected)
    {
        EasyOptions options = PostFieldsTrailerOptions([], _ => TrailerResult.Ok) with { Transfer = new() { Upload = upload } };

        StreamBody? body = ManagedEasyTransfer.TrailerPostFieldsBodyOf(null, options, CurlUrl.Parse(url));

        Assert.AreEqual(expected, body is { Content: PostFieldsTrailerStream, Length: 7 });
    }

    [TestMethod]
    public void TrailerPostFieldsBodyOf_NoTrailerCallbackOrToolInputs_GivesNone()
    {
        EasyOptions options = PostFieldsTrailerOptions([], _ => TrailerResult.Ok);
        CurlUrl url = CurlUrl.Parse("http://h/x");

        StreamBody? untrailed = ManagedEasyTransfer.TrailerPostFieldsBodyOf(null, options with { Callbacks = new() }, url);
        StreamBody? tool = ManagedEasyTransfer.TrailerPostFieldsBodyOf(new EngineToolTransferInputs(), options, url);
        StreamBody? noFields = ManagedEasyTransfer.TrailerPostFieldsBodyOf(null, options with { Http = new() }, url);

        Assert.IsNull(untrailed);
        Assert.IsNull(tool);
        Assert.IsNull(noFields);
    }

    /// <summary>A POST of <c>xxx=yyy</c> to <c>http://h/x</c> with <paramref name="headers" /> and the trailer callback.</summary>
    private static EasyOptions PostFieldsTrailerOptions(string[] headers, TrailerCallback trailer) =>
        new()
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("xxx=yyy"), HttpHeaders = headers },
            Callbacks = new() { Trailer = trailer },
        };
}
