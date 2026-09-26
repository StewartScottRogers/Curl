using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Parses one upstream curl test file (<c>tests/data/test*</c>, format described in
/// <c>docs/tests/FILEFORMAT.md</c> at <c>curl-8_21_0</c>) into an <see cref="UpstreamTestCase"/>.
/// </summary>
/// <remarks>
/// The format is line-based and is not well-formed XML, so it is read the way upstream's
/// <c>getpart.pm</c> reads it: a tag is a line starting with that tag (see
/// <see cref="UpstreamTestFileTag"/>), the file's root (<c>&lt;testcase&gt;</c>) holds sections
/// (<c>&lt;reply&gt;</c>, <c>&lt;verify&gt;</c>, …), and a section holds parts (<c>&lt;data&gt;</c>,
/// <c>&lt;stdout&gt;</c>, …). Inside a part every byte is body until the line that closes that
/// part, so a body may hold <c>&lt;</c>, <c>&amp;</c> and lines that look like other tags. As in
/// <c>getpart.pm</c>, a body line opening a tag of the part's own name nests, and needs its own
/// closing line before the part closes, and a line closing the enclosing section inside a part
/// is an error. Lines outside a part that are not tags are ignored. Variables and <c>%if</c>
/// blocks are left as written.
/// </remarks>
public static class UpstreamTestCaseParser
{
    private const int PartDepth = 3;

    /// <summary>Parses the bytes of one test file.</summary>
    /// <param name="file">The whole file.</param>
    /// <returns>The test case, or the failure naming the section and line that stopped it.</returns>
    public static UpstreamTestCaseParseResult Parse(ReadOnlySpan<byte> file)
    {
        FileReader reader = new(file.ToArray());
        return reader.Read();
    }

    private sealed class FileReader(byte[] bytes)
    {
        private readonly List<OpenElement> open = [];
        private readonly List<UpstreamTestSection> sections = [];
        private UpstreamTestCaseParseFailure? failure;
        private int partBodyStart;

        public UpstreamTestCaseParseResult Read()
        {
            int lineStart = 0;
            int lineNumber = 0;
            while (lineStart < bytes.Length && failure is null)
            {
                int lineEnd = EndOfLine(lineStart);
                lineNumber++;
                ReadLine(lineStart, lineEnd, lineNumber);
                lineStart = lineEnd;
            }

            return Finish();
        }

        private int EndOfLine(int lineStart)
        {
            int lineFeed = Array.IndexOf(bytes, (byte)'\n', lineStart);
            return lineFeed < 0 ? bytes.Length : lineFeed + 1;
        }

        private void ReadLine(int lineStart, int lineEnd, int lineNumber)
        {
            string text = Encoding.Latin1.GetString(bytes, lineStart, lineEnd - lineStart);
            if (open.Count == PartDepth)
            {
                ReadPartLine(text, lineStart, lineNumber);
            }
            else if (UpstreamTestFileTag.TryReadClosing(text, out string closedName))
            {
                Close(closedName, lineNumber);
            }
            else if (UpstreamTestFileTag.TryReadOpening(text, out string openedName, out IReadOnlyDictionary<string, string> attributes))
            {
                open.Add(new OpenElement(openedName, attributes, lineNumber));
                partBodyStart = lineEnd;
            }
        }

        private void ReadPartLine(string text, int lineStart, int lineNumber)
        {
            OpenElement part = open[^1];
            if (UpstreamTestFileTag.Opens(text, part.Name))
            {
                part.NestedDepth++;
            }
            else if (UpstreamTestFileTag.Closes(text, part.Name))
            {
                CloseNestedOrPart(part, lineStart);
            }
            else if (UpstreamTestFileTag.Closes(text, open[^2].Name))
            {
                string path = PathOfOpenElements();
                failure = new UpstreamTestCaseParseFailure(
                    path,
                    lineNumber,
                    $"{path} opened on line {part.LineNumber} is not closed before </{open[^2].Name}> on line {lineNumber}.");
            }
        }

        private void CloseNestedOrPart(OpenElement part, int lineStart)
        {
            if (part.NestedDepth > 0)
            {
                part.NestedDepth--;
                return;
            }

            ClosePart(lineStart);
        }

        private void Close(string name, int lineNumber)
        {
            if (open.Count == 0 || open[^1].Name != name)
            {
                failure = new UpstreamTestCaseParseFailure(
                    $"</{name}>",
                    lineNumber,
                    $"</{name}> on line {lineNumber} does not close an open section.");
                return;
            }

            open.RemoveAt(open.Count - 1);
        }

        private void ClosePart(int lineStart)
        {
            OpenElement part = open[^1];
            byte[] content = bytes[partBodyStart..lineStart];
            sections.Add(new UpstreamTestSection(open[^2].Name, part.Name, part.Attributes, content, part.LineNumber));
            open.RemoveAt(open.Count - 1);
        }

        private UpstreamTestCaseParseResult Finish()
        {
            if (failure is null && open.Count > 0)
            {
                string path = PathOfOpenElements();
                int lineNumber = open[^1].LineNumber;
                failure = new UpstreamTestCaseParseFailure(path, lineNumber, $"{path} opened on line {lineNumber} is never closed.");
            }

            return failure is null
                ? UpstreamTestCaseParseResult.Parsed(new UpstreamTestCase(sections))
                : UpstreamTestCaseParseResult.Failed(failure);
        }

        private string PathOfOpenElements() =>
            string.Concat(open.Skip(open.Count > 1 ? 1 : 0).Select(element => $"<{element.Name}>"));
    }

    private sealed class OpenElement(string name, IReadOnlyDictionary<string, string> attributes, int lineNumber)
    {
        public string Name { get; } = name;

        public IReadOnlyDictionary<string, string> Attributes { get; } = attributes;

        public int LineNumber { get; } = lineNumber;

        public int NestedDepth { get; set; }
    }
}
