using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Conformance.Differential.Libcurl.WebSocket;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.WebSocket;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlWebSocket;

/// <summary>Runs a WebSocket case through Curl's Libcurl <c>curl_ws_*</c> equivalents.</summary>
internal static class CurlLibcurlWebSocketDriver
{
    /// <summary>Runs <paramref name="webSocketCase"/> against <paramref name="url"/>.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(CurlLibrary library, LibcurlWebSocketCase webSocketCase, string url, CancellationToken cancellationToken)
    {
        using var easy = library.EasyInit();
        var lines = new List<string>();
        easy.SetOption(CurlOption.Url, url);
        if (webSocketCase.ConnectOnly)
        {
            easy.SetOption(CurlOption.ConnectOnly, 2L);
        }
        else
        {
            easy.SetOption(CurlOption.Writefunction, (WriteCallback)(data =>
            {
                lines.Add(LibcurlWebSocketLineWriter.Write(data, Fields(easy.WebSocketMeta())));
                return data.Length;
            }));
        }

        lines.Add(LibcurlWebSocketLineWriter.Perform((int)easy.Perform()));
        foreach (var step in webSocketCase.Steps)
        {
            lines.Add(step.Kind switch
            {
                LibcurlWebSocketStepKind.Send => Send(easy, step),
                LibcurlWebSocketStepKind.Receive => await ReceiveAsync(easy, step, cancellationToken),
                _ => LibcurlWebSocketLineWriter.Meta(Fields(easy.WebSocketMeta())),
            });
        }

        return lines;
    }

    private static string Send(CurlEasy easy, LibcurlWebSocketStep step)
    {
        var code = easy.WebSocketSend(step.Payload, out var sent, step.FragmentSize, (CurlWebSocketFlags)step.Flags);
        return LibcurlWebSocketLineWriter.Send(step, (int)code, sent);
    }

    private static async Task<string> ReceiveAsync(CurlEasy easy, LibcurlWebSocketStep step, CancellationToken cancellationToken)
    {
        var buffer = new byte[step.BufferSize];
        for (var attempt = 0; ; attempt++)
        {
            var code = easy.WebSocketRecv(buffer, out var received, out var frame);
            if (code != CurlCode.Again || attempt >= LibcurlWebSocketDifferentialTests.ReceiveAttempts)
            {
                return LibcurlWebSocketLineWriter.Receive(step, (int)code, buffer.AsSpan(0, received), Fields(frame));
            }

            await Task.Delay(LibcurlWebSocketDifferentialTests.ReceivePause, cancellationToken);
        }
    }

    private static LibcurlWebSocketFrameFields? Fields(CurlWebSocketFrame? frame) =>
        frame is { } found ? new(found.Age, (int)found.Flags, found.Offset, found.BytesLeft, found.Length) : null;
}
