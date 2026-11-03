using Curl.Protobuf.Descriptors;
using static Curl.Protobuf.DynamicMessages.DynamicTestTypes;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>Pins how <see cref="DynamicTypeCatalogue" /> resolves the types a message's fields name.</summary>
[TestClass]
public sealed class DynamicTypeCatalogueTests
{
    [TestMethod]
    public void GetMessageType_NestedRelativeAbsoluteAndRecursiveNames_Resolve()
    {
        var outer = Type("Outer");

        Assert.AreEqual("test.Outer.Inner", outer.FindField("inner")!.MessageType!.FullName);
        Assert.AreEqual("Mood", outer.FindField("mood")!.EnumType!.Name);
        Assert.AreEqual(DynamicValueKind.Enum, outer.FindField(1)!.MessageType!.FindField("color")!.Kind);
        Assert.AreEqual(Type("Test1"), outer.FindField("absolute")!.MessageType);
        Assert.AreEqual(outer, outer.FindField("recursive")!.MessageType);
        Assert.AreSame(outer, Catalogue.GetMessageType(".test.Outer"));
        Assert.IsNull(outer.FindField(99));
    }

    [TestMethod]
    public void GetMessageType_MapField_HasAnEntryTypeWithKeyAndValue()
    {
        var items = Type("Maps").FindField("items")!;

        Assert.AreEqual((true, true, false), (items.IsMap, items.IsRepeated, items.IsPacked));
        Assert.AreEqual("test.Maps.itemsEntry", items.MessageType!.FullName);
        Assert.AreEqual((DynamicValueKind.Int32, DynamicValueKind.Message), (items.MessageType.FindField("key")!.Kind, items.MessageType.FindField("value")!.Kind));
    }

    [TestMethod]
    public void Fields_PresenceAndPacking_FollowLabelsOneofsAndOptions()
    {
        var presence = Type("Presence");
        var repeats = Type("Repeats");

        Assert.AreEqual((true, true, true), (presence.FindField("count")!.HasPresence, presence.FindField("text")!.HasPresence, presence.FindField("message")!.HasPresence));
        Assert.IsFalse(Type("Test1").FindField("a")!.HasPresence);
        Assert.AreEqual((true, false, false), (repeats.FindField("packed")!.IsPacked, repeats.FindField("unpacked")!.IsPacked, repeats.FindField("names")!.IsPacked));
        Assert.IsFalse(Type("Test1").FindField("a")!.IsPacked);
        Assert.IsTrue(repeats.FindField("marked")!.IsPacked);
    }

    [TestMethod]
    public void GetMessageType_UnknownNames_Throw()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Catalogue.GetMessageType("test.Nothing"));
        Assert.ThrowsExactly<ArgumentException>(() => Catalogue.GetMessageType("test.Broken"));
        Assert.ThrowsExactly<ArgumentException>(() => Catalogue.GetMessageType("test.Broken"));
        Assert.ThrowsExactly<ArgumentNullException>(() => Catalogue.GetMessageType(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DynamicTypeCatalogue(null!));
    }

    [TestMethod]
    public void TryGetMessageType_KnownAndUnknownNames_ResolveOrReturnFalse()
    {
        Assert.IsTrue(Catalogue.TryGetMessageType("test.Outer", out var outer));
        Assert.AreSame(Type("Outer"), outer);
        Assert.AreSame(Catalogue, outer!.Catalogue);
        Assert.AreSame(Catalogue, outer.FindField("inner")!.MessageType!.Catalogue);
        Assert.IsFalse(Catalogue.TryGetMessageType("test.Nothing", out var nothing));
        Assert.IsNull(nothing);
        Assert.IsFalse(Catalogue.TryGetMessageType("test.Broken", out _));
    }

    [TestMethod]
    public void GetMessageType_FileWithNoPackage_ResolvesFromTheTopScope()
    {
        Assert.IsTrue(ProtoParser.TryParse("syntax = \"proto3\"; message A { B b = 1; } message B { }", out var file, out _));

        var type = new DynamicTypeCatalogue([file]).GetMessageType("A");

        Assert.AreEqual("B", type.FindField("b")!.MessageType!.FullName);
    }
}
