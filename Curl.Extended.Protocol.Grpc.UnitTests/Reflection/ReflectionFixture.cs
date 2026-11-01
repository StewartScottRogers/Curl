using Curl.Extended.Protocol.Grpc.Client;
using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Http2;
using Curl.Protobuf.Wire;

namespace Curl.Extended.Protocol.Grpc.Reflection;

/// <summary>
/// Writes descriptor.proto and reflection.proto messages in the field order protoc and grpc-go's reflection service write
/// them, and scripts a server's HTTP/2 frames on any stream.
/// </summary>
internal static class ReflectionFixture
{
    /// <summary>
    /// helloworld.proto (grpc/grpc examples) as protoc serializes its <c>FileDescriptorProto</c>, without the file options.
    /// </summary>
    public static readonly byte[] HelloWorldFile = File(
        "helloworld.proto",
        "helloworld",
        "proto3",
        [],
        [
            Message("HelloRequest", [Field("name", 1, 1, 9)]),
            Message("HelloReply", [Field("message", 1, 1, 9)]),
        ],
        [],
        [Service("Greeter", [("SayHello", ".helloworld.HelloRequest", ".helloworld.HelloReply")])]);

    /// <summary>The Response-Headers of a successful call.</summary>
    public static readonly string[] OkHeaders = [":status", "200", "content-type", "application/grpc"];

