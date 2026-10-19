using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Core;
using Curl.Engine;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// Pins lib575's handle lifecycle through the managed engine and a pooling connector over a scripted connection:
/// <c>curl_easy_cleanup</c> of a handle that performed outside a multi handle closes its connection, so a
/// <c>curl_easy_duphandle</c> of it, performed through a multi handle, opens a new one rather than reusing it
/// (upstream case 575, BL-2722).
/// </summary>
[TestClass]
public sealed class CurlEasyDuplicateAfterCleanupTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task DupHandle_PerformedInAMultiHandleAfterTheOriginalIsCleanedUp_OpensANewConnection()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response), Encoding.ASCII.GetBytes(Response)]);
        await using var library = CurlGlobal.Init(PoolingEngineOver(connector), CurlGlobalFlags.Nothing);
        CurlEasy original = library.EasyInit();
        original.SetOption(CurlOption.Url, "http://127.0.0.1:8990/575");
        CurlCode first = await original.PerformAsync();
        CurlEasy duplicate = original.DupHandle();
        await original.DisposeAsync();
        await using CurlMulti multi = library.MultiInit();
        multi.AddHandle(duplicate);

        await PerformUntilDoneAsync(multi);

        CurlMessage? done = multi.InfoRead(out _);
        await duplicate.DisposeAsync();
        Assert.AreEqual(CurlCode.Ok, first);
        Assert.AreEqual(CurlCode.Ok, done?.Result);
        Assert.HasCount(2, connector.Targets);
    }

    private static async Task PerformUntilDoneAsync(CurlMulti multi)
    {
        (_, int running) = await multi.PerformAsync();
        while (running > 0)
        {
            _ = await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, 1000);
            (_, running) = await multi.PerformAsync();
        }
    }

    private static ManagedCurlEngine PoolingEngineOver(ScriptedConnector connector) =>
        new(
            TimeProvider.System,
            (_, shared, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                new PoolingConnector(connector, shared.Connections, configuration: null),
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Could not connect to server"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
