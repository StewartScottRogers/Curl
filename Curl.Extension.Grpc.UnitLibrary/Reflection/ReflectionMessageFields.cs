using System.Text;
using Curl.Extension.Grpc.Wire;
using Curl.Protobuf.Wire;

namespace Curl.Extension.Grpc.Reflection;

/// <summary>
/// The fields of one binary message the server reflection client reads (ADR-1149): a <c>ServerReflectionResponse</c> or a
/// <c>FileDescriptorProto</c> and the messages inside it, looked up by field number, with each field's default when absent.
/// </summary>
internal sealed class ReflectionMessageFields
{
    private readonly ILookup<int, ProtobufField> fields;

    private ReflectionMessageFields(IReadOnlyList<ProtobufField> fields) =>
        this.fields = fields.ToLookup(static field => field.FieldNumber);

    /// <summary>Reads every field of a message.</summary>
    /// <param name="message">The message's binary form.</param>
    /// <returns>The message's fields.</returns>
    /// <exception cref="GrpcWireException">The bytes are not a Protocol Buffers message.</exception>
    public static ReflectionMessageFields Read(ReadOnlyMemory<byte> message)
    {
        ProtobufDecodeError error = new ProtobufReader(message).TryReadAllFields(out IReadOnlyList<ProtobufField> read);
        return error == ProtobufDecodeError.None
            ? new ReflectionMessageFields(read)
            : throw new GrpcWireException($"A server reflection message does not decode ({error}).");
    }

    /// <summary>Tells whether the message holds the field.</summary>
    public bool Has(int fieldNumber) => fields.Contains(fieldNumber);

    /// <summary>Gives a string field's last value, or the empty string.</summary>
    public string String(int fieldNumber) => Strings(fieldNumber).LastOrDefault() ?? string.Empty;

    /// <summary>Gives every value of a repeated string field.</summary>
    public IEnumerable<string> Strings(int fieldNumber) => Bytes(fieldNumber).Select(static value => Encoding.UTF8.GetString(value.Span));

    /// <summary>Gives every value of a repeated bytes field.</summary>
    public IEnumerable<ReadOnlyMemory<byte>> Bytes(int fieldNumber) => fields[fieldNumber].Select(static field => field.Payload);

    /// <summary>Gives every value of a repeated message field, read.</summary>
    public IEnumerable<ReflectionMessageFields> Messages(int fieldNumber) => Bytes(fieldNumber).Select(static value => Read(value));

    /// <summary>Gives a message field's last value, read, or <see langword="null" /> when it is absent.</summary>
    public ReflectionMessageFields? Message(int fieldNumber) => Messages(fieldNumber).LastOrDefault();

    /// <summary>Gives an <c>int32</c>, <c>int64</c>, <c>enum</c> or <c>bool</c> field's last value, or 0.</summary>
    public long Integer(int fieldNumber) => (long)fields[fieldNumber].Select(static field => field.Scalar).LastOrDefault();
}
