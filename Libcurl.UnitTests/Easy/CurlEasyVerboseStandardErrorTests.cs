using System.Text;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// Pins libcurl's default verbose output: a handle with <c>CURLOPT_VERBOSE</c> and no <c>CURLOPT_DEBUGFUNCTION</c>
/// writes its lines to <c>CURLOPT_STDERR</c>, through the managed engine and its real HTTP handler over a scripted
/// connection (upstream cases 1506 and 1510, BL-2726).
/// </summary>
[TestClass]
public sealed class CurlEasyVerboseStandardErrorTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi";

    [TestMethod]
    public async Task Perform_VerboseWithStandardErrorAndNoDebugFunction_WritesTheConnectionLeftIntactLine()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        using MemoryStream standardError = new();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/1506");
        easy.SetOption(CurlOption.Verbose, 1L);
        easy.SetOption(CurlOption.Stderr, standardError);
        easy.SetOption(CurlOption.Writedata, Stream.Null);

        CurlCode code = easy.Perform();

        string written = Encoding.ASCII.GetString(standardError.ToArray());
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("* Connection #0 to host 127.0.0.1:8990 left intact\n", written);
        Assert.Contains("> GET /1506 HTTP/1.1\r\n", written);
        Assert.Contains("< HTTP/1.1 200 OK\r\n", written);
        Assert.DoesNotContain("hi", written);
    }

    [TestMethod]
    public async Task Perform_VerboseOffWithStandardError_WritesNothingToStandardError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        using MemoryStream standardError = new();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/1506");
        easy.SetOption(CurlOption.Stderr, standardError);
        easy.SetOption(CurlOption.Writedata, Stream.Null);

        CurlCode code = easy.Perform();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task Perform_VerboseWithNoStandardError_WritesToTheEnginesStandardError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        using MemoryStream engineStandardError = new();
        await using var library = CurlGlobal.Init(EngineOver(connector, engineStandardError), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/1510");
        easy.SetOption(CurlOption.Verbose, 1L);
        easy.SetOption(CurlOption.Writedata, Stream.Null);

        CurlCode code = easy.Perform();

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("* Connection #0 to host 127.0.0.1:8990 left intact\n", Encoding.ASCII.GetString(engineStandardError.ToArray()));
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector, Stream? standardError = null) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null,
            standardError: standardError);
}
