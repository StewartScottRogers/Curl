using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Formadd;

/// <summary>
/// The formadd cases: every <c>CURLFORM_*</c> option of curl 8.22.0's <c>curl.h</c> and every
/// <c>CURL_FORMADD_*</c> error <c>curl_formadd</c> can give without failing an allocation or being
/// compiled out (<c>CURL_FORMADD_MEMORY</c> and <c>CURL_FORMADD_DISABLED</c> cannot be reached).
/// A <see cref="LibcurlFormaddValueKind.File"/> value names <c>formfile.txt</c>
/// (<see cref="TextFileContent"/>), <c>formfile.json</c> (<see cref="JsonFileContent"/>) or the
/// missing <c>nope.txt</c> in the run's directory.
/// </summary>
public static class LibcurlFormaddCases
{
    /// <summary>The content of <c>formfile.txt</c>.</summary>
    public const string TextFileContent = "contents of the form file\nsecond line\n";

    /// <summary>The content of <c>formfile.json</c>.</summary>
    public const string JsonFileContent = "{\"a\":1}";

    /// <summary><c>CURLFORM_NOTHING</c>.</summary>
    public const int Nothing = 0;

    /// <summary><c>CURLFORM_COPYNAME</c>.</summary>
    public const int CopyName = 1;

    /// <summary><c>CURLFORM_PTRNAME</c>.</summary>
    public const int PtrName = 2;

    /// <summary><c>CURLFORM_NAMELENGTH</c>.</summary>
    public const int NameLength = 3;

    /// <summary><c>CURLFORM_COPYCONTENTS</c>.</summary>
    public const int CopyContents = 4;

    /// <summary><c>CURLFORM_PTRCONTENTS</c>.</summary>
    public const int PtrContents = 5;

    /// <summary><c>CURLFORM_CONTENTSLENGTH</c>.</summary>
    public const int ContentsLength = 6;

    /// <summary><c>CURLFORM_FILECONTENT</c>.</summary>
    public const int FileContent = 7;

    /// <summary><c>CURLFORM_ARRAY</c>.</summary>
    public const int Array = 8;

    /// <summary><c>CURLFORM_OBSOLETE</c>.</summary>
    public const int Obsolete = 9;

    /// <summary><c>CURLFORM_FILE</c>.</summary>
    public const int File = 10;

    /// <summary><c>CURLFORM_BUFFER</c>.</summary>
    public const int Buffer = 11;

    /// <summary><c>CURLFORM_BUFFERPTR</c>.</summary>
    public const int BufferPtr = 12;

    /// <summary><c>CURLFORM_BUFFERLENGTH</c>.</summary>
    public const int BufferLength = 13;

    /// <summary><c>CURLFORM_CONTENTTYPE</c>.</summary>
    public const int ContentType = 14;

    /// <summary><c>CURLFORM_CONTENTHEADER</c>.</summary>
    public const int ContentHeader = 15;

    /// <summary><c>CURLFORM_FILENAME</c>.</summary>
    public const int FileName = 16;

    /// <summary><c>CURLFORM_END</c>.</summary>
    public const int End = 17;

    /// <summary><c>CURLFORM_OBSOLETE2</c>.</summary>
    public const int Obsolete2 = 18;

    /// <summary><c>CURLFORM_STREAM</c>.</summary>
    public const int Stream = 19;

    /// <summary><c>CURLFORM_CONTENTLEN</c>.</summary>
    public const int ContentLen = 20;

    /// <summary>Every case.</summary>
    public static IReadOnlyList<LibcurlFormaddCase> All { get; } = Build();

