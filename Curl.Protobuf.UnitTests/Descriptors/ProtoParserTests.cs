namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoParser" /> reads each proto3 construct into descriptors.
/// </summary>
[TestClass]
public sealed class ProtoParserTests
{
    private const string SyntaxLine = "syntax = \"proto3\";\n";

    [TestMethod]
    public void TryParse_SyntaxOnly_ReadsAnEmptyProto3FileWithNoPackage()
    {
        var file = Parse(string.Empty);

        Assert.AreEqual(("proto3", string.Empty), (file.Syntax, file.Package));
        Assert.IsEmpty(file.Options);
        Assert.IsEmpty(file.Messages);
        Assert.IsEmpty(file.Enums);
    }

    [TestMethod]
    public void TryParse_PackageAndEmptyStatementsAndComments_ReadsThePackage()
    {
        var file = Parse("// leading\n; package a.b.c; /* block */ ; // trailing");

        Assert.AreEqual("a.b.c", file.Package);
    }

    [TestMethod]
    public void TryParse_FileOptions_KeepsEachNameAndValueAsWritten()
    {
        var file = Parse(
            "option java_package = \"com.x\";\n" +
            "option (my.opt).x = 5;\n" +
            "option (.full) = -inf;\n" +
            "option a = +5;\n" +
            "option b = -nan;\n" +
            "option c = 1.5;\n" +
            "option d = FOO.BAR;\n" +
            "option e = \"a\" 'b';\n" +
            "option f = { a: { b: 1 } c: 2 };\n" +
            "option g = -7;\n");

        CollectionAssert.AreEqual(
            new[]
            {
                new ProtoOption("java_package", "\"com.x\""),
                new ProtoOption("(my.opt).x", "5"),
                new ProtoOption("(.full)", "-inf"),
                new ProtoOption("a", "+5"),
                new ProtoOption("b", "-nan"),
                new ProtoOption("c", "1.5"),
                new ProtoOption("d", "FOO.BAR"),
                new ProtoOption("e", "\"a\" 'b'"),
                new ProtoOption("f", "{ a: { b: 1 } c: 2 }"),
                new ProtoOption("g", "-7"),
            },
            file.Options.ToArray());
    }

    [TestMethod]
    public void TryParse_EveryScalarType_ReadsEachFieldsType()
    {
        string[] names =
        [
            "double", "float", "int32", "int64", "uint32", "uint64", "sint32", "sint64",
            "fixed32", "fixed64", "sfixed32", "sfixed64", "bool", "string", "bytes",
        ];
        var fields = string.Concat(names.Select((name, index) => $"{name} f_{index} = {index + 1};\n"));

        var message = Parse("message Scalars {\n" + fields + "}").Messages[0];

        CollectionAssert.AreEqual(
            new[]
            {
                ProtoFieldType.Double, ProtoFieldType.Float, ProtoFieldType.Int32, ProtoFieldType.Int64,
                ProtoFieldType.UInt32, ProtoFieldType.UInt64, ProtoFieldType.SInt32, ProtoFieldType.SInt64,
                ProtoFieldType.Fixed32, ProtoFieldType.Fixed64, ProtoFieldType.SFixed32, ProtoFieldType.SFixed64,
                ProtoFieldType.Bool, ProtoFieldType.String, ProtoFieldType.Bytes,
            },
            message.Fields.Select(field => field.Type.Type).ToArray());
        CollectionAssert.AreEqual(names, message.Fields.Select(field => field.Type.Name).ToArray());
    }

    [TestMethod]
    public void TryParse_LabelsAndNamedTypes_ReadsLabelTypeNameNumberAndOptions()
    {
        var message = Parse(
            "message A {\n" +
            "  optional string _private = 1;\n" +
            "  repeated .pkg.Other others = 2 [deprecated = true, (x) = \"y\"];\n" +
            "  Other.Inner inner = 536870911;\n" +
            "}").Messages[0];

        Assert.AreEqual(
            new ProtoFieldDescriptor("_private", 1, ProtoFieldLabel.Optional, new ProtoTypeReference(ProtoFieldType.String, "string"), null, null, null, message.Fields[0].Options),
            message.Fields[0]);
        Assert.IsEmpty(message.Fields[0].Options);
        Assert.AreEqual(
            (ProtoFieldLabel.Repeated, new ProtoTypeReference(ProtoFieldType.Named, ".pkg.Other"), "others", 2),
            (message.Fields[1].Label, message.Fields[1].Type, message.Fields[1].Name, message.Fields[1].Number));
        CollectionAssert.AreEqual(
            new[] { new ProtoOption("deprecated", "true"), new ProtoOption("(x)", "\"y\"") },
            message.Fields[1].Options.ToArray());
        Assert.AreEqual(
            (ProtoFieldLabel.Singular, new ProtoTypeReference(ProtoFieldType.Named, "Other.Inner"), 536870911),
            (message.Fields[2].Label, message.Fields[2].Type, message.Fields[2].Number));
    }

