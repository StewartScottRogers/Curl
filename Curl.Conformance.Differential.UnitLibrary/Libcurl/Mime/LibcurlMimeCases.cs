using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>
/// The tester's <c>mimetest.py</c> cases, in its order, rebuilt from its case list (its
/// <c>refused</c> entry, which it skipped, is left out). A <c>file</c> step names
/// <c>mimefile.txt</c> (<see cref="TextFileContent"/>), <c>mimefile.json</c>
/// (<see cref="JsonFileContent"/>) or the missing <c>nope.txt</c> in the run's directory.
/// </summary>
public static class LibcurlMimeCases
{
    /// <summary>The content the tester wrote to <c>mimefile.txt</c>.</summary>
    public const string TextFileContent = "contents of the mime file\nsecond line\n";

    /// <summary>The content the tester wrote to <c>mimefile.json</c>.</summary>
    public const string JsonFileContent = "{\"a\":1}";

    private const string EncodedText = "héllo wörld=\r\nline2 with trailing space \r\n";

    private static readonly string?[] Encoders = ["base64", "quoted-printable", "7bit", "8bit", "binary", "BASE64", "Quoted-Printable", "nonsense", "", null];

    /// <summary>Every case.</summary>
    public static IReadOnlyList<LibcurlMimeCase> All { get; } = Build();

    private static List<LibcurlMimeCase> Build()
    {
        var cases = new List<LibcurlMimeCase>
        {
            Case("simple", Part(Name("a"), Data("v"))),
            Case("two", Part(Name("a"), Data("v1")), Part(Name("b"), Data("v2"))),
            Case("file", Part(Name("f"), FileName("x.txt"), Type("text/plain"), Data("file body\n"))),
            Case("filedata", Part(Name("f"), File("mimefile.txt"))),
            Case("filedata-missing", Part(Name("f"), File("nope.txt"))),
            Case("filedata-fn-override", Part(Name("f"), FileName("other.bin"), File("mimefile.txt"))),
            Case("filedata-type-override", Part(Name("f"), Type("application/x-foo"), File("mimefile.txt"))),
            Case("filedata-json", Part(Name("f"), File("mimefile.json"))),
            Case("no-name", Part(Data("v"))),
            Case("null-name", Part(Name(null), Data("v"))),
            Case("empty-name", Part(Name(""), Data("v"))),
            Case("quote-name", Part(Name("a\"b"), Data("v"))),
            Case("quote-filename", Part(Name("f"), FileName("a\"b.txt"), Data("v"))),
            Case("backslash-filename", Part(Name("f"), FileName("a\\b.txt"), Data("v"))),
            Case("crlf-name", Part(Name("a\r\nX: y"), Data("v"))),
            Case("unicode-name", Part(Name("é日"), Data("v"))),
            Case("unicode-filename", Part(Name("f"), FileName("é日.txt"), Data("v"))),
            Case("path-filename", Part(Name("f"), FileName("C:\\dir\\file.txt"), Data("v"))),
            Case("slash-filename", Part(Name("f"), FileName("/dir/file.txt"), Data("v"))),
            Case("empty-filename", Part(Name("f"), FileName(""), Data("v"))),
            Case("null-filename", Part(Name("f"), FileName(null), Data("v"))),
            Case("type-params", Part(Name("f"), Type("text/plain; charset=utf-8"), Data("v"))),
            Case("type-empty", Part(Name("f"), Type(""), Data("v"))),
            Case("type-null", Part(Name("f"), Type(null), Data("v"))),
            Case("type-bad", Part(Name("f"), Type("not a type"), Data("v"))),
            Case("no-data", Part(Name("a"))),
            Case("null-data", Part(Name("a"), DataHex(null))),
            Case("empty-data", Part(Name("a"), DataHex(string.Empty))),
            Case("binary-data", Part(Name("a"), DataHex(EveryByte()))),
            Case("crlf-data", Part(Name("a"), Data("line1\r\nline2\nline3\r\n"))),
            Case("boundary-like-data", Part(Name("a"), Data("--------------------------abcdef0123456789\r\n"))),
            Case("big-data", Part(Name("a"), Data(string.Concat(Enumerable.Repeat("ab", 60000))))),
        };
        foreach (var encoder in Encoders)
        {
            cases.Add(Case("enc-" + (encoder ?? "None"), Part(Name("a"), Encoder(encoder), Data(EncodedText + new string('x', 100)))));
        }

        cases.AddRange(
        [
            Case("enc-b64-bin", Part(Name("a"), Encoder("base64"), DataHex(EveryByte()))),
            Case("enc-qp-bin", Part(Name("a"), Encoder("quoted-printable"), DataHex(EveryByte()))),
            Case("enc-7bit-8bit-data", Part(Name("a"), Encoder("7bit"), Data("héllo"))),
            Case("enc-b64-file", Part(Name("f"), Encoder("base64"), File("mimefile.txt"))),
            Case("enc-qp-file", Part(Name("f"), Encoder("quoted-printable"), File("mimefile.txt"))),
            Case("headers", Part(Name("a"), Data("v"), Headers("X-Custom: 1", "X-Other: two"))),
            Case("headers-override-type", Part(Name("a"), Data("v"), Headers("Content-Type: text/special"))),
            Case("headers-override-disp", Part(Name("a"), Data("v"), Headers("Content-Disposition: attachment"))),
            Case("headers-bad", Part(Name("a"), Data("v"), Headers("nocolon", string.Empty))),
            Case("headers-encoding", Part(Name("a"), Data("v"), Headers("Content-Transfer-Encoding: base64"))),
            Case("sub-mixed", Part(Name("multi"), Type("multipart/mixed"), Sub(Part(Name("s1"), Data("one")), Part(Name("s2"), FileName("f.txt"), Data("two"))))),
            Case("sub-nested", Part(Name("outer"), Type("multipart/alternative"), Sub(Part(Type("multipart/mixed"), Sub(Part(Name("deep"), Data("d"))))))),
            Case("sub-no-type", Part(Name("multi"), Sub(Part(Data("one"))))),
            Case("sub-empty", Part(Name("multi"), Sub())),
            Case("sub-plus-data", Part(Name("multi"), Data("x"), Sub(Part(Data("one"))))),
            Case("sub-encoder", Part(Name("multi"), Encoder("base64"), Sub(Part(Data("one"))))),
            Case("many-parts", [.. Enumerable.Range(0, 50).Select(ManyPart)]),
            Case(
                "mixed-everything",
                Part(Name("a"), Data("1")),
                Part(Name("f"), FileName("a.txt"), Type("text/plain"), File("mimefile.txt")),
                Part(Name("b"), Encoder("base64"), Data("hello")),
                Part(Name("m"), Type("multipart/mixed"), Sub(Part(Data("x"))))),
            Case("empty-mime"),
            Case("name-then-null-data", Part(Name("a"), Data("x")), Part(Name("a"), DataHex(null))),
            Case("file-then-data", Part(Name("a"), Data("replaced"), File("mimefile.txt"))),
            Case("data-then-file", Part(Name("a"), Data("replaced"), File("mimefile.txt"))),
        ]);
        return cases;
    }