    private static List<LibcurlFormaddCase> Build() =>
    [
        Case("copyname-copycontents", Call(Text(CopyName, "a"), Text(CopyContents, "v"))),
        Case("ptrname-ptrcontents", Call(Text(PtrName, "a"), Text(PtrContents, "v"))),
        Case("namelength", Call(Text(CopyName, "abcdef"), Length(NameLength, 3), Text(CopyContents, "v"))),
        Case("namelength-ptrname", Call(Text(PtrName, "abcdef"), Length(NameLength, 2), Text(CopyContents, "v"))),
        Case("contentslength-binary", Call(Text(CopyName, "a"), Bytes(PtrContents, "ab\0cd"), Length(ContentsLength, 5))),
        Case("contentslength-short", Call(Text(CopyName, "a"), Text(CopyContents, "hello world"), Length(ContentsLength, 5))),
        Case("contentlen", Call(Text(CopyName, "a"), Bytes(PtrContents, "hello world"), Length(ContentLen, 5))),
        Case("empty-contents", Call(Text(CopyName, "a"), Text(CopyContents, string.Empty))),
        Case("filecontent", Call(Text(CopyName, "f"), FilePath(FileContent, "formfile.txt"))),
        Case("filecontent-missing", Call(Text(CopyName, "f"), FilePath(FileContent, "nope.txt"))),
        Case("file", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"))),
        Case("file-json", Call(Text(CopyName, "f"), FilePath(File, "formfile.json"))),
        Case("file-type", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"), Text(ContentType, "text/x-special"))),
        Case("file-filename", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"), Text(FileName, "shown.txt"))),
        Case("file-two", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"), FilePath(File, "formfile.json"))),
        Case("file-two-types", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"), Text(ContentType, "text/a"), FilePath(File, "formfile.json"), Text(ContentType, "text/b"))),
        Case("file-missing", Call(Text(CopyName, "f"), FilePath(File, "nope.txt"))),
        Case("buffer", Call(Text(CopyName, "f"), Text(Buffer, "buf.bin"), Bytes(BufferPtr, "abc"), Length(BufferLength, 3))),
        Case("buffer-txt", Call(Text(CopyName, "f"), Text(Buffer, "b.txt"), Bytes(BufferPtr, "line\n"), Length(BufferLength, 5))),
        Case("buffer-no-length", Call(Text(CopyName, "f"), Text(Buffer, "b.txt"), Bytes(BufferPtr, "abc"))),
        Case("contenttype", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Text(ContentType, "text/plain"))),
        Case("contentheader", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Headers(ContentHeader, "X-A: 1", "X-B: two"))),
        Case("filename-on-contents", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Text(FileName, "x.txt"))),
        Case("array", Call(Text(CopyName, "a"), Forms(Text(CopyContents, "v"), Text(ContentType, "text/plain")))),
        Case("array-names", Call(Forms(Text(CopyName, "a"), Text(CopyContents, "v")))),
        Case("end-ends-call", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Alone(End), Text(CopyName, "ignored"))),
        Case("quote-name", Call(Text(CopyName, "a\"b"), Text(CopyContents, "v"))),
        Case("unicode-name", Call(Text(CopyName, "é日"), Text(CopyContents, "v"))),
        Case("two-calls", Call(Text(CopyName, "a"), Text(CopyContents, "1")), Call(Text(CopyName, "b"), Text(CopyContents, "2"))),
        Case("failed-then-ok", Call(Text(CopyName, "a")), Call(Text(CopyName, "b"), Text(CopyContents, "2"))),
        Case("ok-then-failed", Call(Text(CopyName, "a"), Text(CopyContents, "1")), Call(Text(CopyContents, "2"))),
        Case("many-fields", [.. Enumerable.Range(0, 20).Select(ManyField)]),
        new("stream", [Call(Text(CopyName, "s"), Alone(Stream, LibcurlFormaddValueKind.Pointer), Length(ContentsLength, 5))], false),
        Case("error-option-twice", Call(Text(CopyName, "a"), Text(CopyName, "b"), Text(CopyContents, "v"))),
        Case("error-contents-twice", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Text(PtrContents, "w"))),
        Case("error-type-twice", Call(Text(CopyName, "a"), Text(CopyContents, "v"), Text(ContentType, "text/a"), Text(ContentType, "text/b"))),
        Case("error-null-name", Call(Text(CopyName, null), Text(CopyContents, "v"))),
        Case("error-null-contents", Call(Text(CopyName, "a"), Text(CopyContents, null))),
        Case("error-null-file", Call(Text(CopyName, "a"), Text(File, null))),
        Case("error-null-array", Call(Text(CopyName, "a"), Alone(Array, LibcurlFormaddValueKind.Text))),
        Case("error-unknown-option", Call(Text(CopyName, "a"), Alone(99), Text(CopyContents, "v"))),
        Case("error-nothing", Call(Alone(Nothing))),
        Case("error-obsolete", Call(Text(CopyName, "a"), Text(Obsolete, "x"))),
        Case("error-obsolete2", Call(Text(CopyName, "a"), Text(Obsolete2, "x"))),
        Case("error-incomplete-no-name", Call(Text(CopyContents, "v"))),
        Case("error-incomplete-no-contents", Call(Text(CopyName, "a"))),
        Case("error-incomplete-file-length", Call(Text(CopyName, "f"), FilePath(File, "formfile.txt"), Length(ContentsLength, 5))),
        Case("error-incomplete-empty", Call()),
        Case("error-illegal-array", Call(Text(CopyName, "a"), Forms(Forms(Text(CopyContents, "v"))))),
    ];

    private static LibcurlFormaddArgument[] ManyField(int index) =>
        Call(Text(CopyName, string.Create(CultureInfo.InvariantCulture, $"p{index}")), Text(CopyContents, string.Create(CultureInfo.InvariantCulture, $"value{index}")));

    private static LibcurlFormaddCase Case(string name, params LibcurlFormaddArgument[][] calls) => new(name, calls, true);

    private static LibcurlFormaddArgument[] Call(params LibcurlFormaddArgument[] arguments) => arguments;

    private static LibcurlFormaddArgument Text(int option, string? text) => new(option, LibcurlFormaddValueKind.Text, text, 0, [], []);

    private static LibcurlFormaddArgument Bytes(int option, string text) =>
        new(option, LibcurlFormaddValueKind.Bytes, Convert.ToHexString(Encoding.UTF8.GetBytes(text)), 0, [], []);

    private static LibcurlFormaddArgument Length(int option, long length) => new(option, LibcurlFormaddValueKind.Length, null, length, [], []);

    private static LibcurlFormaddArgument FilePath(int option, string fileName) => new(option, LibcurlFormaddValueKind.File, fileName, 0, [], []);

    private static LibcurlFormaddArgument Headers(int option, params string[] lines) => new(option, LibcurlFormaddValueKind.Headers, null, 0, lines, []);

    private static LibcurlFormaddArgument Forms(params LibcurlFormaddArgument[] arguments) => new(Array, LibcurlFormaddValueKind.Array, null, 0, [], arguments);

    private static LibcurlFormaddArgument Alone(int option, LibcurlFormaddValueKind kind = LibcurlFormaddValueKind.None) => new(option, kind, null, 0, [], []);
}
