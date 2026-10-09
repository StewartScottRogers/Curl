using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>ws://</c> and <c>wss://</c> end to end through the production composition over fake
/// connectors: the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers, the
/// HTTP authenticator it composes, the runner's <c>-T</c>, <c>-D</c> and <c>-i</c> handling and the
/// <c>-V</c> protocol list. Every exchange was recorded from curl 8.21.0 (mingw, Schannel) with
/// <c>Record-CurlExchange.ps1</c> (ADR-0128's table, BL-582 and BL-583 Notes). The random
/// <c>Sec-WebSocket-Key</c> and frame mask are the only bytes not compared exactly: the key is
/// checked to be 16 bytes of base64 and the mask is used to unmask the frame.
/// </summary>
[TestClass]
public sealed class CurlCompositionWsTests
{
    private const string SwitchingHead =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    /// <summary>A text frame <c>hello</c>, then a close frame with status 1000.</summary>
    private const string HelloAndClose = "\x81\x05hello\x88\x02\x03\xe8";

    private const string SizesFormat = "%{http_code} %{size_download} %{size_header} %{size_request}";

    private static readonly Encoding Latin1 = Encoding.Latin1;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("ws", false)]
    [DataRow("wss", true)]
    public async Task CreateRunner_UserAndHeader_SendsCurlsUpgradeRequestAndWritesTheFramePayloads(string scheme, bool useTls)
    {
        ScriptedConnector connector = new([Latin1.GetBytes(SwitchingHead + HelloAndClose)]);
        Diagnostics.Arrange("use tls", useTls);
        Diagnostics.Bytes("scripted response", Latin1.GetBytes(SwitchingHead + HelloAndClose));

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, ["-u", "user:pw", "-H", "X-Test: 1", "-w", SizesFormat, $"{scheme}://127.0.0.1:47912/p"]);

        Diagnostics.Assert("standard output", "hello\x03\xe8" + "101 11 102 239", standardOutput);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(
            "GET /p HTTP/1.1\r\nHost: 127.0.0.1:47912\r\nAuthorization: Basic dXNlcjpwdw==\r\nUser-Agent: curl/8.21.0\r\n"
                + "Accept: */*\r\nUpgrade: websocket\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: <key>\r\n"
                + "X-Test: 1\r\nConnection: Upgrade\r\n\r\n",
            WithKeyHidden(connector.Written));
        Assert.AreEqual(("127.0.0.1", 47912, useTls), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        Assert.AreEqual("hello\x03\xe8" + "101 11 102 239", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_IncludeHeaders_WritesOnlyThePayloads()
    {
        ScriptedConnector connector = new([Latin1.GetBytes(SwitchingHead + HelloAndClose)]);
        Diagnostics.Bytes("scripted response", Latin1.GetBytes(SwitchingHead + HelloAndClose));

        (int exitCode, string standardOutput, _) = await RunAsync(connector, ["-i", "ws://127.0.0.1:47901/chat"]);

        Diagnostics.Assert("standard output", "hello\x03\xe8", standardOutput);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual("hello\x03\xe8", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_DumpHeaderToStandardOutput_WritesTheHeadThenThePayloads()
    {
        ScriptedConnector connector = new([Latin1.GetBytes(SwitchingHead + "\x81\x05hello\x88\x00")]);
        Diagnostics.Bytes("scripted response", Latin1.GetBytes(SwitchingHead + "\x81\x05hello\x88\x00"));

        (int exitCode, string standardOutput, _) = await RunAsync(connector, ["-D", "-", "ws://127.0.0.1:47901/"]);

        Diagnostics.Diff("standard output", SwitchingHead + "hello", standardOutput);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(SwitchingHead + "hello", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_UpgradeRefused_PrintsRefusedAndReturns22()
    {
        ScriptedConnector connector = new([Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok")]);
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");

        (int exitCode, string standardOutput, string standardError) = await RunAsync(connector, ["ws://127.0.0.1:47901/"]);

        Diagnostics.Assert("standard error", "curl: (22) Refused WebSocket upgrade: 200\n", Unix(standardError));
        Diagnostics.Assert("exit code", 22, exitCode);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual("curl: (22) Refused WebSocket upgrade: 200" + Environment.NewLine, standardError);
        Assert.AreEqual(22, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_ServerClosesAfterTheUpgrade_PrintsEmptyReplyAndReturns52()
    {
        ScriptedConnector connector = new([Latin1.GetBytes(SwitchingHead)]);
        Diagnostics.Arrange("scripted response", SwitchingHead);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(
            connector, ["-w", "%{http_code} %{size_download}", "ws://127.0.0.1:47901/"]);

        Diagnostics.Assert("standard output", "101 0", standardOutput);
        Diagnostics.Assert("standard error", "curl: (52) Empty reply from server\n", Unix(standardError));
        Diagnostics.Assert("exit code", 52, exitCode);
        Assert.AreEqual("101 0", standardOutput);
        Assert.AreEqual("curl: (52) Empty reply from server" + Environment.NewLine, standardError);
        Assert.AreEqual(52, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_UploadFile_SendsItAsOneMaskedBinaryFrame()
    {
        ScriptedConnector connector = new([Latin1.GetBytes(SwitchingHead + "\x81\x02ok")]);
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl583-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string upload = Path.Combine(directory, "abc.bin");
        await System.IO.File.WriteAllBytesAsync(upload, "abc"u8.ToArray());
        Diagnostics.Arrange("upload file", "<temp>/abc.bin holding \"abc\"");
        Diagnostics.Bytes("scripted response", Latin1.GetBytes(SwitchingHead + "\x81\x02ok"));

        (int exitCode, string standardOutput, string standardError) result;
        try
        {
            result = await RunAsync(connector, ["-T", upload, "-w", "%{size_upload} %{size_download}", "ws://127.0.0.1:47901/"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        byte[] written = connector.Written;
        int frameStart = Latin1.GetString(written).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        byte[] frame = written[frameStart..];
        Diagnostics.Bytes("upload frame", frame);
        Diagnostics.Assert("upload frame length", 9, frame.Length);
        Diagnostics.Assert("standard output", "ok9 4", result.standardOutput);
        Diagnostics.Assert("exit code", 0, result.exitCode);
        Assert.AreEqual(9, frame.Length);
        Assert.AreEqual((0x82, 0x83), (frame[0], frame[1]));
        byte[] payload = [.. frame[6..].Select((masked, index) => (byte)(masked ^ frame[2 + index]))];
        Assert.AreEqual("abc", Latin1.GetString(payload));
        Assert.AreEqual("ok9 4", result.standardOutput);
        Assert.AreEqual(string.Empty, result.standardError);
        Assert.AreEqual(0, result.exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_Version_ListsWsAndWss()
    {
        ScriptedConnector connector = new([]);

        (int exitCode, string standardOutput, _) = await RunAsync(connector, ["-V"]);

        string protocols = standardOutput.Split(Environment.NewLine).Single(line => line.StartsWith("Protocols:", StringComparison.Ordinal));
        Diagnostics.Act("protocols line", protocols);
        Diagnostics.Assert("protocols line", CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())[2], protocols);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())[2], protocols);
        StringAssert.EndsWith(protocols, " telnet tftp ws wss");
        Assert.AreEqual(0, exitCode);
    }

    /// <summary>
    /// The request as text with its <c>Sec-WebSocket-Key</c> value replaced by <c>&lt;key&gt;</c>,
    /// after checking the value is the base64 of 16 bytes.
    /// </summary>
    private static string WithKeyHidden(byte[] written)
    {
        string text = Latin1.GetString(written);
        Match key = Regex.Match(text, "Sec-WebSocket-Key: ([A-Za-z0-9+/]{22}==)\r\n");
        Assert.IsTrue(key.Success, text);
        Assert.HasCount(16, Convert.FromBase64String(key.Groups[1].Value));
        return text.Replace(key.Groups[1].Value, "<key>", StringComparison.Ordinal);
    }

    private static string Unix(string text) => text.Replace("\r", string.Empty, StringComparison.Ordinal);

    private async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        ScriptedConnector connector, string[] arguments)
    {
        Diagnostics.Arrange(
            "command line arguments",
            string.Join(" ", ["-sS", .. arguments]).Replace(Path.GetTempPath(), "<temp>/", StringComparison.Ordinal).Replace('\\', '/'));
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sS", .. arguments]);

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output", Unix(Latin1.GetString(standardOutput.ToArray())));
        Diagnostics.Act("standard error", Unix(Encoding.UTF8.GetString(standardError.ToArray())));
        Diagnostics.Bytes("request written", connector.Written);
        return (exitCode, Latin1.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