    [TestMethod]
    public void TryParse_MapField_ReadsARepeatedMapWithKeyAndValueTypes()
    {
        var field = Parse("message A { map<string, Project> projects = 3; }").Messages[0].Fields[0];

        Assert.AreEqual(
            new ProtoFieldDescriptor(
                "projects",
                3,
                ProtoFieldLabel.Repeated,
                new ProtoTypeReference(ProtoFieldType.Map, "map<string, Project>"),
                new ProtoTypeReference(ProtoFieldType.String, "string"),
                new ProtoTypeReference(ProtoFieldType.Named, "Project"),
                null,
                field.Options),
            field);
    }

    [TestMethod]
    public void TryParse_Oneof_ReadsItsFieldsIntoTheMessageNamingTheOneof()
    {
        var message = Parse(
            "message A { int32 before = 1; oneof choice { option (o) = 1; ; string a = 2; B b = 3; } }").Messages[0];

        CollectionAssert.AreEqual(
            new[] { (string?)null, "choice", "choice" },
            message.Fields.Select(field => field.OneofName).ToArray());
        Assert.HasCount(1, message.Oneofs);
        Assert.AreEqual("choice", message.Oneofs[0].Name);
        CollectionAssert.AreEqual(new[] { new ProtoOption("(o)", "1") }, message.Oneofs[0].Options.ToArray());
    }

    [TestMethod]
    public void TryParse_NestedMessageEnumOptionAndEmptyStatement_ReadsEachIntoTheMessage()
    {
        var message = Parse(
            "message Outer { ; option deprecated = true; message Inner { message Innermost {} } enum Kind { K = 0; } }").Messages[0];

        Assert.AreEqual("Outer", message.Name);
        CollectionAssert.AreEqual(new[] { new ProtoOption("deprecated", "true") }, message.Options.ToArray());
        Assert.AreEqual("Inner", message.Messages[0].Name);
        Assert.AreEqual("Innermost", message.Messages[0].Messages[0].Name);
        Assert.AreEqual("Kind", message.Enums[0].Name);
    }

    [TestMethod]
    public void TryParse_MessageReserved_ReadsNumbersRangesMaxAndNames()
    {
        var message = Parse("message A { reserved 2, 15, 9 to 11, 40 to max; reserved \"foo\", \"bar\"; }").Messages[0];

        CollectionAssert.AreEqual(
            new[]
            {
                new ProtoReservedRange(2, 2), new ProtoReservedRange(15, 15),
                new ProtoReservedRange(9, 11), new ProtoReservedRange(40, 536870911),
            },
            message.ReservedRanges.ToArray());
        CollectionAssert.AreEqual(new[] { "foo", "bar" }, message.ReservedNames.ToArray());
    }

    [TestMethod]
    public void TryParse_Enum_ReadsValuesOptionsAliasesNegativeNumbersAndReserved()
    {
        var file = Parse(
            "enum E { option allow_alias = true; ; A = 0; B = 0; C = -3 [deprecated = true]; " +
            "reserved -5 to -4, 100 to max; reserved \"X\"; }");

        var enumeration = file.Enums[0];
        Assert.AreEqual("E", enumeration.Name);
        CollectionAssert.AreEqual(new[] { new ProtoOption("allow_alias", "true") }, enumeration.Options.ToArray());
        CollectionAssert.AreEqual(
            new[] { ("A", 0), ("B", 0), ("C", -3) },
            enumeration.Values.Select(value => (value.Name, value.Number)).ToArray());
        CollectionAssert.AreEqual(new[] { new ProtoOption("deprecated", "true") }, enumeration.Values[2].Options.ToArray());
        CollectionAssert.AreEqual(
            new[] { new ProtoReservedRange(-5, -4), new ProtoReservedRange(100, int.MaxValue) },
            enumeration.ReservedRanges.ToArray());
        CollectionAssert.AreEqual(new[] { "X" }, enumeration.ReservedNames.ToArray());
    }

    private static ProtoFileDescriptor Parse(string body)
    {
        var parsed = ProtoParser.TryParse(SyntaxLine + body, out var file, out var error);

        Assert.IsTrue(parsed, error?.ToString());
        Assert.IsNull(error);
        Assert.IsNotNull(file);
        return file;
    }
}
