using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Pins the string forms of <c>google.protobuf.Timestamp</c> and <c>google.protobuf.Duration</c>
/// in the JSON mapping (protobuf.dev/programming-guides/json), both ways.
/// </summary>
[TestClass]
public sealed class ProtobufJsonTimeTypesTests
{
    private const string Proto = """
        syntax = "proto3";
        package google.protobuf;
        message Timestamp { int64 seconds = 1; int32 nanos = 2; }
        message Duration { int64 seconds = 1; int32 nanos = 2; }
        message Times { Timestamp at = 1; Duration took = 2; repeated Duration laps = 3; }
        """;

    private static readonly DynamicTypeCatalogue Catalogue = CreateCatalogue();

    private static DynamicMessageType Times => Catalogue.GetMessageType("google.protobuf.Times");

    [TestMethod]
    [DataRow("1970-01-01T00:00:01Z", 1L, 0, "1970-01-01T00:00:01Z")]
    [DataRow("1970-01-01T00:00:00.5Z", 0L, 500_000_000, "1970-01-01T00:00:00.500Z")]
    [DataRow("1970-01-01T00:00:00.000001Z", 0L, 1_000, "1970-01-01T00:00:00.000001Z")]
    [DataRow("1970-01-01T00:00:00.123456789Z", 0L, 123_456_789, "1970-01-01T00:00:00.123456789Z")]
    [DataRow("1970-01-01T01:00:00+01:00", 0L, 0, "1970-01-01T00:00:00Z")]
    [DataRow("1969-12-31T22:30:00-01:30", 0L, 0, "1970-01-01T00:00:00Z")]
    [DataRow("1969-12-31T23:59:59Z", -1L, 0, "1969-12-31T23:59:59Z")]
    [DataRow("0001-01-01T00:00:00Z", -62135596800L, 0, "0001-01-01T00:00:00Z")]
    [DataRow("9999-12-31T23:59:59.999999999Z", 253402300799L, 999_999_999, "9999-12-31T23:59:59.999999999Z")]
    public void Timestamp_RoundTrips_AsRfc3339InUtc(string text, long seconds, int nanos, string written)
    {
        var message = Parse($$"""{"at":"{{text}}"}""");
        var at = (DynamicMessage)message.GetValue("at");

        Assert.AreEqual(seconds, at.GetValue("seconds"));
        Assert.AreEqual(nanos, at.GetValue("nanos"));
        Assert.AreEqual($$"""{"at":"{{written}}"}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    [DataRow("1.5s", 1L, 500_000_000, "1.500s")]
    [DataRow("0s", 0L, 0, "0s")]
    [DataRow("-0s", 0L, 0, "0s")]
    [DataRow("-1.5s", -1L, -500_000_000, "-1.500s")]
    [DataRow("-0.000001s", 0L, -1_000, "-0.000001s")]
    [DataRow("2.000000001s", 2L, 1, "2.000000001s")]
    [DataRow("-3s", -3L, 0, "-3s")]
    [DataRow("315576000000s", 315576000000L, 0, "315576000000s")]
    public void Duration_RoundTrips_AsSecondsWith0369FractionalDigits(string text, long seconds, int nanos, string written)
    {
        var message = Parse($$"""{"took":"{{text}}"}""");
        var took = (DynamicMessage)message.GetValue("took");

        Assert.AreEqual(seconds, took.GetValue("seconds"));
        Assert.AreEqual(nanos, took.GetValue("nanos"));
        Assert.AreEqual($$"""{"took":"{{written}}"}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void RepeatedDuration_RoundTrips_AsAnArrayOfStrings() =>
        Assert.AreEqual("""{"laps":["1s","0.250s"]}""", ProtobufJsonFormatter.Format(Parse("""{"laps":["1s","0.25s"]}"""), indented: false));

    [TestMethod]
    [DataRow("""{"at":5}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"at":{"seconds":1}}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"took":5}""", ProtobufJsonError.WrongType)]
    [DataRow("""{"at":"1970-01-01 00:00:00Z"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-13-01T00:00:00Z"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00.1234567890Z"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"0001-01-01T00:00:00+01:00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"9999-12-31T23:59:59-01:00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00Z\n"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"1.5"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"1.5 s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"+1s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"315576000001s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"1000000000000s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"1.0000000001s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"1.s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"-.5s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"took":"--1s"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00.5"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00.Z"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00z"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00+0100"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00+01-00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00*01:00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00+0a:00"}""", ProtobufJsonError.InvalidValue)]
    [DataRow("""{"at":"1970-01-01T00:00:00+01:0a"}""", ProtobufJsonError.InvalidValue)]
    public void Parse_NotTheStringForm_IsRejected(string json, ProtobufJsonError expected) =>
        Assert.AreEqual(expected, ProtobufJsonParser.Parse(Times, Encoding.UTF8.GetBytes(json), out _));

    [TestMethod]
    [DataRow("at", -62135596801L, 0)]
    [DataRow("at", 253402300800L, 0)]
    [DataRow("at", 0L, -1)]
    [DataRow("at", 0L, 1_000_000_000)]
    [DataRow("took", 315576000001L, 0)]
    [DataRow("took", -315576000001L, 0)]
    [DataRow("took", 0L, 1_000_000_000)]
    [DataRow("took", 0L, -1_000_000_000)]
    [DataRow("took", -1L, 1)]
    [DataRow("took", 1L, -1)]
    [DataRow("took", long.MinValue, int.MinValue)]
    public void Format_OutOfRange_ThrowsFormatException(string field, long seconds, int nanos) =>
        Assert.ThrowsExactly<FormatException>(() => ProtobufJsonFormatter.Format(WithTime(field, seconds, nanos), indented: false));

    [TestMethod]
    public void TryConvertBinaryToJson_TimestampOutOfRange_IsInvalidValue()
    {
        var binary = DynamicMessageEncoder.Encode(WithTime("at", 0, -1));

        var error = ProtobufJsonConverter.ConvertBinaryToJson(Times, binary, indented: false, out var json);

        Assert.AreEqual(ProtobufJsonError.InvalidValue, error);
        Assert.IsNull(json);
    }

    [TestMethod]
    public void TryConvertJsonToBinary_TimestampAndDuration_AreTheirMessages()
    {
        var error = ProtobufJsonConverter.ConvertJsonToBinary(Times, Encoding.UTF8.GetBytes("""{"at":"1970-01-01T00:00:01Z","took":"1.5s"}"""), out var binary);

        Assert.AreEqual(ProtobufJsonError.None, error);
        Assert.AreEqual("0A020801120808011080CAB5EE01", Convert.ToHexString(binary!));
    }

    private static DynamicMessage Parse(string json)
    {
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonParser.Parse(Times, Encoding.UTF8.GetBytes(json), out var message));
        return message!;
    }

    private static DynamicMessage WithTime(string field, long seconds, int nanos)
    {
        var message = new DynamicMessage(Times);
        var time = new DynamicMessage(Times.FindField(field)!.MessageType!);
        time.SetValue("seconds", seconds);
        time.SetValue("nanos", nanos);
        message.SetValue(field, time);
        return message;
    }

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
