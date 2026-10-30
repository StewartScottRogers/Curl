using Curl.Protobuf.Descriptors;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>The message types the dynamic message tests encode and decode, from one <c>.proto</c> file.</summary>
internal static class DynamicTestTypes
{
    private const string Proto = """
        syntax = "proto3";
        package test;
        message Test1 { int32 a = 1; }
        message Test2 { string b = 2; }
        message Test3 { Test1 c = 3; }
        message Test4 { string d = 4; repeated int32 e = 6; }
        enum Color { RED = 0; GREEN = 1; }
        message Scalars {
          double f_double = 1; float f_float = 2; int32 f_int32 = 3; int64 f_int64 = 4;
          uint32 f_uint32 = 5; uint64 f_uint64 = 6; sint32 f_sint32 = 7; sint64 f_sint64 = 8;
          fixed32 f_fixed32 = 9; fixed64 f_fixed64 = 10; sfixed32 f_sfixed32 = 11; sfixed64 f_sfixed64 = 12;
          bool f_bool = 13; string f_string = 14; bytes f_bytes = 15; Color f_color = 16;
        }
        message Repeats {
          repeated sint64 packed = 1; repeated int32 unpacked = 2 [packed = false];
          repeated string names = 3; repeated Test1 items = 4; repeated fixed32 fixed = 5; repeated double doubles = 6;
          repeated int32 marked = 7 [deprecated = true, packed = true];
        }
        message Presence { optional int32 count = 1; oneof choice { string text = 2; int32 number = 3; Test1 message = 4; } }
        message Maps { map<string, int32> counts = 1; map<int32, Test1> items = 2; }
        message Outer {
          message Inner { Color color = 1; }
          enum Mood { CALM = 0; }
          Inner inner = 1; Mood mood = 2; .test.Test1 absolute = 3; Outer recursive = 4;
        }
        message Broken { Missing missing = 1; }
        """;

    public static DynamicTypeCatalogue Catalogue { get; } = CreateCatalogue();

    public static DynamicMessageType Type(string name) => Catalogue.GetMessageType($"test.{name}");

    public static DynamicMessage Message(string name) => new(Type(name));

    public static DynamicMessage Decode(string name, string hex)
    {
        var error = DynamicMessageDecoder.TryDecode(Type(name), Convert.FromHexString(hex), out var message);
        Assert.AreEqual(Wire.ProtobufDecodeError.None, error);
        return message!;
    }

    public static string EncodeToHex(DynamicMessage message) => Convert.ToHexString(DynamicMessageEncoder.Encode(message));

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
