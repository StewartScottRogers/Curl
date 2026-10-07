using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how URL and netrc credentials holding a control character are refused before connecting,
/// as curl 8.21.0's <c>lib/url.c</c> refuses them (<see cref="TransferCredentialLookup" />): exit 3
/// <c>error extracting credentials from URL</c> for a URL user name or password decoding to a byte
/// below 0x20 (only 0x00 for <c>http</c>, <c>https</c>, <c>ws</c> and <c>wss</c>), and exit 26
/// <c>control code detected in .netrc credentials</c> for a netrc entry holding one, except over
/// those four schemes. Measured on 2026-10-03 with curl 8.21.0 (mingw, Schannel), BL-1411.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerCredentialControlCodeTests
{
    private const string UrlCredentialsLines = "* error extracting credentials from URL\ncurl: (3) error extracting credentials from URL";

    private const string NetrcLines = "* control code detected in .netrc credentials\ncurl: (26) control code detected in .netrc credentials";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly InMemoryDataFileReader dataFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("ftp", "ftp://u%01x:p@127.0.0.1:1/f", DisplayName = "ftp user %01")]
    [DataRow("ftp", "ftp://u%1fx:p@127.0.0.1:1/f", DisplayName = "ftp user %1f")]
    [DataRow("imap", "imap://u:p%0a@127.0.0.1:1/", DisplayName = "imap password %0a")]
    [DataRow("http", "http://u%00x:p@127.0.0.1:1/", DisplayName = "http user %00")]
    [DataRow("ws", "ws://u%00x:p@127.0.0.1:1/", DisplayName = "ws user %00")]
    public async Task RunAsync_UrlCredentialsWithARefusedControlCode_FailsWithExit3BeforeConnecting(string scheme, string url)
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath(scheme);

        int exitCode = await RunAsync(["-sSv", url], handler);

        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Assert("stderr", Unix(UrlCredentialsLines + NewLine), Unix(StandardErrorText));
        Diagnostics.Assert("handler contexts", 0, handler.Contexts.Count());
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(UrlCredentialsLines + NewLine, StandardErrorText);
        Assert.IsEmpty(handler.Contexts);
    }

    [TestMethod]
    [DataRow("ftp", "ftp://u%7fx:p@127.0.0.1:1/f", "u\u007fx", DisplayName = "ftp user %7f")]
    [DataRow("ftp", "ftp://u%c3%a9:p@127.0.0.1:1/f", "ué", DisplayName = "ftp user %c3%a9")]
    [DataRow("http", "http://u%01x:p@127.0.0.1:1/", "u\u0001x", DisplayName = "http user %01")]
    [DataRow("ws", "ws://u%01x:p@127.0.0.1:1/", "u\u0001x", DisplayName = "ws user %01")]
    public async Task RunAsync_UrlCredentialsTheSchemeAccepts_AreHandedToTheTransfer(string scheme, string url, string user)
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.WritingPath(scheme);

        int exitCode = await RunAsync(["-sS", url], handler);

        Diagnostics.Assert("exit code", 0, exitCode);
        NetworkCredential? credentials = Assert.ContainsSingle(handler.Contexts).Credentials;
        Diagnostics.Assert("user name", Escape(user), Escape(credentials?.UserName));
        Diagnostics.Assert("password", "p", credentials?.Password ?? "<null>");
        Assert.AreEqual(0, exitCode);
        Assert.IsNotNull(credentials);
        Assert.AreEqual(user, credentials.UserName);
        Assert.AreEqual("p", credentials.Password);
    }

    [TestMethod]
    public async Task RunAsync_UserOptionAndUrlUserWithAControlCode_SendsTheUserOption()
    {
        // A -u with a user name wins, so curl never decodes the URL's credentials.
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sS", "-u", "q:r", "ftp://u%01x:p@127.0.0.1:1/f"], ftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("user name", "q", Escape(Assert.ContainsSingle(ftp.Contexts).Credentials?.UserName));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("q", Assert.ContainsSingle(ftp.Contexts).Credentials?.UserName);
    }

    [TestMethod]
    [DataRow("-n", DisplayName = "required file")]
    [DataRow("--netrc-optional", DisplayName = "optional file")]
    public async Task RunAsync_NetrcPasswordWithAControlCodeOverFtp_FailsWithExit26BeforeConnecting(string netrcOption)
    {
        ArrangeNetrc("machine 127.0.0.1 login u password p\u0001q\n");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sSv", netrcOption, "ftp://127.0.0.1:1/f"], ftp);

        Diagnostics.Assert("exit code", 26, exitCode);
        Diagnostics.Assert("stderr", Unix(NetrcLines + NewLine), Unix(StandardErrorText));
        Diagnostics.Assert("handler contexts", 0, ftp.Contexts.Count());
        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(NetrcLines + NewLine, StandardErrorText);
        Assert.IsEmpty(ftp.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_NetrcLoginWithAControlCodeOverFtp_FailsWithExit26()
    {
        ArrangeNetrc("machine 127.0.0.1 login u\u001fv password p\n");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sS", "-n", "ftp://127.0.0.1:1/f"], ftp);

        Diagnostics.Assert("exit code", 26, exitCode);
        Diagnostics.Assert("handler contexts", 0, ftp.Contexts.Count());
        Assert.AreEqual(26, exitCode, StandardErrorText);
        Assert.IsEmpty(ftp.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_NetrcPasswordWithAControlCodeOverHttp_SendsIt()
    {
        // curl --netrc-file <file> http://127.0.0.1:PORT/a: Authorization: Basic dTpwAXE= (u:p<0x01>q).
        ArrangeNetrc("machine 127.0.0.1 login u password p\u0001q\n");
        ScriptedConnector server = ServeOk();

        int exitCode = await RunAsync(["-sS", "-n", "http://127.0.0.1:1/a"], HttpOver(server));

        Diagnostics.Bytes("request bytes", server.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "request has the Authorization header",
            true,
            Encoding.Latin1.GetString(server.Written).Contains("\r\nAuthorization: Basic dTpwAXE=\r\n", StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.Contains("\r\nAuthorization: Basic dTpwAXE=\r\n", Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_NetrcWithoutAnEntryForTheHostOverFtp_IsNotChecked()
    {
        ArrangeNetrc("machine other login u password p\u0001q\n");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sS", "-n", "ftp://127.0.0.1:1/f"], ftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("credentials", "<null>", Assert.ContainsSingle(ftp.Contexts).Credentials?.ToString() ?? "<null>");
        Assert.AreEqual(0, exitCode);
        Assert.IsNull(Assert.ContainsSingle(ftp.Contexts).Credentials);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToAnFtpUrlWithAControlCodeInItsUser_FailsWithExit3BeforeTheHop()
    {
        ScriptedConnector server = ServeRedirect("ftp://u%01x:p@127.0.0.1:1/f");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sS", "-L", "http://127.0.0.1:1/a"], HttpOver(server), ftp);

        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Assert(
            "stderr",
            "curl: (3) error extracting credentials from URL\n",
            Unix(StandardErrorText));
        Diagnostics.Assert("ftp contexts", 0, ftp.Contexts.Count());
        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) error extracting credentials from URL" + NewLine, StandardErrorText);
        Assert.IsEmpty(ftp.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_RedirectToAnFtpUrlWithAcceptedUser_LogsInAsThatUser()
    {
        ScriptedConnector server = ServeRedirect("ftp://u%7fx:p@127.0.0.1:1/f");
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-sS", "-L", "http://127.0.0.1:1/a"], HttpOver(server), ftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("user name", Escape("u\u007fx"), Escape(Assert.ContainsSingle(ftp.Contexts).Credentials?.UserName));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("u\u007fx", Assert.ContainsSingle(ftp.Contexts).Credentials?.UserName);
    }

    [TestMethod]
    public async Task RunAsync_RedirectWhoseProxyIsRefused_FailsWithTheProxyFailureFirst()
    {
        // The hop's proxy is chosen before its URL credentials are checked, so its failure stands.
        ScriptedConnector server = ServeRedirect("https://u%00x:p@127.0.0.1:1/b");
        ProxySelector proxySelector = new(name => name == "https_proxy" ? "bogus://127.0.0.1:2" : null);
        Diagnostics.Arrange("https_proxy environment variable", "bogus://127.0.0.1:2");

        int exitCode = await RunAsync(["-sS", "-L", "http://127.0.0.1:1/a"], new TransferDispatch(new ProtocolDispatcher([HttpOver(server)]), [], proxySelector: proxySelector));

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert(
            "stderr",
            "curl: (7) Unsupported proxy scheme for 'bogus://127.0.0.1:2'\n",
            Unix(StandardErrorText));
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual("curl: (7) Unsupported proxy scheme for 'bogus://127.0.0.1:2'" + NewLine, StandardErrorText);
    }

    /// <summary>The text with the platform's newline as LF, so what is printed does not depend on the OS.</summary>
    private static string Unix(string text) => text.Replace(NewLine, "\n", StringComparison.Ordinal);

    /// <summary>The text with every control character shown as <c>\xNN</c>.</summary>
    private static string Escape(string? text) =>
        text is null
            ? "<null>"
            : string.Concat(text.Select(character => character < ' ' || character == '\u007f' ? $"\\x{(int)character:x2}" : character.ToString()));

    private void ArrangeNetrc(string content)
    {
        dataFiles.Files["home/.netrc"] = Encoding.UTF8.GetBytes(content);
        Diagnostics.Arrange("netrc file home/.netrc", Escape(content.Replace("\n", "\\n", StringComparison.Ordinal)));
    }

    private ScriptedConnector ServeOk()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length 0");
        return new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
    }

    private ScriptedConnector ServeRedirect(string location)
    {
        Diagnostics.Arrange("scripted response", $"HTTP/1.1 302 Found, Location {location}, Content-Length 0");
        return new(
        [
            Encoding.Latin1.GetBytes($"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\n\r\n"),
        ]);
    }

    private static HttpProtocolHandler HttpOver(ScriptedConnector server) =>
        new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

    private Task<int> RunAsync(IReadOnlyList<string> arguments, params IProtocolHandler[] handlers) =>
        RunAsync(arguments, new TransferDispatch(new ProtocolDispatcher(handlers)));

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, TransferDispatch dispatch)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("HOME environment variable", "home");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => dispatch,
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    outputPaths: fileSystem,
                    configFileReader: dataFiles,
                    readEnvironmentVariable: name => name == "HOME" ? "home" : null)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr, platform newlines as LF", Encoding.UTF8.GetBytes(Unix(StandardErrorText)));
        return exitCode;
    }
}