    /// <summary>Writes a <c>FileDescriptorProto</c>.</summary>
    public static byte[] File(string name, string package, string? syntax, string[] dependencies, byte[][] messages, byte[][] enums, byte[][] services)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, name);
        if (package.Length > 0)
        {
            writer.WriteStringField(2, package);
        }

        Array.ForEach(dependencies, dependency => writer.WriteStringField(3, dependency));
        Array.ForEach(messages, message => writer.WriteBytesField(4, message));
        Array.ForEach(enums, enumType => writer.WriteBytesField(5, enumType));
        Array.ForEach(services, service => writer.WriteBytesField(6, service));
        if (syntax is not null)
        {
            writer.WriteStringField(12, syntax);
        }

        return writer.ToArray();
    }

    /// <summary>Writes a <c>DescriptorProto</c>.</summary>
    public static byte[] Message(string name, byte[][] fields, byte[][]? nested = null, byte[][]? enums = null, string[]? oneofs = null, bool isMapEntry = false)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, name);
        Array.ForEach(fields, field => writer.WriteBytesField(2, field));
        Array.ForEach(nested ?? [], message => writer.WriteBytesField(3, message));
        Array.ForEach(enums ?? [], enumType => writer.WriteBytesField(4, enumType));
        if (isMapEntry)
        {
            var options = new ProtobufWriter();
            options.WriteVarintField(7, 1);
            writer.WriteBytesField(7, options.WrittenBytes);
        }

        foreach (string oneof in oneofs ?? [])
        {
            var oneofWriter = new ProtobufWriter();
            oneofWriter.WriteStringField(1, oneof);
            writer.WriteBytesField(8, oneofWriter.WrittenBytes);
        }

        return writer.ToArray();
    }

    /// <summary>Writes a <c>FieldDescriptorProto</c>: label 1 optional, 2 required, 3 repeated; type by descriptor.proto's numbers.</summary>
    public static byte[] Field(string name, int number, int label, int type, string? typeName = null, int? oneofIndex = null, bool isProto3Optional = false, bool? packed = null)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, name);
        writer.WriteInt32Field(3, number);
        writer.WriteInt32Field(4, label);
        writer.WriteInt32Field(5, type);
        if (typeName is not null)
        {
            writer.WriteStringField(6, typeName);
        }

        if (packed is { } isPacked)
        {
            var options = new ProtobufWriter();
            options.WriteVarintField(2, isPacked ? 1UL : 0UL);
            writer.WriteBytesField(8, options.WrittenBytes);
        }

        if (oneofIndex is { } index)
        {
            writer.WriteInt32Field(9, index);
        }

        writer.WriteStringField(10, name);
        if (isProto3Optional)
        {
            writer.WriteVarintField(17, 1);
        }

        return writer.ToArray();
    }

    /// <summary>Writes an <c>EnumDescriptorProto</c>.</summary>
    public static byte[] Enum(string name, params (string Name, int Number)[] values)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, name);
        foreach ((string valueName, int number) in values)
        {
            var value = new ProtobufWriter();
            value.WriteStringField(1, valueName);
            value.WriteInt32Field(2, number);
            writer.WriteBytesField(2, value.WrittenBytes);
        }

        return writer.ToArray();
    }

    /// <summary>Writes a <c>ServiceDescriptorProto</c>.</summary>
    public static byte[] Service(string name, (string Name, string Input, string Output)[] methods, bool isStreaming = false)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, name);
        foreach ((string methodName, string input, string output) in methods)
        {
            var method = new ProtobufWriter();
            method.WriteStringField(1, methodName);
            method.WriteStringField(2, input);
            method.WriteStringField(3, output);
            if (isStreaming)
            {
                method.WriteVarintField(5, 1);
                method.WriteVarintField(6, 1);
            }

            writer.WriteBytesField(2, method.WrittenBytes);
        }

        return writer.ToArray();
    }

    /// <summary>Writes a <c>ServerReflectionResponse</c> carrying a <c>file_descriptor_response</c> with the files given.</summary>
    public static byte[] FilesResponse(params byte[][] files)
    {
        var descriptors = new ProtobufWriter();
        Array.ForEach(files, file => descriptors.WriteBytesField(1, file));
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, string.Empty);
        writer.WriteBytesField(4, descriptors.WrittenBytes);
        return writer.ToArray();
    }

    /// <summary>Writes a <c>ServerReflectionResponse</c> carrying an <c>error_response</c>.</summary>
    public static byte[] ErrorResponse(int code, string message)
    {
        var error = new ProtobufWriter();
        error.WriteInt32Field(1, code);
        error.WriteStringField(2, message);
        var writer = new ProtobufWriter();
        writer.WriteBytesField(7, error.WrittenBytes);
        return writer.ToArray();
    }

    /// <summary>Gives the server's SETTINGS frame (with the settings given) followed by the given frames.</summary>
    public static byte[] Server(Http2Setting[] settings, params Http2Frame[] frames) =>
        [.. new[] { Http2FrameFactory.CreateSettings(settings) }.Concat(frames).SelectMany(Http2FrameCodec.Serialize)];

    /// <summary>Gives the server's empty SETTINGS frame followed by the given frames.</summary>
    public static byte[] Server(params Http2Frame[] frames) => Server([], frames);

    /// <summary>Gives a HEADERS frame carrying the name and value pairs given.</summary>
    public static Http2Frame Headers(int streamId, bool isEndStream, params string[] namesAndValues) =>
        Http2FrameFactory.CreateHeaders(
            streamId,
            new HpackEncoder().Encode(namesAndValues.Chunk(2).Select(static pair => new HeaderField(pair[0], pair[1]))),
            isEndStream,
            isEndHeaders: true);

    /// <summary>Gives a DATA frame carrying one uncompressed Length-Prefixed-Message.</summary>
    public static Http2Frame Message(int streamId, byte[] message) =>
        Http2FrameFactory.CreateData(streamId, LengthPrefixedMessage.Frame(message, isCompressed: false), isEndStream: false);

    /// <summary>Gives the Trailers ending a stream with the status given.</summary>
    public static Http2Frame Trailers(int streamId, int status, string message = "") =>
        Headers(streamId, true, "grpc-status", status.ToString(System.Globalization.CultureInfo.InvariantCulture), "grpc-message", message);

    /// <summary>Gives every Length-Prefixed-Message the client sent on a stream, after its connection preface.</summary>
    public static async Task<List<byte[]>> SentMessagesAsync(byte[] sent, int streamId)
    {
        using var stream = new MemoryStream(sent, Http2Connection.ClientPreface.Length, sent.Length - Http2Connection.ClientPreface.Length);
        var reader = new LengthPrefixedMessageReader();
        List<byte[]> messages = [];
        while (await Http2FrameCodec.ReadAsync(stream, 1 << 20, CancellationToken.None) is { } frame)
        {
            if (frame.StreamId == streamId && frame.Type == Http2FrameType.Data)
            {
                reader.Append(frame.Payload.Span);
                while (reader.TryRead(out GrpcMessage? message))
                {
                    messages.Add(message.Payload);
                }
            }
        }

        return messages;
    }

    /// <summary>Opens an HTTP/2 session on a connection that replays the server's bytes.</summary>
    public static async Task<(GrpcHttp2Session Session, ScriptedConnection Connection)> SessionAsync(byte[] server)
    {
        var connection = new ScriptedConnection(server);
        return (await GrpcHttp2Session.OpenAsync(new ConnectionStream(connection), CancellationToken.None), connection);
    }
}
