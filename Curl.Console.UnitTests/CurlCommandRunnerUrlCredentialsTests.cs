using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how a URL's user information becomes the transfer's credentials when <c>-u</c> gives no user
/// name and no netrc option is in effect (<see cref="TransferCredentialLookup" />). Every expectation
/// was measured on 2026-09-28 with curl 8.21.0 (mingw, Schannel) through
/// <c>Record-CurlExchange.ps1</c> against 127.0.0.1:47911, and <c>-Ftp</c> on 47912 (BL-791 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUrlCredentialsTests
{
    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://b:x@127.0.0.1:47911/", "Yjp4", DisplayName = "b:x sends b:x")]
    [DataRow("http://zz@127.0.0.1:47911/", "eno6", DisplayName = "zz sends zz:")]
    [DataRow("http://:x@127.0.0.1:47911/", "Ong=", DisplayName = ":x sends :x")]
    [DataRow("http://a%3Ab@127.0.0.1:47911/", "YTpiOg==", DisplayName = "a%3Ab sends a:b:")]
    public async Task RunAsync_UrlWithUserInformation_SendsItAsBasicAuthorization(string url, string encoded)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        Diagnostics.Bytes("server reply", Encoding.Latin1.GetBytes(Ok));

        int exitCode = await RunAsync(["-s", "-S", url], HttpOver(server));

        Diagnostics.Bytes("request written", server.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "request",
            Encoding.Latin1.GetBytes(
                $"GET / HTTP/1.1\r\nHost: 127.0.0.1:47911\r\nAuthorization: Basic {encoded}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"),
            server.Written);
        Assert.AreEqual(
            $"GET / HTTP/1.1\r\nHost: 127.0.0.1:47911\r\nAuthorization: Basic {encoded}\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    [DataRow("http://@127.0.0.1:47911/", DisplayName = "@ sends none")]
    [DataRow("http://:@127.0.0.1:47911/", DisplayName = ":@ sends none")]
    public async Task RunAsync_UrlWithEmptyUserInformation_SendsNoAuthorization(string url)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        Diagnostics.Bytes("server reply", Encoding.Latin1.GetBytes(Ok));

        int exitCode = await RunAsync(["-s", "-S", url], HttpOver(server));

        Diagnostics.Bytes("request written", server.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "request",
            Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1:47911\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"),
            server.Written);
        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: 127.0.0.1:47911\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    [DataRow("q:r", "cTpy", DisplayName = "-u q:r beats the URL's b:x")]
    [DataRow(":pw", "Yjp4", DisplayName = "-u :pw gives way to the URL's b:x")]
    [DataRow(":", "Yjp4", DisplayName = "-u : gives way to the URL's b:x")]
    public async Task RunAsync_UserOptionAndUrlUserInformation_SendsTheOneCurlSends(string user, string encoded)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        Diagnostics.Bytes("server reply", Encoding.Latin1.GetBytes(Ok));

        int exitCode = await RunAsync(["-s", "-S", "-u", user, "http://b:x@127.0.0.1:47911/"], HttpOver(server));

        Diagnostics.Bytes("request written", server.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert(
            $"request contains Authorization: Basic {encoded}",
            true,
            Encoding.Latin1.GetString(server.Written).Contains($"\r\nAuthorization: Basic {encoded}\r\n", StringComparison.Ordinal));
        Assert.Contains($"\r\nAuthorization: Basic {encoded}\r\n", Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    [DataRow("ftp://u:p@127.0.0.1:47912/f", "u", "p", DisplayName = "USER u, PASS p")]
    [DataRow("ftp://u%40x@127.0.0.1:47912/f", "u@x", "", DisplayName = "USER u@x, PASS (empty)")]
    public async Task RunAsync_FtpUrlWithUserInformation_LogsInAsItsUser(string url, string user, string password)
    {
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-s", "-S", url], ftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler call count", 1, ftp.Contexts.Count);
        NetworkCredential? credentials = Assert.ContainsSingle(ftp.Contexts).Credentials;
        Diagnostics.Assert("credentials present", true, credentials is not null);
        Assert.IsNotNull(credentials);
        Diagnostics.Assert("user name", user, credentials.UserName);
        Assert.AreEqual(user, credentials.UserName);
        Diagnostics.Assert("password", password, credentials.Password);
        Assert.AreEqual(password, credentials.Password);
    }

    [TestMethod]
    public async Task RunAsync_MqttUrlWithUserInformation_HandsItsUserNameAndPasswordToTheMqttTransfer()
    {
        // FR-042: mqtt://al:pw@host/t sends CONNECT with the user name and password, as -u al:pw does.
        RecordingProtocolHandler mqtt = RecordingProtocolHandler.WritingPath("mqtt");

        int exitCode = await RunAsync(["-s", "-S", "mqtt://al:pw@127.0.0.1:1883/t"], mqtt);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler call count", 1, mqtt.Contexts.Count);
        NetworkCredential? credentials = Assert.ContainsSingle(mqtt.Contexts).Credentials;
        Diagnostics.Assert("credentials present", true, credentials is not null);
        Assert.IsNotNull(credentials);
        Diagnostics.Assert("user name", "al", credentials.UserName);
        Assert.AreEqual("al", credentials.UserName);
        Diagnostics.Assert("password", "pw", credentials.Password);
        Assert.AreEqual("pw", credentials.Password);
    }

    [TestMethod]
    public async Task RunAsync_FtpUrlWithoutUserInformation_CarriesNoCredentials()
    {
        // curl logs in as anonymous; FtpSession supplies that when the context carries no credentials.
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-s", "-S", "ftp://127.0.0.1:47912/f"], ftp);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("handler call count", 1, ftp.Contexts.Count);
        Diagnostics.Assert("credentials are null", true, ftp.Contexts.Count == 1 && ftp.Contexts[0].Credentials is null);
        Assert.IsNull(Assert.ContainsSingle(ftp.Contexts).Credentials);
    }

    private static HttpProtocolHandler HttpOver(ScriptedConnector server) =>
        new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler", $"{handler.GetType().Name} for {string.Join('/', handler.SupportedSchemes)}");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    outputPaths: fileSystem)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(Encoding.UTF8.GetString(standardError.ToArray())));
        return exitCode;
    }
}
