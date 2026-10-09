using System.Text;

using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the runner sends <c>-H</c> text in the encoding
/// <see cref="CredentialEncoding.ForPlatform" /> gives for the platform it runs on (ADR-0067),
/// through the production HTTP handler over a <see cref="ScriptedConnector" />.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHeaderEncodingTests
{
    private const string Url = "http://127.0.0.1:18233/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false, DisplayName = "Linux and macOS: UTF-8")]
    [DataRow(true, DisplayName = "Windows: the ANSI code page")]
    public async Task RunAsync_NonAsciiHeader_SendsItInThePlatformEncoding(bool runsOnWindows)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        byte[] expected = [.. "X-A: "u8, .. CredentialEncoding.ForPlatform(runsOnWindows).GetBytes("é€"), .. "\r\n"u8];

        Diagnostics.Arrange("command line", "curl -sS -H \"X-A: é€\" " + Url);
        Diagnostics.Arrange("runs on Windows", runsOnWindows);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher(
                        CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                    new InMemoryFileSystem(),
                    new InMemoryFileSystem(),
                    new MemoryStream(),
                    new MemoryStream(),
                    new MemoryStream(),
                    runsOnWindows)
                .RunAsync(["-sS", "-H", "X-A: é€", Url]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("request", server.Written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("request contains the header in the platform encoding", true, Encoding.Latin1.GetString(server.Written).Contains(Encoding.Latin1.GetString(expected), StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.Contains(Encoding.Latin1.GetString(expected), Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_ConfigFileHeaderAndUserAgentOnWindows_SendsTheFileUtf8Bytes()
    {
        // Measured with real curl 8.21.0 (Windows, Schannel, 2026-10-08): a -K file's values go out as the
        // file's own bytes, E2 80 9C ... E2 80 9D, not the ANSI code page's 93 ... 94 (upstream test 470, BL-1848).
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        InMemoryDataFileReader configFiles = new();
        configFiles.Files["k.txt"] = Encoding.UTF8.GetBytes("-H \"X-A: “quoted”\"\nuser-agent = “agent”\n");
        MemoryStream standardError = new();

        Diagnostics.Arrange("command line", "curl -sS -K k.txt " + Url);
        Diagnostics.Bytes("config file k.txt", configFiles.Files["k.txt"]);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher(
                        CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                    new InMemoryFileSystem(),
                    new InMemoryFileSystem(),
                    new MemoryStream(),
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    configFileReader: configFiles)
                .RunAsync(["-K", "k.txt", Url]);
        }

        string request = Encoding.Latin1.GetString(server.Written);
        string header = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("X-A: “quoted”\r\n"));
        string userAgent = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("User-Agent: “agent”\r\n"));
        string warning = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("request", server.Written);
        Diagnostics.Act("standard error", warning);
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("request contains the header's UTF-8 bytes", true, request.Contains(header, StringComparison.Ordinal));
        Diagnostics.Assert("request contains the user agent's UTF-8 bytes", true, request.Contains(userAgent, StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.Contains(header, request);
        Assert.Contains(userAgent, request);
        Assert.Contains("Warning: The argument '“agent”' starts with a Unicode character.", warning);
    }

    [TestMethod]
    public async Task RunAsync_NonAsciiHeaderOffWindows_SendsUtf8()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);

        Diagnostics.Arrange("command line", "curl -sS -H \"X-A: é\" " + Url);
        Diagnostics.Arrange("runs on Windows", false);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher(
                        CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                    new InMemoryFileSystem(),
                    new InMemoryFileSystem(),
                    new MemoryStream(),
                    new MemoryStream(),
                    new MemoryStream(),
                    runsOnWindows: false)
                .RunAsync(["-sS", "-H", "X-A: é", Url]);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("request", server.Written);
        Diagnostics.Assert("request contains the UTF-8 bytes read as Latin-1", true, Encoding.Latin1.GetString(server.Written).Contains("X-A: Ã©\r\n", StringComparison.Ordinal));
        Assert.Contains("X-A: Ã©\r\n", Encoding.Latin1.GetString(server.Written));
    }
}
