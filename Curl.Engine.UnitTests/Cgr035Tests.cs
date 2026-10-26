using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>CGR-035: <c>curl_easy_pause(CURLPAUSE_RECV)</c> holds the write callback until the handle is unpaused.</summary>
[TestClass]
public sealed class Cgr035Tests
{
    [TestMethod]
    public async Task WriteAsync_ReceivingPausedByTheApplication_CallsNoCallbackUntilUnpaused()
    {
        EasyPauseGate gate = new();
        int calls = 0;
        WriteCallback callback = data =>
        {
            calls++;
            return data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback, gate);
        gate.Set(CurlPause.Receive);

        Task writing = stream.WriteAsync(new byte[] { 1, 2 }).AsTask();
        int callsWhilePaused = calls;
        bool completedWhilePaused = writing.IsCompleted;
        gate.Set(CurlPause.Continue);
        await writing.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(0, callsWhilePaused);
        Assert.IsFalse(completedWhilePaused);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task WriteAsync_CallbackPausesThroughTheApiAndTakesTheChunk_HoldsTheNextChunkUntilUnpaused()
    {
        EasyPauseGate gate = new();
        int calls = 0;
        WriteCallback callback = data =>
        {
            calls++;
            if (calls == 1)
            {
                gate.Set(CurlPause.Receive);
            }

            return data.Length;
        };
        await using CallbackWriteStream stream = CallbackWriteStream.Of(callback, gate);
        await stream.WriteAsync(new byte[] { 1 });

        Task second = stream.WriteAsync(new byte[] { 2 }).AsTask();
        int callsWhilePaused = calls;
        gate.Set(CurlPause.Continue);
        await second.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(1, callsWhilePaused);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackPausesTheHandleAndTakesTheChunk_DeliversNothingMoreUntilUnpaused()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhello"), Encoding.ASCII.GetBytes("world")]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        TaskCompletionSource firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool calledWhilePaused = false;
        StringBuilder received = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/p" },
            Callbacks = new EasyCallbackOptions
            {
                Write = data =>
                {
                    calledWhilePaused |= (easy.PausedDirections & CurlPause.Receive) != 0;
                    received.Append(Encoding.ASCII.GetString(data));
                    if (firstCall.TrySetResult())
                    {
                        _ = easy.Pause(CurlPause.Receive);
                    }

                    return data.Length;
                },
            },
        };

        Task<CurlCode> performing = easy.PerformAsync(CancellationToken.None).AsTask();
        await firstCall.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Task.WhenAny(performing, Task.Delay(300));
        bool finishedWhilePaused = performing.IsCompleted;
        _ = easy.Pause(CurlPause.Continue);
        CurlCode code = await performing.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.IsFalse(calledWhilePaused);
        Assert.IsFalse(finishedWhilePaused);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("helloworld", received.ToString());
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "Failed to connect"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null);
}
