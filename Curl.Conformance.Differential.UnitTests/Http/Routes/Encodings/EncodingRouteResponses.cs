using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Sends an encoding route two requests for its path over an in-memory connection and splits what it wrote.</summary>
internal static class EncodingRouteResponses
{
    public const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    /// <summary>The gzip header Python's zlib writes at level 9: mtime 0, extra flags 2, OS 3.</summary>
    public static readonly byte[] PythonGzipHeader = [0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x03];

    public static Task<string> RespondTwiceAsync(IDifferentialRoute route, string path) =>
        DifferentialHttpResponderTests.RespondAsync(
            [route],
            new DifferentialResponderState(),
            $"GET {path} HTTP/1.1\r\n\r\nGET {path} HTTP/1.1\r\n\r\n");

    /// <summary>For a route that keeps the connection: checks both answers are the same and returns one as head and body.</summary>
    public static async Task<(string Head, byte[] Body)> RespondKeptAsync(IDifferentialRoute route, string path)
    {
        var written = await RespondTwiceAsync(route, path);
        var answer = written[..(written.Length / 2)];
        Assert.AreEqual(answer + answer, written);
        var headEnd = answer.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        return (answer[..headEnd], Encoding.Latin1.GetBytes(answer[headEnd..]));
    }

    public static string OkHead(string headers, int contentLength) =>
        $"HTTP/1.1 200 OK\r\n{Date}{headers}Content-Length: {contentLength}\r\n\r\n";

    public static byte[] Decompress(byte[] compressed, Func<Stream, Stream> openDecompressor)
    {
        using var output = new MemoryStream();
        using (var decompressor = openDecompressor(new MemoryStream(compressed)))
        {
            decompressor.CopyTo(output);
        }

        return output.ToArray();
    }

    public static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static string Repeated(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
}
