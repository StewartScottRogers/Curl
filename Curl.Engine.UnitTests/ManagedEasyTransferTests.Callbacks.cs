using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// The write, read and header callbacks' magic return values through a whole transfer (BL-2415, ADR-0573),
// and the seek callback rewinding a read-callback upload a redirect sends again (BL-2417).
public sealed partial class ManagedEasyTransferTests
{
    private const string TemporaryRedirect = "HTTP/1.1 307 Temporary Redirect\r\nLocation: http://h/again\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_WriteCallbackPausesThenIsUnpaused_GetsTheSameBodyBytesAgainAndSucceeds()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        List<string> calls = [];
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new()
            {
                Write = data =>
                {
                    calls.Add(Encoding.ASCII.GetString(data));
                    return calls.Count == 1 ? WriteCallbackResult.Pause : data.Length;
                },
            },
        };
        Task unpausing = UnpauseOnceAsync(easy, CurlPause.Receive);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        await unpausing;

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "hello", "hello" }, calls);
        Assert.AreEqual(CurlPause.Continue, easy.PausedDirections);
    }

    [TestMethod]
    public async Task PerformAsync_HeaderCallbackPausesThenIsUnpaused_GetsEveryHeaderLine()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        List<string> headers = [];
        bool paused = false;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new()
            {
                Header = header =>
                {
                    if (!paused)
                    {
                        paused = true;
                        return WriteCallbackResult.Pause;
                    }

                    headers.Add(Encoding.ASCII.GetString(header));
                    return header.Length;
                },
            },
        };
        Task unpausing = UnpauseOnceAsync(easy, CurlPause.Receive);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        await unpausing;

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Type: text/plain\r\n\r\n", string.Concat(headers));
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackReturnsWriteFuncError_ReturnsWriteError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = _ => WriteCallbackResult.Error },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackPausesOnAFileUrl_ReturnsWriteErrorWithoutWaiting()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl2415-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "hello");
        try
        {
            await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([])).CreateEasy();
            easy.Options = new EasyOptions
            {
                Network = new EasyNetworkOptions { Url = new Uri(path).AbsoluteUri },
                Callbacks = new() { Write = _ => WriteCallbackResult.Pause },
            };

            CurlCode code = await easy.PerformAsync(CancellationToken.None);

            Assert.AreEqual(CurlCode.WriteError, code);
            Assert.AreEqual(CurlPause.Continue, ((ManagedEasyTransfer)easy).PausedDirections);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackPausesThenIsUnpaused_UploadsEveryByte()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Continue + Response)]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        ReadCallback source = ReadOf("abc");
        bool paused = false;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true },
            Callbacks = new()
            {
                Read = buffer =>
                {
                    if (!paused)
                    {
                        paused = true;
                        return ReadCallbackResult.Pause;
                    }

                    return source(buffer);
                },
            },
        };
        Task unpausing = UnpauseOnceAsync(easy, CurlPause.Send);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        await unpausing;

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "\r\n\r\n3\r\nabc\r\n0\r\n\r\n");
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackAborts_ReturnsAbortedByCallbackWithLibcurlsText()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Continue + Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/up" },
            Transfer = new EasyTransferOptions { Upload = true },
            Callbacks = new() { Read = _ => ReadCallbackResult.Abort },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(CallbackReadStream.AbortedText, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_RedirectResendsAReadCallbackUploadAndSeekSucceeds_RewindsOnceAndSendsTheBodyTwice()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Continue + TemporaryRedirect), Encoding.ASCII.GetBytes(Continue + Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        MemoryStream source = new(Encoding.ASCII.GetBytes("abc"));
        List<(long Offset, SeekOrigin Origin)> seeks = [];
        easy.Options = SeekingUploadOptions(
            buffer => source.Read(buffer),
            (offset, origin) =>
            {
                seeks.Add((offset, origin));
                source.Position = offset;
                return SeekResult.Ok;
            });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { (0L, SeekOrigin.Begin) }, seeks);
        string written = Encoding.ASCII.GetString(connector.Written);
        Assert.AreEqual(2, written.Split("\r\n3\r\nabc\r\n0\r\n\r\n").Length - 1);
    }

    [TestMethod]
    [DataRow(SeekResult.Fail, "seek callback returned error 1")]
    [DataRow(SeekResult.CantSeek, "seek callback returned error 2")]
    public async Task PerformAsync_RedirectResendsAReadCallbackUploadAndSeekFails_ReturnsSendFailRewindWithLibcurlsText(SeekResult result, string text)
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Continue + TemporaryRedirect), Encoding.ASCII.GetBytes(Continue + Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = SeekingUploadOptions(ReadOf("abc"), (_, _) => result);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.SendFailRewind, code);
        Assert.AreEqual(text, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_SeekCallbackSetWithoutARedirect_NeverCallsIt()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Continue + Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        int seeks = 0;
        easy.Options = SeekingUploadOptions(ReadOf("abc"), (_, _) =>
        {
            seeks++;
            return SeekResult.Ok;
        });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0, seeks);
    }

    /// <summary>A <c>PUT</c> of the read callback's bytes to <c>http://h/up</c> under <c>CURLOPT_FOLLOWLOCATION</c>, with a seek callback.</summary>
    private static EasyOptions SeekingUploadOptions(ReadCallback read, SeekCallback seek) => new()
    {
        Network = new EasyNetworkOptions { Url = "http://h/up" },
        Transfer = new EasyTransferOptions { Upload = true },
        Http = new EasyHttpOptions { FollowLocation = FollowLocation.All },
        Callbacks = new() { Read = read, Seek = seek },
    };

    /// <summary>Waits until a callback pauses <paramref name="direction" />, then unpauses the handle, as an application's other thread would.</summary>
    private static Task UnpauseOnceAsync(ManagedEasyTransfer easy, CurlPause direction) =>
        Task.Run(async () =>
        {
            while ((easy.PausedDirections & direction) == 0)
            {
                await Task.Yield();
            }

            easy.Pause(CurlPause.Continue);
        });
}
