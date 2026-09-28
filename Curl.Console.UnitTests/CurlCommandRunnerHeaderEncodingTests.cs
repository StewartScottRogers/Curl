using System.Text;

using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    [DataRow(false, DisplayName = "Linux and macOS: UTF-8")]
    [DataRow(true, DisplayName = "Windows: the ANSI code page")]
    public async Task RunAsync_NonAsciiHeader_SendsItInThePlatformEncoding(bool runsOnWindows)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        byte[] expected = [.. "X-A: "u8, .. CredentialEncoding.ForPlatform(runsOnWindows).GetBytes("é€"), .. "\r\n"u8];

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows)
            .RunAsync(["-sS", "-H", "X-A: é€", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.Contains(Encoding.Latin1.GetString(expected), Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_NonAsciiHeaderOffWindows_SendsUtf8()
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);

        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(
                    CurlComposition.CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver()))),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(["-sS", "-H", "X-A: é", Url]);

        Assert.Contains("X-A: Ã©\r\n", Encoding.Latin1.GetString(server.Written));
    }
}
