using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// Pins libcurl's built-in progress meter: a handle with <c>CURLOPT_NOPROGRESS</c> 0 draws it on <c>CURLOPT_STDERR</c>
/// with no progress callback or one returning <c>CURL_PROGRESSFUNC_CONTINUE</c>, and not with one returning 0 (BL-4012).
/// </summary>
[TestClass]
public sealed class CurlEasyProgressMeterTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi";

    private const string FirstHeaderStart = "  % Total    % Received % Xferd  Average Speed";

    [TestMethod]
    public async Task Perform_NoProgressOffAndNoCallback_DrawsTheMeterHeaderAndAFinalStatusLine()
    {
        string written = await PerformWithProgressAsync(null);

        Assert.StartsWith(FirstHeaderStart, written);
        Assert.Contains("Dload  Upload", written);
        Assert.Contains("\r100 ", written);
        Assert.EndsWith(Environment.NewLine, written);
    }

    [TestMethod]
    public async Task Perform_TransferInfoCallbackReturnsContinue_DrawsTheMeter()
    {
        string written = await PerformWithProgressAsync((_, _, _, _) => ProgressCallbackResult.Continue);

        Assert.StartsWith(FirstHeaderStart, written);
        Assert.Contains("\r100 ", written);
    }

    [TestMethod]
    public async Task Perform_TransferInfoCallbackReturnsZero_WritesNothing()
    {
        string written = await PerformWithProgressAsync((_, _, _, _) => 0);

        Assert.AreEqual(string.Empty, written);
    }

    private static async Task<string> PerformWithProgressAsync(TransferInfoCallback? transferInfo)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using var library = CurlGlobal.Init(EngineOver(connector), CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        using MemoryStream standardError = new();
        easy.SetOption(CurlOption.Url, "http://127.0.0.1:8990/4012");
        easy.SetOption(CurlOption.Noprogress, 0L);
        easy.SetOption(CurlOption.Stderr, standardError);
        easy.SetOption(CurlOption.Writedata, Stream.Null);
        if (transferInfo is not null)
        {
            easy.SetOption(CurlOption.Xferinfofunction, transferInfo);
        }

        CurlCode code = easy.Perform();

        Assert.AreEqual(CurlCode.Ok, code);
        return Encoding.UTF8.GetString(standardError.ToArray());
    }

    private static ManagedCurlEngine EngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
