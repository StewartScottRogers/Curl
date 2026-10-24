using System.Globalization;

namespace Curl.Conformance.Differential.Libcurl.WebSocket;

/// <summary>
/// Writes what one <c>curl_ws_*</c> call answered as one line, its fields separated by
/// <c> |</c> so <see cref="Easy.LibcurlEasyComparer"/> reports the fields that differ.
/// </summary>
public static class LibcurlWebSocketLineWriter
{
    /// <summary>The perform that opens the connection.</summary>
    /// <param name="code">The perform's <c>CURLcode</c>.</param>
    /// <returns>The line.</returns>
    public static string Perform(int code) => string.Create(CultureInfo.InvariantCulture, $"perform |code={code}");

    /// <summary>A <c>curl_ws_send</c> call.</summary>
    /// <param name="step">The step that made it.</param>
    /// <param name="code">Its <c>CURLcode</c>.</param>
    /// <param name="sent">Its <c>*sent</c>.</param>
    /// <returns>The line.</returns>
    public static string Send(LibcurlWebSocketStep step, int code, long sent)
    {
        ArgumentNullException.ThrowIfNull(step);
        return string.Create(CultureInfo.InvariantCulture, $"send flags={step.Flags} frag={step.FragmentSize} len={step.Payload.Length} |code={code} |sent={sent}");
    }

    /// <summary>A <c>curl_ws_recv</c> call, after any <c>CURLE_AGAIN</c> answers.</summary>
    /// <param name="step">The step that made it.</param>
    /// <param name="code">Its <c>CURLcode</c>.</param>
    /// <param name="data">The bytes it wrote.</param>
    /// <param name="frame">Its <c>*meta</c>, or <see langword="null"/>.</param>
    /// <returns>The line.</returns>
    public static string Receive(LibcurlWebSocketStep step, int code, ReadOnlySpan<byte> data, LibcurlWebSocketFrameFields? frame)
    {
        ArgumentNullException.ThrowIfNull(step);
        return string.Create(CultureInfo.InvariantCulture, $"recv buf={step.BufferSize} |code={code} |n={data.Length} |data={PythonLiteral.FormatBytes(data)} |frame={Frame(frame)}");
    }

    /// <summary>A <c>curl_ws_meta</c> call outside a write callback.</summary>
    /// <param name="frame">What it answered, or <see langword="null"/>.</param>
    /// <returns>The line.</returns>
    public static string Meta(LibcurlWebSocketFrameFields? frame) => $"meta |frame={Frame(frame)}";

    /// <summary>One write callback call and the <c>curl_ws_meta</c> it read.</summary>
    /// <param name="data">The bytes the callback was given.</param>
    /// <param name="frame">What <c>curl_ws_meta</c> answered inside it, or <see langword="null"/>.</param>
    /// <returns>The line.</returns>
    public static string Write(ReadOnlySpan<byte> data, LibcurlWebSocketFrameFields? frame) =>
        string.Create(CultureInfo.InvariantCulture, $"write |n={data.Length} |data={PythonLiteral.FormatBytes(data)} |frame={Frame(frame)}");

    private static string Frame(LibcurlWebSocketFrameFields? frame) => frame is null
        ? "null"
        : string.Create(CultureInfo.InvariantCulture, $"age={frame.Age} flags={frame.Flags} offset={frame.Offset} bytesleft={frame.BytesLeft} len={frame.Length}");
}
