using System.Text;
using Curl.Protobuf.Descriptors;
using Curl.Protobuf.DynamicMessages;

namespace Curl.Protobuf.Json;

/// <summary>
/// Pins the JSON form of <c>google.protobuf.FieldMask</c> (protobuf.dev/programming-guides/json):
/// one comma-separated string of lowerCamelCase paths, both ways.
/// </summary>
[TestClass]
public sealed class ProtobufJsonFieldMaskTests
{
    private const string Proto = """
        syntax = "proto3";
        package google.protobuf;
        message FieldMask { repeated string paths = 1; }
        message Masked { FieldMask mask = 1; }
        """;

    private static readonly DynamicTypeCatalogue Catalogue = CreateCatalogue();

    private static DynamicMessageType Masked => Catalogue.GetMessageType("google.protobuf.Masked");

    [TestMethod]
    public void FieldMask_RoundTrips_AsOneStringOfLowerCamelCasePaths()
    {
        var message = Parse("""{"mask":"fooBar,baz.quxQuux"}""");

        CollectionAssert.AreEqual(new object[] { "foo_bar", "baz.qux_quux" }, ((DynamicMessage)message.GetValue("mask")).GetRepeated("paths").ToArray());
        Assert.AreEqual("""{"mask":"fooBar,baz.quxQuux"}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void FieldMask_Empty_IsAnEmptyString()
    {
        var message = Parse("""{"mask":""}""");

        Assert.IsEmpty(((DynamicMessage)message.GetValue("mask")).GetRepeated("paths"));
        Assert.AreEqual("""{"mask":""}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void FieldMask_SetWithoutPaths_IsAnEmptyString()
    {
        var message = new DynamicMessage(Masked);
        message.SetValue("mask", new DynamicMessage(Masked.FindField("mask")!.MessageType!));

        Assert.AreEqual("""{"mask":""}""", ProtobufJsonFormatter.Format(message, indented: false));
    }

    [TestMethod]
    public void FieldMask_NotAString_IsRejected() =>
        Assert.AreEqual(ProtobufJsonError.WrongType, ProtobufJsonParser.Parse(Masked, Encoding.UTF8.GetBytes("""{"mask":["a"]}"""), out _));

    private static DynamicMessage Parse(string json)
    {
        Assert.AreEqual(ProtobufJsonError.None, ProtobufJsonParser.Parse(Masked, Encoding.UTF8.GetBytes(json), out var message));
        return message!;
    }

    private static DynamicTypeCatalogue CreateCatalogue()
    {
        Assert.IsTrue(ProtoParser.TryParse(Proto, out var file, out var error), error?.ToString());
        return new DynamicTypeCatalogue([file]);
    }
}
