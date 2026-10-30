using static Curl.Protobuf.Json.JsonTestTypes;

namespace Curl.Protobuf.Json;

/// <summary>Pins <see cref="ProtobufJsonNames" />, the JSON names of fields.</summary>
[TestClass]
public sealed class ProtobufJsonNamesTests
{
    [TestMethod]
    [DataRow("f_sfixed64", "fSfixed64")]
    [DataRow("renamed", "custom")]
    [DataRow("next", "next")]
    public void JsonName_Field_IsLowerCamelCaseOrTheJsonNameOption(string name, string expected)
    {
        var type = name == "next" ? Type("Node") : Type("Scalars");

        Assert.AreEqual(expected, ProtobufJsonNames.JsonName(type.FindField(name)!));
    }

    [TestMethod]
    public void FindField_NoSuchMember_IsNull()
    {
        Assert.IsNull(ProtobufJsonNames.FindField(Type("Scalars"), "missing"));
    }

    [TestMethod]
    public void JsonNameAndFindField_NullArgument_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProtobufJsonNames.JsonName(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ProtobufJsonNames.FindField(null!, "x"));
    }
}
