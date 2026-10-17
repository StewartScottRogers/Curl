using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;
using Curl.Core.FileSystem;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedFormBuilderTests
{
    private const string Head = "Content-Type: multipart/form-data; boundary=b0\r\n\r\n";

    [TestMethod]
    public void Add_CopyNameAndCopyContents_AddsATextPartNamedByTheField()
    {
        ManagedFormBuilder form = NewForm();

        CurlFormAddCode code = form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, "1")]);

        Assert.AreEqual(CurlFormAddCode.Ok, code);
        EasyMimePart part = OnlyPart(form);
        Assert.AreEqual("a", part.Name);
        Assert.AreEqual("1", TextOf(part));
        Assert.IsNull(part.FileName);
        Assert.IsNull(part.Type);
        Assert.IsEmpty(part.Headers);
    }

    [TestMethod]
    public void Add_PointerNameAndPointerContentsBytes_AddsTheBytesUpToTheirFirstNul()
    {
        ManagedFormBuilder form = NewForm();

        CurlFormAddCode code = form.Add([Arg(CurlFormOption.PointerName, "a"), Arg(CurlFormOption.PointerContents, new byte[] { 1, 2, 0, 3 })]);

        Assert.AreEqual(CurlFormAddCode.Ok, code);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, BytesOf(OnlyPart(form)));
    }

    [TestMethod]
    public void Add_ContentsLengthAndContentLength_SendThatManyBytesNulIncluded()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, new byte[] { 1, 0, 2, 3 }), Arg(CurlFormOption.ContentsLength, 3L)]);
        form.Add([Arg(CurlFormOption.CopyName, "b"), Arg(CurlFormOption.CopyContents, "xyz"), Arg(CurlFormOption.ContentLength, 2L)]);
        form.Add([Arg(CurlFormOption.CopyName, "c"), Arg(CurlFormOption.CopyContents, "xy"), Arg(CurlFormOption.ContentsLength, 9L)]);

        IReadOnlyList<EasyMimePart> parts = form.Build().Parts;
        CollectionAssert.AreEqual(new byte[] { 1, 0, 2 }, BytesOf(parts[0]));
        Assert.AreEqual("xy", TextOf(parts[1]));
        Assert.AreEqual("xy", TextOf(parts[2]));
    }

    [TestMethod]
    public void Add_NameLength_SendsThatManyBytesOfTheName()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "abc"), Arg(CurlFormOption.NameLength, 2L), Arg(CurlFormOption.CopyContents, "1")]);
        form.Add([Arg(CurlFormOption.CopyName, "d\0e"), Arg(CurlFormOption.CopyContents, "1")]);
        form.Add([Arg(CurlFormOption.CopyName, "f"), Arg(CurlFormOption.NameLength, 5L), Arg(CurlFormOption.CopyContents, "1")]);

        CollectionAssert.AreEqual(new[] { "ab", "d", "f" }, form.Build().Parts.Select(part => part.Name).ToArray());
    }

    [TestMethod]
    public void Add_NulWithinTheNameLength_AnswersNullAndAddsNothing()
    {
        ManagedFormBuilder form = NewForm();

        CurlFormAddCode code = form.Add([Arg(CurlFormOption.CopyName, "a\0b"), Arg(CurlFormOption.NameLength, 3L), Arg(CurlFormOption.CopyContents, "1")]);

        Assert.AreEqual(CurlFormAddCode.Null, code);
        Assert.IsEmpty(form.Build().Parts);
    }

    [TestMethod]
    public void Add_FileContent_SendsTheFileAsContentWithNoFileNameOrGuessedType()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.FileContent, "dir/x.txt"), Arg(CurlFormOption.FileName, "shown")]);

        EasyMimePart part = OnlyPart(form);
        Assert.AreEqual(new EasyMimeFile("dir/x.txt"), part.Data);
        Assert.IsNull(part.FileName);
        Assert.IsNull(part.Type);
    }

    [TestMethod]
    public void Add_File_UploadsTheFileUnderItsBaseNameWithTheTypeItsNameEndsIn()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.File, "dir/x.TXT")]);
        form.Add([Arg(CurlFormOption.CopyName, "b"), Arg(CurlFormOption.File, "dir/y.bin")]);
        form.Add([Arg(CurlFormOption.CopyName, "c"), Arg(CurlFormOption.File, "dir/z.png"), Arg(CurlFormOption.ContentType, "text/x"), Arg(CurlFormOption.FileName, "shown")]);

        IReadOnlyList<EasyMimePart> parts = form.Build().Parts;
        Assert.AreEqual(new EasyMimeFile("dir/x.TXT"), parts[0].Data);
        Assert.AreEqual("x.TXT", parts[0].FileName);
        Assert.AreEqual("text/plain", parts[0].Type);
        Assert.AreEqual("application/octet-stream", parts[1].Type);
        Assert.AreEqual("text/x", parts[2].Type);
        Assert.AreEqual("shown", parts[2].FileName);
    }

    [TestMethod]
    public void Add_FileDash_ReadsStandardInputWithNoFileName()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.File, "-")]);

        EasyMimePart part = OnlyPart(form);
        Assert.IsInstanceOfType<EasyMimeStandardInput>(part.Data);
        Assert.IsNull(part.FileName);
        Assert.AreEqual("application/octet-stream", part.Type);
    }

    [TestMethod]
    public void Add_SeveralFiles_NestsThemAsSubPartsTypedFromTheFileBeforeWhenUnknown()
    {
        ManagedFormBuilder form = NewForm();

        CurlFormAddCode code = form.Add(
        [
            Arg(CurlFormOption.CopyName, "a"),
            Arg(CurlFormOption.File, "x.txt"),
            Arg(CurlFormOption.ContentHeader, new[] { "X-A: 1" }),
            Arg(CurlFormOption.File, "y.bin"),
            Arg(CurlFormOption.ContentType, "text/y"),
            Arg(CurlFormOption.ContentType, "text/z"),
            Arg(CurlFormOption.File, "z.bin"),
            Arg(CurlFormOption.ContentType, "text/w"),
        ]);

        Assert.AreEqual(CurlFormAddCode.Ok, code);
        EasyMimePart field = OnlyPart(form);
        Assert.AreEqual("a", field.Name);
        IReadOnlyList<EasyMimePart> files = field.SubParts!.Parts;
        CollectionAssert.AreEqual(new[] { "text/plain", "text/y", "text/z", "text/w" }, files.Select(file => file.Type).ToArray());
        CollectionAssert.AreEqual(new[] { "x.txt", "y.bin", "z.bin", null }, files.Select(file => file.FileName).ToArray());
        Assert.IsTrue(files.All(file => file.Name is null));
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, files[0].Headers.ToArray());
        Assert.AreEqual(new EasyMimeFile("z.bin"), files[2].Data);
        Assert.IsNull(files[3].Data);
    }

    [TestMethod]
    public void Add_SeveralFilesWithAFileName_SendsEveryFileUnderItTypedLikeTheFileBefore()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.File, "x.bin"), Arg(CurlFormOption.ContentType, "text/q"), Arg(CurlFormOption.FileName, "shown"), Arg(CurlFormOption.File, "y.bin")]);

        IReadOnlyList<EasyMimePart> files = OnlyPart(form).SubParts!.Parts;
        CollectionAssert.AreEqual(new[] { "shown", "shown" }, files.Select(file => file.FileName).ToArray());
        CollectionAssert.AreEqual(new[] { "text/q", "text/q" }, files.Select(file => file.Type).ToArray());
    }

    [TestMethod]
    public void Add_BufferPointerAndBuffer_UploadsTheBufferUnderItsNameAndGuessedType()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.Buffer, "p.png"), Arg(CurlFormOption.BufferPointer, new byte[] { 1, 0, 2 }), Arg(CurlFormOption.BufferLength, 3L)]);
        form.Add([Arg(CurlFormOption.CopyName, "b"), Arg(CurlFormOption.BufferPointer, new byte[] { 1, 0, 2 }), Arg(CurlFormOption.Buffer, "q")]);
        form.Add([Arg(CurlFormOption.CopyName, "c"), Arg(CurlFormOption.BufferPointer, new byte[] { 4 })]);

        IReadOnlyList<EasyMimePart> parts = form.Build().Parts;
        CollectionAssert.AreEqual(new byte[] { 1, 0, 2 }, BytesOf(parts[0]));
        Assert.AreEqual("p.png", parts[0].FileName);
        Assert.AreEqual("image/png", parts[0].Type);
        CollectionAssert.AreEqual(new byte[] { 1 }, BytesOf(parts[1]));
        Assert.AreEqual("application/octet-stream", parts[1].Type);
        Assert.IsNull(parts[2].FileName);
    }

    [TestMethod]
    public void Add_Stream_AddsAStreamPartOfTheContentsLengthUnderItsFileName()
    {
        ManagedFormBuilder form = NewForm();
        object stream = new();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.Stream, stream), Arg(CurlFormOption.FileName, "s"), Arg(CurlFormOption.ContentsLength, 4L)]);

        EasyMimePart part = OnlyPart(form);
        Assert.AreEqual(new FormStreamData(4, stream), part.Data);
        Assert.AreEqual("s", part.FileName);
        Assert.IsNull(part.Type);
    }

    [TestMethod]
    public void Add_TextWithAFileName_SendsNoFileName()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, "1"), Arg(CurlFormOption.FileName, "f"), Arg(CurlFormOption.ContentHeader, null)]);

        Assert.IsNull(OnlyPart(form).FileName);
    }

    [TestMethod]
    public void Add_Array_ReadsItsOptionsUpToItsEndThenTheCallsUpToTheirEnd()
    {
        ManagedFormBuilder form = NewForm();

        CurlFormAddCode code = form.Add(
        [
            Arg(CurlFormOption.Array, new CurlFormArgument[] { Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.End, null), Arg(CurlFormOption.Obsolete, null) }),
            Arg(CurlFormOption.Array, new CurlFormArgument[] { Arg(CurlFormOption.CopyContents, "1") }),
            Arg(CurlFormOption.End, null),
            Arg(CurlFormOption.Obsolete2, null),
        ]);

        Assert.AreEqual(CurlFormAddCode.Ok, code);
        Assert.AreEqual("1", TextOf(OnlyPart(form)));
    }

    [TestMethod]
    public void Add_ArrayInAnArrayOrNoArray_AnswersIllegalArrayOrNull()
    {
        CurlFormArgument[] nested = [Arg(CurlFormOption.Array, Array.Empty<CurlFormArgument>())];

        Assert.AreEqual(CurlFormAddCode.IllegalArray, NewForm().Add([Arg(CurlFormOption.Array, nested)]));
        Assert.AreEqual(CurlFormAddCode.Null, NewForm().Add([Arg(CurlFormOption.Array, null)]));
    }

    [TestMethod]
    public void Add_OptionWithNoUse_AnswersUnknownOption()
    {
        Assert.AreEqual(CurlFormAddCode.UnknownOption, NewForm().Add([Arg(CurlFormOption.Nothing, null)]));
        Assert.AreEqual(CurlFormAddCode.UnknownOption, NewForm().Add([Arg((CurlFormOption)99, null)]));
    }

    [TestMethod]
    public void Add_OptionGivenTwiceForOneNode_AnswersOptionTwice()
    {
        CurlFormArgument name = Arg(CurlFormOption.CopyName, "a");
        CurlFormArgument contents = Arg(CurlFormOption.CopyContents, "1");

        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(name, Arg(CurlFormOption.PointerName, "b")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.NameLength, 1L), Arg(CurlFormOption.NameLength, 1L)));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(contents, Arg(CurlFormOption.PointerContents, "2")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(contents, Arg(CurlFormOption.File, "x")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.PointerContents, "1"), Arg(CurlFormOption.FileContent, "x")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.FileContent, "x"), Arg(CurlFormOption.FileContent, "y")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.BufferPointer, new byte[1]), Arg(CurlFormOption.BufferPointer, new byte[1])));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.BufferLength, 1L), Arg(CurlFormOption.BufferLength, 1L)));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.Stream, "s"), Arg(CurlFormOption.Stream, "t")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.ContentType, "t"), Arg(CurlFormOption.ContentType, "u")));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.ContentHeader, new[] { "X: 1" }), Arg(CurlFormOption.ContentHeader, new[] { "Y: 1" })));
        Assert.AreEqual(CurlFormAddCode.OptionTwice, Code(Arg(CurlFormOption.FileName, "f"), Arg(CurlFormOption.Buffer, "g")));
    }

    [TestMethod]
    public void Add_OptionWithNoValue_AnswersNull()
    {
        CurlFormArgument file = Arg(CurlFormOption.File, "x");

        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.CopyName, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.CopyContents, 5)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.FileContent, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.File, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(file, Arg(CurlFormOption.File, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.BufferPointer, "not bytes")));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.Stream, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.ContentType, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(file, Arg(CurlFormOption.ContentType, "t"), Arg(CurlFormOption.ContentType, null)));
        Assert.AreEqual(CurlFormAddCode.Null, Code(Arg(CurlFormOption.FileName, null)));
    }

    [TestMethod]
    public void Add_FieldMissingANameOrValueOrAFileWithALength_AnswersIncomplete()
    {
        CurlFormArgument name = Arg(CurlFormOption.CopyName, "a");
        CurlFormArgument length = Arg(CurlFormOption.ContentsLength, 1L);

        Assert.AreEqual(CurlFormAddCode.Incomplete, Code(Arg(CurlFormOption.CopyContents, "1")));
        Assert.AreEqual(CurlFormAddCode.Incomplete, Code(name));
        Assert.AreEqual(CurlFormAddCode.Incomplete, Code(name, Arg(CurlFormOption.File, "x"), length));
        Assert.AreEqual(CurlFormAddCode.Incomplete, Code(name, Arg(CurlFormOption.File, "x"), Arg(CurlFormOption.File, "y"), length));
    }

    [TestMethod]
    public void Add_FailedCall_AddsNothingAndLaterCallsStillAdd()
    {
        ManagedFormBuilder form = NewForm();

        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.Nothing, null)]);
        form.Add([Arg(CurlFormOption.CopyName, "b"), Arg(CurlFormOption.CopyContents, "2")]);

        Assert.AreEqual("b", OnlyPart(form).Name);
    }

    [TestMethod]
    public void Serialize_TwoFields_HandsOverTheContentTypeLineThenTheBodyInOnePiece()
    {
        ManagedFormBuilder form = NewForm();
        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, "1")]);
        form.Add([Arg(CurlFormOption.CopyName, "b"), Arg(CurlFormOption.CopyContents, "2"), Arg(CurlFormOption.ContentType, "text/x")]);
        List<byte[]> pieces = [];

        int code = form.Serialize(piece => Take(pieces, piece));

        Assert.AreEqual(0, code);
        Assert.HasCount(1, pieces);
        Assert.AreEqual(
            Head
            + "--b0\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n"
            + "--b0\r\nContent-Disposition: form-data; name=\"b\"\r\nContent-Type: text/x\r\n\r\n2\r\n--b0--\r\n",
            Encoding.UTF8.GetString(pieces[0]));
    }

    [TestMethod]
    public void Serialize_FileField_ReadsTheFileIntoTheBody()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "f.txt");
            File.WriteAllText(path, "hello");
            ManagedFormBuilder form = NewForm();
            form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.File, path)]);
            List<byte[]> pieces = [];

            int code = form.Serialize(piece => Take(pieces, piece));

            Assert.AreEqual(0, code);
            Assert.AreEqual(
                Head + "--b0\r\nContent-Disposition: form-data; name=\"a\"; filename=\"f.txt\"\r\nContent-Type: text/plain\r\n\r\nhello\r\n--b0--\r\n",
                Encoding.UTF8.GetString(pieces[0]));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Serialize_FormPastOnePiece_HandsOverFullPiecesThenTheRest()
    {
        ManagedFormBuilder form = NewForm();
        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, new string('x', 2 * ManagedFormBuilder.PieceSize))]);
        List<byte[]> pieces = [];

        int code = form.Serialize(piece => Take(pieces, piece));

        Assert.AreEqual(0, code);
        Assert.HasCount(3, pieces);
        Assert.HasCount(ManagedFormBuilder.PieceSize, pieces[0]);
        Assert.HasCount(ManagedFormBuilder.PieceSize, pieces[1]);
        StringAssert.StartsWith(Encoding.UTF8.GetString(pieces[0]), Head + "--b0\r\n");
        StringAssert.EndsWith(Encoding.UTF8.GetString(pieces[2]), "x\r\n--b0--\r\n");
    }

    [TestMethod]
    public void Serialize_CallbackTakesFewerBytes_AnswersReadError()
    {
        ManagedFormBuilder form = NewForm();
        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.CopyContents, "1")]);

        int code = form.Serialize(piece => piece.Length - 1);

        Assert.AreEqual((int)CurlCode.ReadError, code);
    }

    [TestMethod]
    public void Serialize_FileThatDoesNotExist_AnswersReadErrorAndHandsOverNothing()
    {
        ManagedFormBuilder form = NewForm();
        form.Add([Arg(CurlFormOption.CopyName, "a"), Arg(CurlFormOption.File, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "none"))]);
        List<byte[]> pieces = [];

        int code = form.Serialize(piece => Take(pieces, piece));

        Assert.AreEqual((int)CurlCode.ReadError, code);
        Assert.IsEmpty(pieces);
    }

    [TestMethod]
    public void Serialize_EmptyForm_HandsOverTheContentTypeLineFirst()
    {
        List<byte[]> pieces = [];

        int code = NewForm().Serialize(piece => Take(pieces, piece));

        Assert.AreEqual(0, code);
        StringAssert.StartsWith(Encoding.UTF8.GetString(pieces[0]), Head);
    }

    private static ManagedFormBuilder NewForm() => new(new PhysicalFileSystem(), () => "b0");

    private static CurlFormArgument Arg(CurlFormOption option, object? value) => new(option, value);

    private static CurlFormAddCode Code(params CurlFormArgument[] arguments) => NewForm().Add(arguments);

    private static EasyMimePart OnlyPart(ManagedFormBuilder form) => form.Build().Parts.Single();

    private static byte[] BytesOf(EasyMimePart part) => ((EasyMimeBytes)part.Data!).Bytes.ToArray();

    private static string TextOf(EasyMimePart part) => Encoding.UTF8.GetString(BytesOf(part));

    private static long Take(List<byte[]> pieces, ReadOnlySpan<byte> piece)
    {
        pieces.Add(piece.ToArray());
        return piece.Length;
    }
}
