using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Http2;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>Builds the bytes a gRPC server sends on stream 1, as PROTOCOL-HTTP2.md "Responses" lays them out, after its SETTINGS.</summary>
internal static class GrpcServerReply
{
    /// <summary>The Response-Headers of a successful call.</summary>
    public static readonly string[] OkHeaders = [":status", "200", "content-type", "application/grpc"];

    /// <summary>Gives the server's SETTINGS frame followed by the given frames.</summary>
    public static byte[] Of(params Http2Frame[] frames) =>
        [.. new[] { Http2FrameFactory.CreateSettings([]) }.Concat(frames).SelectMany(Http2FrameCodec.Serialize)];

    /// <summary>Gives a HEADERS frame on stream 1 carrying the name and value pairs given.</summary>
    public static Http2Frame Headers(bool isEndStream, params string[] namesAndValues) =>
        Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(FieldsOf(namesAndValues)), isEndStream, isEndHeaders: true);

    /// <summary>Gives a DATA frame on stream 1 carrying the bytes given.</summary>
    public static Http2Frame Data(byte[] data) => Http2FrameFactory.CreateData(1, data, isEndStream: false);

    /// <summary>Gives a DATA frame on stream 1 carrying one uncompressed Length-Prefixed-Message.</summary>
    public static Http2Frame Message(byte[] message) => Data(LengthPrefixedMessage.Frame(message, isCompressed: false));

    /// <summary>Gives the Trailers ending stream 1 with the status and message given.</summary>
    public static Http2Frame Trailers(int status, string message = "") =>
        Headers(true, "grpc-status", status.ToString(System.Globalization.CultureInfo.InvariantCulture), "grpc-message", message);

    private static IEnumerable<HeaderField> FieldsOf(string[] namesAndValues) =>
        namesAndValues.Chunk(2).Select(static pair => new HeaderField(pair[0], pair[1]));
}
