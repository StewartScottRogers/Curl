using static Curl.Protobuf.DynamicMessages.DynamicTestTypes;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>Pins how a <see cref="DynamicMessage" /> holds, checks and clears field values.</summary>
[TestClass]
public sealed class DynamicMessageTests
{
    [TestMethod]
    public void GetValue_UnsetFields_AreTheirDefaults()
    {
        var message = Message("Scalars");

        Assert.AreEqual((0, string.Empty, false, 0.0), (message.GetValue("f_int32"), message.GetValue("f_string"), message.GetValue("f_bool"), message.GetValue("f_double")));
        Assert.IsEmpty((byte[])message.GetValue("f_bytes"));
        Assert.AreEqual(Type("Test1"), ((DynamicMessage)Message("Test3").GetValue("c")).Type);
    }

    [TestMethod]
    public void HasValue_SetUnsetAndCollections_SaysWhetherAnythingIsHeld()
    {
        var message = Message("Maps");
        var repeats = Message("Repeats");
        var before = (message.HasValue("counts"), repeats.HasValue("names"));
        _ = message.GetMap("counts");
        _ = repeats.GetRepeated("names");
        var empty = (message.HasValue("counts"), repeats.HasValue("names"));
        message.GetMap("counts")["a"] = 1;
        repeats.GetRepeated("names").Add("a");

        Assert.AreEqual((false, false), before);
        Assert.AreEqual((false, false), empty);
        Assert.AreEqual((true, true), (message.HasValue("counts"), repeats.HasValue("names")));
    }

    [TestMethod]
    public void ClearValue_SetField_UnsetsIt()
    {
        var message = Message("Presence");
        message.SetValue("count", 3);
        var before = message.HasValue("count");

        message.ClearValue("count");

        Assert.AreEqual((true, false), (before, message.HasValue("count")));
        Assert.AreEqual(string.Empty, EncodeToHex(message));
    }

    [TestMethod]
    public void SetValue_ValueOfAnotherType_Throws()
    {
        var message = Message("Scalars");

        Assert.ThrowsExactly<ArgumentException>(() => message.SetValue("f_int32", 1L));
        Assert.ThrowsExactly<ArgumentException>(() => message.SetValue("f_string", 1));
        Assert.ThrowsExactly<ArgumentException>(() => message.SetValue("f_bytes", "x"));
        Assert.ThrowsExactly<ArgumentException>(() => Message("Test3").SetValue("c", Message("Test2")));
        Assert.ThrowsExactly<ArgumentException>(() => Message("Test3").SetValue("c", 1));
        Assert.ThrowsExactly<ArgumentNullException>(() => message.SetValue("f_int32", null!));
    }

    [TestMethod]
    public void FieldAccess_WrongNameOrShape_Throws()
    {
        var message = Message("Repeats");

        Assert.ThrowsExactly<ArgumentException>(() => message.GetValue("nothing"));
        Assert.ThrowsExactly<ArgumentNullException>(() => message.GetValue(null!));
        Assert.ThrowsExactly<InvalidOperationException>(() => message.GetValue("names"));
        Assert.ThrowsExactly<InvalidOperationException>(() => Message("Test1").GetRepeated("a"));
        Assert.ThrowsExactly<InvalidOperationException>(() => Message("Maps").GetRepeated("counts"));
        Assert.ThrowsExactly<InvalidOperationException>(() => message.GetMap("names"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DynamicMessage(null!));
    }
}
