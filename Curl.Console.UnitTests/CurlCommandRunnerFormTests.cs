using System.Text;

using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>-F</c> / <c>--form</c> end to end through the production HTTP handler over a
/// <see cref="ScriptedConnector" />, with an injected boundary and an in-memory file. The
/// expected request was measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel) as
/// <c>curl --no-progress-meter -u u:p -F a=b -F f=@&lt;dir&gt;\srv.py http://127.0.0.1:18233/</c>,
/// <c>srv.py</c> holding <c>print(1)</c> LF, through <c>Record-CurlExchange.ps1</c> (BL-233 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerFormTests
{
    private const string Url = "http://127.0.0.1:18233/";

    private const string Boundary = "------------------------26sLGDDRgOYwegSEyKubav";

    private const string MeasuredRequest =
        "POST / HTTP/1.1\r\n"
        + "Host: 127.0.0.1:18233\r\n"
        + "Authorization: Basic dTpw\r\n"
        + "User-Agent: curl/8.21.0\r\n"
        + "Accept: */*\r\n"
        + "Content-Length: 313\r\n"
        + "Content-Type: multipart/form-data; boundary=" + Boundary + "\r\n"
        + "\r\n"
        + "--" + Boundary + "\r\n"
        + "Content-Disposition: form-data; name=\"a\"\r\n"
        + "\r\n"
        + "b\r\n"
        + "--" + Boundary + "\r\n"
        + "Content-Disposition: form-data; name=\"f\"; filename=\"srv.py\"\r\n"
        + "Content-Type: application/octet-stream\r\n"
        + "\r\n"
        + "print(1)\n\r\n"
        + "--" + Boundary + "--\r\n";

    private const string RedirectBoundary = "------------------------ugfPuRE0XGGB3ODF3hLOBy";

    private const string MeasuredRedirectBody =
        "--" + RedirectBoundary + "\r\n"
        + "Content-Disposition: form-data; name=\"a\"\r\n"
        + "\r\n"
        + "b\r\n"
        + "--" + RedirectBoundary + "\r\n"
        + "Content-Disposition: form-data; name=\"f\"; filename=\"file.txt\"\r\n"
        + "Content-Type: text/plain\r\n"
        + "\r\n"
        + "hello\r\n"
        + "--" + RedirectBoundary + "--\r\n";

    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);

    public CurlCommandRunnerFormTests() =>
        files.ExistingContent["srv.py"] = Encoding.ASCII.GetBytes("print(1)\n");

    [TestMethod]
    public async Task RunAsync_FormTextAndFileUpload_SendsTheMeasuredMultipartRequest()
    {
        int exitCode = await RunAsync("-sS", "-u", "u:p", "-F", "a=b", "-F", "f=@srv.py", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredRequest, Encoding.Latin1.GetString(server.Written));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_FormWithOutputFile_SendsTheMultipartRequestAndWritesTheFile()
    {
        int exitCode = await RunAsync("-sS", "-u", "u:p", "-F", "a=b", "-F", "f=@srv.py", "-o", "out.txt", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(MeasuredRequest, Encoding.Latin1.GetString(server.Written));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(files.Written["out.txt"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting()
    {
        files.UnreadablePaths.Add("nope.txt");

        int exitCode = await RunAsync("-F", "a=b", "-F", "f=@nope.txt", Url);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(
            $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}{Environment.NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(server.Targets);
    }

    [TestMethod]
    public async Task RunAsync_FormFile_ClosesTheFileAfterTheTransfer()
    {
        TrackingFileSystem tracking = new();

        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused")))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                formBodyBuilder: new MultipartFormBodyBuilder(tracking, Encoding.UTF8, () => Boundary))
            .RunAsync(["-sS", "-F", "f=<x", Url]);

        Assert.IsFalse(tracking.Opened.Single().CanRead);
    }

    /// <summary>
    /// Measured on 2026-09-27 with curl 8.21.0 (mingw, Schannel) as
    /// <c>curl -L --max-redirs 1 -F a=b -F f=@&lt;dir&gt;\file.txt http://127.0.0.1:18298/first</c>,
    /// <c>file.txt</c> holding <c>hello</c>, answered 307 or 308 with <c>Location: /next</c>
    /// (BL-298 Notes): both POSTs carry the same body, boundary and all.
    /// </summary>
    [TestMethod]
    [DataRow(307)]
    [DataRow(308)]
    public async Task RunAsync_FormFollowed307Or308_SendsTheMeasuredMultipartBodyOnBothRequests(int status)
    {
        files.ExistingContent["file.txt"] = Encoding.ASCII.GetBytes("hello");
        ScriptedConnector redirectingServer = new(
        [
            Encoding.Latin1.GetBytes($"HTTP/1.1 {status} Moved\r\nLocation: /next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"),
            Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello"),
        ]);

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(redirectingServer, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused")))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                formBodyBuilder: new MultipartFormBodyBuilder(files, Encoding.UTF8, () => RedirectBoundary))
            .RunAsync(["-sS", "-L", "-F", "a=b", "-F", "f=@file.txt", "http://127.0.0.1:18298/first"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            MeasuredRedirectRequest("/first") + MeasuredRedirectRequest("/next"),
            Encoding.Latin1.GetString(redirectingServer.Written));
        Assert.AreEqual("hello", Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    /// <summary>
    /// The part curl 8.21.0 sent on 2026-09-26 for <c>-F "t=hi;encoder=base64"</c>
    /// (<c>MultipartFormBodyBuilderEncoderTests</c>, 187 bytes), under this class's boundary.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_FormBase64Encoder_SendsTheBase64EncodedPart()
    {
        int exitCode = await RunAsync("-sS", "-F", "t=hi;encoder=base64", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "POST / HTTP/1.1\r\n"
            + "Host: 127.0.0.1:18233\r\n"
            + "User-Agent: curl/8.21.0\r\n"
            + "Accept: */*\r\n"
            + "Content-Length: 187\r\n"
            + "Content-Type: multipart/form-data; boundary=" + Boundary + "\r\n"
            + "\r\n"
            + "--" + Boundary + "\r\n"
            + "Content-Disposition: form-data; name=\"t\"\r\n"
            + "Content-Transfer-Encoding: base64\r\n"
            + "\r\n"
            + "aGk=\r\n"
            + "--" + Boundary + "--\r\n",
            Encoding.Latin1.GetString(server.Written));
    }

    /// <summary>Measured on 2026-09-27 with curl 8.21.0 (mingw, Schannel): <c>curl -sS -F "t=hi;encoder=bogus" http://127.0.0.1:1/</c>.</summary>
    [TestMethod]
    public async Task RunAsync_FormUnknownEncoder_Exits43WithoutConnecting()
    {
        int exitCode = await RunAsync("-sS", "-F", "t=hi;encoder=bogus", Url);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual(
            $"curl: (43) A libcurl function was given a bad argument{Environment.NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsEmpty(server.Targets);
    }

    private static string MeasuredRedirectRequest(string path) =>
        $"POST {path} HTTP/1.1\r\n"
        + "Host: 127.0.0.1:18298\r\n"
        + "User-Agent: curl/8.21.0\r\n"
        + "Accept: */*\r\n"
        + "Content-Length: 297\r\n"
        + "Content-Type: multipart/form-data; boundary=" + RedirectBoundary + "\r\n"
        + "\r\n"
        + MeasuredRedirectBody;

    private Task<int> RunAsync(params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused")))),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                formBodyBuilder: new MultipartFormBodyBuilder(files, Encoding.UTF8, () => Boundary))
            .RunAsync(arguments);

    /// <summary>Opens every path as <c>xy</c> and keeps each stream it opened.</summary>
    private sealed class TrackingFileSystem : IFileSystem
    {
        public List<Stream> Opened { get; } = [];

        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken)
        {
            MemoryStream stream = new("xy"u8.ToArray(), writable: false);
            Opened.Add(stream);

            return ValueTask.FromResult(FileOpenResult.Opened(stream, stream.Length, null));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(
            string path,
            FileWriteMode mode,
            UnixFileMode createMode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
