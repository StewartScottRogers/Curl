using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins the request bytes a <c>CURLOPT_POSTFIELDS</c> post of <c>abcdef</c> sends for each
/// <c>CURLOPT_POSTFIELDSIZE</c>: that many bytes and that <c>Content-Length</c>, the whole data for -1 and for a
/// size past it (ADR-0773), and <c>CURLINFO_SIZE_UPLOAD_T</c> the body bytes sent (BL-3190). Libcurl's
/// <c>CURLOPT_POSTFIELDSIZE</c> and <c>CURLOPT_POSTFIELDSIZE_LARGE</c> both set <see cref="EasyHttpOptions.PostFieldSize" />.
/// </summary>
[TestClass]
public sealed class PostFieldSizeWireTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    [DataRow(0L, "")]
    [DataRow(3L, "abc")]
    [DataRow(6L, "abcdef")]
    [DataRow(10L, "abcdef")]
    [DataRow(-1L, "abcdef")]
    public async Task PerformAsync_PostFieldsWithPostFieldSize_SendsThatManyBytesAndThatContentLength(long size, string body)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = EngineOver(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Http = new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("abcdef"), PostFieldSize = size, Post = true },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.Contains(written, $"\r\nContent-Length: {body.Length}\r\n");
        Assert.AreEqual(body, written[(written.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]);
        Assert.AreEqual(body.Length, easy.Info.SizeUpload);
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
