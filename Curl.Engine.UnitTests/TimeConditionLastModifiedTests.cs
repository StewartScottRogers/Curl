using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;
using TimeCondition = Curl.Api.Options.Behaviour.TimeCondition;

namespace Curl.Engine;

/// <summary>
/// Pins the condition header each <c>CURLOPT_TIMECONDITION</c> sends with a <c>CURLOPT_TIMEVALUE</c>:
/// <c>If-Modified-Since</c> for 1, <c>If-Unmodified-Since</c> for 2 and <c>Last-Modified</c> for 3
/// (<c>CURL_TIMECOND_LASTMOD</c>), the time in RFC 1123 form in GMT, as libcurl 8.22.0's
/// <c>Curl_add_timecondition</c> does (ADR-0781, BL-3198).
/// </summary>
[TestClass]
public sealed class TimeConditionLastModifiedTests
{
    private const long TimeValue = 784111777;
    private const string Date = "Sun, 06 Nov 1994 08:49:37 GMT";
    private const string EmptyResponse = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_TimeConditionLastModified_SendsLastModifiedAndNoIfModifiedSince()
    {
        string written = await RequestFor(TimeCondition.LastModified);

        StringAssert.Contains(written, $"\r\nLast-Modified: {Date}\r\n");
        Assert.DoesNotContain("If-Modified-Since", written);
        Assert.DoesNotContain("If-Unmodified-Since", written);
    }

    [TestMethod]
    [DataRow(TimeCondition.IfModifiedSince, "If-Modified-Since")]
    [DataRow(TimeCondition.IfUnmodifiedSince, "If-Unmodified-Since")]
    public async Task PerformAsync_TimeConditionIfModifiedOrUnmodifiedSince_SendsThatHeader(TimeCondition condition, string name)
    {
        string written = await RequestFor(condition);

        StringAssert.Contains(written, $"\r\n{name}: {Date}\r\n");
        Assert.DoesNotContain("Last-Modified", written);
    }

    private static async Task<string> RequestFor(TimeCondition condition)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(EmptyResponse)]);
        IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new EasyTransferOptions { TimeCondition = condition, TimeValue = TimeValue },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        await easy.DisposeAsync();
        return Encoding.ASCII.GetString(connector.Written);
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
