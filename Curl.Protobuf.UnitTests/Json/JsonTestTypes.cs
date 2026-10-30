using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>The message types the JSON mapping tests convert, from one <c>.proto</c> file.</summary>
internal static class JsonTestTypes
{
    private const string Proto = """
        syntax = "proto3";
        package json;
        enum Color { RED = 0; GREEN = 1; }
        message Scalars {
          double f_double = 1; float f_float = 2; int32 f_int32 = 3; int64 f_int64 = 4;
          uint32 f_uint32 = 5; uint64 f_uint64 = 6; sint32 f_sint32 = 7; sint64 f_sint64 = 8;
          fixed32 f_fixed32 = 9; fixed64 f_fixed64 = 10; sfixed32 f_sfixed32 = 11; sfixed64 f_sfixed64 = 12;
          bool f_bool = 13; string f_string = 14; bytes f_bytes = 15; Color f_color = 16;
          int32 renamed = 17 [json_name = "custom"];
        }
        message Lists { repeated int64 numbers = 1; repeated Scalars items = 2; repeated Color colors = 3; }
        message Maps {
          map<string, int32> counts = 1; map<int64, Scalars> items = 2; map<bool, string> flags = 3; map<uint32, Color> colors = 4;
        }
        message Presence { optional int32 count = 1; oneof choice { string text = 2; int32 number = 3; } Scalars child = 4; }
        message Node { Node next = 1; int32 value = 2; }
        """;

    public static DynamicTypeCatalogue Catalogue { get; } = CreateCatalogue();

    public static DynamicMessageType Type(string name) => Catalogue.GetMessageType($"json.{name}");

    public static ProtobufJsonError Parse(string name, string json, out DynamicMessage? message) =>
        ProtobufJsonParser.TryParse(Type(name), Encoding.UTF8.GetBytes(json), out message);

    public static DynamicMessage Parse(string name, string json)
    {
        Assert.AreEqual(ProtobufJsonError.None, Parse(name, json, out var message));
        return message!;
    }

    public static ProtobufJsonError ParseError(string name, string json) => Parse(name, json, out _);

    public static string Format(DynamicMessage message) => ProtobufJsonFormatter.Format(message, indented: false);

    public static string RoundTrip(string name, string json) => Format(Parse(name, json));

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
