using System.Text;

namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>
/// A <c>grpc.reflection.v1.ServerReflectionRequest</c> (grpc/grpc <c>reflection.proto</c>) as the reference reflection
/// service reads it: its <c>host</c> and the one field of its <c>message_request</c> oneof that it set.
/// </summary>
/// <param name="Host">The <c>host</c> field; empty when the client sent none.</param>
/// <param name="FieldNumber">The number of the <c>message_request</c> field set, such as <see cref="FileContainingSymbolField" />; 0 for none.</param>
/// <param name="Value">That field's value as UTF-8 text.</param>
public sealed record GrpcReflectionRequest(string Host, int FieldNumber, string Value)
{
    /// <summary>The <c>host</c> field's number.</summary>
    public const int HostField = 1;

    /// <summary>The <c>file_by_filename</c> field's number.</summary>
    public const int FileByFilenameField = 3;

    /// <summary>The <c>file_containing_symbol</c> field's number.</summary>
    public const int FileContainingSymbolField = 4;

    private const int LengthDelimitedWireType = 2;

    /// <summary>A <c>file_by_filename</c> request for <paramref name="fileName" />.</summary>
    /// <param name="fileName">The file's name.</param>
    /// <returns>The request's binary form.</returns>
    public static byte[] FileByFilename(string fileName) => new ProtobufMessageWriter().String(FileByFilenameField, fileName).ToArray();

    /// <summary>Reads a request; every field of <c>ServerReflectionRequest</c> is length-delimited.</summary>
    /// <param name="message">The request's binary form.</param>
    /// <returns>The request.</returns>
    /// <exception cref="InvalidDataException">A field is not length-delimited, or the message ends inside a field.</exception>
    public static GrpcReflectionRequest Parse(ReadOnlySpan<byte> message)
    {
        var request = new GrpcReflectionRequest(string.Empty, 0, string.Empty);
        while (!message.IsEmpty)
        {
            ulong tag = ReadVarint(ref message);
            ulong length = ReadVarint(ref message);
            if ((tag & 7) != LengthDelimitedWireType || length > (ulong)message.Length)
            {
                throw new InvalidDataException($"A ServerReflectionRequest field (tag {tag}) is not a length-delimited field within the message.");
            }

            string value = Encoding.UTF8.GetString(message[..(int)length]);
            message = message[(int)length..];
            int fieldNumber = (int)(tag >> 3);
            request = fieldNumber == HostField ? request with { Host = value } : request with { FieldNumber = fieldNumber, Value = value };
        }

        return request;
    }

    private static ulong ReadVarint(ref ReadOnlySpan<byte> message)
    {
        ulong value = 0;
        int shift = 0;
        byte current;
        do
        {
            if (message.IsEmpty)
            {
                throw new InvalidDataException("A ServerReflectionRequest ends inside a varint.");
            }

            current = message[0];
            message = message[1..];
            value |= (ulong)(current & 0x7F) << shift;
            shift += 7;
        }
        while (current >= 0x80);
        return value;
    }
}
