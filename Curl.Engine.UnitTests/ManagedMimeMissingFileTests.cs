using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;

namespace Curl.Engine;

// A curl_mime_filedata part whose file is missing, through a whole CURLOPT_MIMEPOST transfer: libcurl 8.22.0 sends the
// part empty and the transfer ends with CURLE_OK, as recorded in BL-3210's case 1 (BL-3211).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    public async Task PerformAsync_MimePostOfAMissingFile_SendsThePartEmptyAndSucceedsAsLibcurlDoes()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        ManagedCurlEngine engine = CreateEngine(connector);
        IMimeBuilder mime = engine.CreateMime();
        IMimePartBuilder missing = mime.AddPart();
        missing.SetName("a");
        CurlCode setCode = missing.SetFileData(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope.txt"));
        IMimePartBuilder data = mime.AddPart();
        data.SetName("b");
        data.SetData(Encoding.UTF8.GetBytes("hello"));
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();

        CurlCode code = await PerformMimePostAsync(easy, "http://h/form", mime.Build());

        string body =
            $"--{FormBoundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n\r\n"
            + $"--{FormBoundary}\r\nContent-Disposition: form-data; name=\"b\"\r\n\r\nhello\r\n--{FormBoundary}--\r\n";
        string sent = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.ReadError, setCode);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsTrue(sent.StartsWith("POST /form HTTP/1.1\r\n", StringComparison.Ordinal));
        Assert.Contains($"Content-Length: {body.Length}\r\n", sent);
        Assert.EndsWith("\r\n\r\n" + body, sent);
    }
}
