namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>One <c>curl_ws_*</c> call a connect-only case makes after its perform.</summary>
/// <param name="Kind">The call.</param>
/// <param name="Payload">What <c>curl_ws_send</c> sends; empty for the other calls.</param>
/// <param name="Flags">The <c>CURLWS_*</c> flags <c>curl_ws_send</c> is given.</param>
/// <param name="FragmentSize">The <c>fragsize</c> <c>curl_ws_send</c> is given.</param>
/// <param name="BufferSize">The buffer length <c>curl_ws_recv</c> is given.</param>
public sealed record LibcurlWebSocketStep(LibcurlWebSocketStepKind Kind, byte[] Payload, int Flags, long FragmentSize, int BufferSize)
{
    /// <summary>A <c>curl_ws_send</c> call.</summary>
    public static LibcurlWebSocketStep Send(byte[] payload, int flags, long fragmentSize = 0) =>
        new(LibcurlWebSocketStepKind.Send, payload, flags, fragmentSize, 0);

    /// <summary>A <c>curl_ws_recv</c> call with a buffer of <paramref name="bufferSize"/> bytes.</summary>
    public static LibcurlWebSocketStep Receive(int bufferSize = 256) =>
        new(LibcurlWebSocketStepKind.Receive, [], 0, 0, bufferSize);

    /// <summary>A <c>curl_ws_meta</c> call.</summary>
    public static LibcurlWebSocketStep Meta() => new(LibcurlWebSocketStepKind.Meta, [], 0, 0, 0);
}
