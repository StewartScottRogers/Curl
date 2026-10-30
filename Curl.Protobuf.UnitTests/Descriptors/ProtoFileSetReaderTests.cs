using System.Diagnostics.CodeAnalysis;

namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoFileSetReader" /> reads imports through an <see cref="IProtoFileReader" />
/// and resolves named types across them.
/// </summary>
[TestClass]
public sealed class ProtoFileSetReaderTests
{
    private const string SyntaxLine = "syntax = \"proto3\";\n";

    [TestMethod]
    public void TryRead_NullArguments_Throw()
    {
        var reader = new InMemoryProtoFileReader();

        Assert.ThrowsExactly<ArgumentNullException>(() => ProtoFileSetReader.TryRead(null!, reader, out _, out _));
        Assert.ThrowsExactly<ArgumentNullException>(() => ProtoFileSetReader.TryRead("a.proto", null!, out _, out _));
    }

    [TestMethod]
    public void TryRead_ImportedTypes_ResolveToTheirFullNamesAndFiles()
    {
        var files = Read(
            ("main.proto",
                "package app;\nimport \"common.proto\";\n" +
                "message Request { lib.Id id = 1; Outer.Inner inner = 2; map<string, .lib.Kind> kinds = 3; int32 n = 4; }\n" +
                "message Outer { message Inner { Inner self = 1; Request request = 2; } }\n" +
                "service Api { rpc Get (Request) returns (stream lib.Id); }\n"),
            ("common.proto", "package lib;\nmessage Id { Kind kind = 1; }\nenum Kind { KIND_UNSPECIFIED = 0; }\n"));

        CollectionAssert.AreEqual(new[] { "common.proto", "main.proto" }, files.Files.Select(file => file.Path).ToArray());
        Assert.AreEqual("main.proto", files.Root.Path);
        CollectionAssert.AreEquivalent(
            new[] { ".lib.Id", ".lib.Kind", ".app.Request", ".app.Outer", ".app.Outer.Inner" },
            files.Types.Keys.ToArray());
        var request = files.Root.Descriptor.Messages[0];
        Assert.AreEqual(".lib.Id", files.FindType(request.Fields[0].Type)?.FullName);
        Assert.AreEqual(".app.Outer.Inner", files.FindType(request.Fields[1].Type)?.FullName);
        Assert.AreEqual(".lib.Kind", files.FindType(request.Fields[2].MapValue!)?.FullName);
        Assert.IsNull(files.FindType(request.Fields[2].Type));
        Assert.IsNull(files.FindType(request.Fields[3].Type));
        var inner = files.Root.Descriptor.Messages[1].Messages[0];
        Assert.AreEqual(".app.Outer.Inner", files.FindType(inner.Fields[0].Type)?.FullName);
        Assert.AreEqual(".app.Request", files.FindType(inner.Fields[1].Type)?.FullName);
        var method = files.Root.Descriptor.Services[0].Methods[0];
        Assert.AreSame(request, files.FindType(method.InputType)?.Message);
        var id = files.FindType(method.OutputType);
        Assert.AreEqual(("common.proto", (ProtoEnumDescriptor?)null), (id?.Path, id?.Enum));
        Assert.AreEqual("Kind", files.Types[".lib.Kind"].Enum?.Name);
    }

    [TestMethod]
    public void TryRead_PublicImportsAtAnyDepth_AreVisibleAndADiamondIsReadOnce()
    {
        var reader = new InMemoryProtoFileReader(
            ("main.proto", "import \"left.proto\";\nimport weak \"right.proto\";\nmessage M { Deep d = 1; }\n"),
            ("left.proto", "import public \"middle.proto\";\n"),
            ("right.proto", "import public \"middle.proto\";\n"),
            ("middle.proto", "import public \"deep.proto\";\n"),
            ("deep.proto", "message Deep {}\n"));

        var read = ProtoFileSetReader.TryRead("main.proto", reader, out var files, out var error);

        Assert.IsTrue(read, error?.Message);
        CollectionAssert.AreEqual(
            new[] { "deep.proto", "middle.proto", "left.proto", "right.proto", "main.proto" },
            files!.Files.Select(file => file.Path).ToArray());
        CollectionAssert.AreEqual(new[] { "main.proto", "left.proto", "middle.proto", "deep.proto", "right.proto" }, reader.Reads);
    }