    private static LibcurlMimePart ManyPart(int index) =>
        Part(Name(string.Create(CultureInfo.InvariantCulture, $"p{index}")), Data(string.Create(CultureInfo.InvariantCulture, $"value{index}")));

    private static string EveryByte() => Convert.ToHexString([.. Enumerable.Range(0, 256).Select(value => (byte)value)]);

    private static LibcurlMimeCase Case(string name, params LibcurlMimePart[] parts) => new(name, parts);

    private static LibcurlMimePart Part(params LibcurlMimeStep[] steps) => new(steps);

    private static LibcurlMimeStep Name(string? name) => Text(LibcurlMimeStepKind.Name, name);

    private static LibcurlMimeStep FileName(string? fileName) => Text(LibcurlMimeStepKind.FileName, fileName);

    private static LibcurlMimeStep Type(string? type) => Text(LibcurlMimeStepKind.Type, type);

    private static LibcurlMimeStep Encoder(string? encoder) => Text(LibcurlMimeStepKind.Encoder, encoder);

    private static LibcurlMimeStep Data(string text) => DataHex(Convert.ToHexString(Encoding.UTF8.GetBytes(text)));

    private static LibcurlMimeStep DataHex(string? hex) => Text(LibcurlMimeStepKind.Data, hex);

    private static LibcurlMimeStep File(string fileName) => Text(LibcurlMimeStepKind.File, fileName);

    private static LibcurlMimeStep Headers(params string[] lines) => new(LibcurlMimeStepKind.Headers, null, lines, []);

    private static LibcurlMimeStep Sub(params LibcurlMimePart[] parts) => new(LibcurlMimeStepKind.SubParts, null, [], parts);

    private static LibcurlMimeStep Text(LibcurlMimeStepKind kind, string? text) => new(kind, text, [], []);
}
