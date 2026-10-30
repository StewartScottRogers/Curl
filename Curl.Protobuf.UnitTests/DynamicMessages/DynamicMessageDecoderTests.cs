using Curl.Protobuf.Wire;
using static Curl.Protobuf.DynamicMessages.DynamicTestTypes;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// Pins how <see cref="DynamicMessageDecoder" /> reports malformed input: the wire codec's
/// typed <see cref="ProtobufDecodeError" />, never an exception, and no message.
/// </summary>
[TestClass]
public sealed class DynamicMessageDecoderTests
{
    [TestMethod]
    [DataRow("Test1", "08", ProtobufDecodeError.TruncatedVarint, DisplayName = "tag with no value")]
    [DataRow("Test1", "0896", ProtobufDecodeError.TruncatedVarint, DisplayName = "truncated varint value")]
    [DataRow("Test1", "88", ProtobufDecodeError.TruncatedVarint, DisplayName = "truncated tag")]
    [DataRow("Test1", "0E", ProtobufDecodeError.InvalidWireType, DisplayName = "wire type 6")]
    [DataRow("Test1", "0C", ProtobufDecodeError.UnmatchedEndGroup, DisplayName = "end group at the top")]
    [DataRow("Test1", "1205", ProtobufDecodeError.LengthPastEnd, DisplayName = "unknown field past the end")]
    [DataRow("Test2", "120561", ProtobufDecodeError.LengthPastEnd, DisplayName = "string past the end")]
    [DataRow("Test2", "1201FF", ProtobufDecodeError.InvalidUtf8, DisplayName = "string not UTF-8")]
    [DataRow("Test3", "1A05", ProtobufDecodeError.LengthPastEnd, DisplayName = "message past the end")]
    [DataRow("Test3", "1A020896", ProtobufDecodeError.TruncatedVarint, DisplayName = "malformed nested message")]
    [DataRow("Test4", "3205", ProtobufDecodeError.LengthPastEnd, DisplayName = "packed past the end")]
    [DataRow("Test4", "320196", ProtobufDecodeError.TruncatedVarint, DisplayName = "packed truncated varint")]
    [DataRow("Repeats", "2A0101", ProtobufDecodeError.TruncatedFixed, DisplayName = "packed truncated fixed32")]
    [DataRow("Scalars", "090102", ProtobufDecodeError.TruncatedFixed, DisplayName = "truncated fixed64")]
    [DataRow("Scalars", "1501", ProtobufDecodeError.TruncatedFixed, DisplayName = "truncated fixed32")]
    [DataRow("Maps", "0A05", ProtobufDecodeError.LengthPastEnd, DisplayName = "map entry past the end")]
    [DataRow("Maps", "0A021096", ProtobufDecodeError.TruncatedVarint, DisplayName = "malformed map entry")]
    public void TryDecode_MalformedInput_IsTheTypedErrorAndNoMessage(string typeName, string hex, ProtobufDecodeError expected)
    {
        var error = DynamicMessageDecoder.TryDecode(Type(typeName), Convert.FromHexString(hex), out var message);

        Assert.AreEqual(expected, error);
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryDecode_MessagesNestedPastTheMaximumDepth_IsNestingTooDeep()
    {
        var payload = Array.Empty<byte>();
        for (var depth = 0; depth <= ProtobufReader.MaximumDepth; depth++)
        {
            var writer = new ProtobufWriter();
            writer.WriteBytesField(4, payload);
            payload = writer.ToArray();
        }

        var error = DynamicMessageDecoder.TryDecode(Type("Outer"), payload, out var message);

        Assert.AreEqual(ProtobufDecodeError.NestingTooDeep, error);
        Assert.IsNull(message);
    }

    [TestMethod]
    public void TryDecode_EmptyInput_IsAnEmptyMessage()
    {
        var error = DynamicMessageDecoder.TryDecode(Type("Test1"), ReadOnlyMemory<byte>.Empty, out var message);

        Assert.AreEqual(ProtobufDecodeError.None, error);
        Assert.IsFalse(message!.HasValue("a"));
    }
}