    [TestMethod]
    public void TryRead_TypeOfAFileImportedWithoutPublic_IsNotVisibleTwoImportsAway()
    {
        var error = ReadError(
            ("main.proto", "import \"middle.proto\";\nmessage M {\n  Deep d = 1;\n}\n"),
            ("middle.proto", "import \"deep.proto\";\n"),
            ("deep.proto", "message Deep {}\n"));

        Assert.AreEqual(new ProtoFileError("main.proto", ProtoFileErrorKind.UnresolvedType, 4, 3, "Type \"Deep\" is not defined."), error);
    }

    [TestMethod]
    public void TryRead_MissingRootFile_FailsWithFileNotFound()
    {
        var error = ReadError("main.proto", ("other.proto", string.Empty));

        Assert.AreEqual(new ProtoFileError("main.proto", ProtoFileErrorKind.FileNotFound, 0, 0, "File \"main.proto\" was not found."), error);
    }

    [TestMethod]
    public void TryRead_MissingImport_FailsAtTheImportStatement()
    {
        var error = ReadError(("main.proto", "package p;\n  import public \"gone.proto\";\n"));

        Assert.AreEqual(new ProtoFileError("main.proto", ProtoFileErrorKind.MissingImport, 3, 3, "Import \"gone.proto\" was not found."), error);
    }

    [TestMethod]
    public void TryRead_ImportCycle_FailsAtTheImportThatClosesIt()
    {
        var error = ReadError(
            ("a.proto", "import \"b.proto\";\n"),
            ("b.proto", "\nimport \"a.proto\";\n"));

        Assert.AreEqual(new ProtoFileError("b.proto", ProtoFileErrorKind.ImportCycle, 3, 1, "Import \"a.proto\" leads back to a file that imports it."), error);
    }

    [TestMethod]
    public void TryRead_FileImportingItself_FailsAsACycle()
    {
        var error = ReadError(("a.proto", "import \"a.proto\";\n"));

        Assert.AreEqual(ProtoFileErrorKind.ImportCycle, error.Kind);
    }

    [TestMethod]
    public void TryRead_MalformedImportedFile_FailsWithItsPathAndPosition()
    {
        var error = ReadError(("a.proto", "import \"b.proto\";\n"), ("b.proto", "message {}\n"));

        Assert.AreEqual(new ProtoFileError("b.proto", ProtoFileErrorKind.Syntax, 2, 9, "Expected a message name, found \"{\"."), error);
    }

    [TestMethod]
    public void TryRead_MethodNamingAnEnum_FailsWithNotAMessage()
    {
        var error = ReadError(("a.proto", "enum E { E_ZERO = 0; }\nmessage M {}\nservice S { rpc Get (M) returns (E); }\n"));

        Assert.AreEqual(new ProtoFileError("a.proto", ProtoFileErrorKind.NotAMessage, 4, 34, "Type \"E\" is an enum, not a message."), error);
    }

    [TestMethod]
    public void TryRead_UnknownAbsoluteName_FailsWithUnresolvedType()
    {
        var error = ReadError(("a.proto", "package p;\nmessage M { .M m = 1; }\n"));

        Assert.AreEqual(new ProtoFileError("a.proto", ProtoFileErrorKind.UnresolvedType, 3, 13, "Type \".M\" is not defined."), error);
    }

    private static ProtoFileSet Read(params (string Path, string Body)[] files)
    {
        var read = ProtoFileSetReader.TryRead(files[0].Path, new InMemoryProtoFileReader(files), out var set, out var error);
        Assert.IsTrue(read, error?.Message);
        return set!;
    }

    private static ProtoFileError ReadError(params (string Path, string Body)[] files)
    {
        return ReadError(files[0].Path, files);
    }

    private static ProtoFileError ReadError(string path, params (string Path, string Body)[] files)
    {
        var read = ProtoFileSetReader.TryRead(path, new InMemoryProtoFileReader(files), out var set, out var error);
        Assert.IsFalse(read);
        Assert.IsNull(set);
        return error!;
    }

    /// <summary>Serves <c>.proto</c> files from memory, each with the syntax line before its body, and records each path read.</summary>
    private sealed class InMemoryProtoFileReader(params (string Path, string Body)[] files) : IProtoFileReader
    {
        public List<string> Reads { get; } = [];

        public bool TryReadFile(string path, [NotNullWhen(true)] out string? text)
        {
            Reads.Add(path);
            var found = files.Where(file => file.Path == path).Select(file => SyntaxLine + file.Body).ToList();
            text = found.FirstOrDefault();
            return text is not null;
        }
    }
}
