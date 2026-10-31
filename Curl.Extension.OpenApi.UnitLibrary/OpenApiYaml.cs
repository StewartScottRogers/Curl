using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Curl.Extension.OpenApi;

/// <summary>
/// Reads a YAML 1.2 description (OpenAPI 3.1 §4.3) into the JSON text <see cref="OpenApiDocument" /> reads (ADR-1083): block
/// mappings and sequences (YAML 1.2.2 §8.2), flow mappings and sequences (§7.4), plain, single- and double-quoted scalars
/// (§7.3) with their escapes (§5.7), literal and folded block scalars (§8.1), comments (§6.6), directives (§6.8) and the
/// document start marker (§9.1.3). A plain scalar is a null or a Boolean by the core schema (§10.3.2), a number when it has
/// the JSON schema's number form (§10.2.1.4), and a string otherwise. Anchors, aliases and tags (§6.9, §7.1) are refused.
/// </summary>
/// <param name="lines">The description's lines; a sequence entry's line is rewritten in place to the node after its dash.</param>
/// <param name="writer">Where the JSON goes.</param>
internal sealed class OpenApiYaml(List<string> lines, Utf8JsonWriter writer)
{
    private const string EscapeLetters = "0abt\tnvfre \"/\\N_LP";
    private static readonly string EscapedCharacters = string.Concat("\0\a\b\t\t\n\v\f\r", (char)27, " \"/\\", (char)0x85, (char)0xA0, (char)0x2028, (char)0x2029);
    private static readonly Dictionary<string, bool?> Literals = new(StringComparer.Ordinal)
    {
        [string.Empty] = null,
        ["~"] = null,
        ["null"] = null,
        ["Null"] = null,
        ["NULL"] = null,
        ["true"] = true,
        ["True"] = true,
        ["TRUE"] = true,
        ["false"] = false,
        ["False"] = false,
        ["FALSE"] = false,
    };

    private int next;

    /// <summary>Converts a YAML description to JSON.</summary>
    /// <param name="text">The description's text.</param>
    /// <returns>The same description as UTF-8 JSON.</returns>
    /// <exception cref="FormatException">The text is not YAML this reader reads.</exception>
    /// <exception cref="NotSupportedException">The text uses an anchor, an alias or a tag.</exception>
    internal static byte[] ToJson(string text)
    {
        using MemoryStream json = new();
        using (Utf8JsonWriter writer = new(json, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            OpenApiYaml reader = new([.. text.ReplaceLineEndings("\n").Split('\n')], writer);
            reader.SkipDirectives();
            reader.WriteNode(-1);
            if (reader.SkipToContent())
            {
                throw new FormatException("a line is outside the document's top node");
            }
        }

        return json.ToArray();
    }

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    private static string Content(string line) => StripComment(line).Trim();

    private static bool IsBlank(string line) => line.Trim().Length == 0;

    private static bool IsSequenceEntry(string content) => content == "-" || content.StartsWith("- ", StringComparison.Ordinal);

    private static bool IsQuote(string text, int at) => at < text.Length && "\"'".Contains(text[at], StringComparison.Ordinal);

    private static string StripComment(string line)
    {
        string spaced = " " + line;
        for (int at = 0; at < line.Length; at++)
        {
            if (line[at] == '#' && char.IsWhiteSpace(spaced[at]))
            {
                return line[..at];
            }

            at = IsQuote(line, at) && StartsScalar(line, at) ? QuotedEnd(line, at) : at;
        }

        return line;
    }

    private static bool StartsScalar(string line, int at)
    {
        string before = line[..at].TrimEnd();
        return before.Length == 0 || ":-[{,".Contains(before[^1], StringComparison.Ordinal);
    }

    private static int QuotedEnd(string text, int start) => text[start] == '"' ? DoubleQuotedEnd(text, start) : SingleQuotedEnd(text, start);

    private static int DoubleQuotedEnd(string text, int start)
    {
        int at = start + 1;
        while (at < text.Length && text[at] != '"')
        {
            at += text[at] == '\\' ? 2 : 1;
        }

        return Math.Min(at, text.Length);
    }

    private static int SingleQuotedEnd(string text, int start)
    {
        int at = text.IndexOf('\'', start + 1);
        while (at >= 0 && at + 1 < text.Length && text[at + 1] == '\'')
        {
            at = text.IndexOf('\'', at + 2);
        }

        return at < 0 ? text.Length : at;
    }

    private static int KeyEnd(string content)
    {
        int colon = (content + " ").IndexOf(": ", IsQuote(content, 0) ? QuotedEnd(content, 0) : 0, StringComparison.Ordinal);
        return "[{".Contains(content[0], StringComparison.Ordinal) ? -1 : colon;
    }

    private static bool IsJsonNumber(string text)
    {
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(text));
        try
        {
            reader.Read();
            bool isNumber = reader.TokenType == JsonTokenType.Number;
            reader.Read();
            return isNumber;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string KeyText(string key) => IsQuote(key, 0) ? Unquote(key) : key;

    private static string Unquote(string quoted) =>
        quoted[0] == '\'' ? quoted[1..^1].Replace("''", "'", StringComparison.Ordinal) : Unescape(quoted[1..^1]);

    private static string Unescape(string body)
    {
        StringBuilder text = new();
        for (int at = 0; at < body.Length; at++)
        {
            if (body[at] != '\\')
            {
                text.Append(body[at]);
                continue;
            }

            at++;
            int kind = "xuU".IndexOf(body[at], StringComparison.Ordinal);
            int digits = kind < 0 ? 0 : 2 << kind;
            text.Append(digits == 0 ? Escaped(body[at]) : CodePoint(body, at + 1, digits));
            at += digits;
        }

        return text.ToString();
    }

    private static char Escaped(char letter) =>
        EscapeLetters.IndexOf(letter, StringComparison.Ordinal) is >= 0 and int index
            ? EscapedCharacters[index]
            : throw new FormatException($"\\{letter} is no escape");

    private static string CodePoint(string body, int start, int digits) =>
        start + digits <= body.Length
        && int.TryParse(body.AsSpan(start, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value)
        && Rune.TryCreate(value, out Rune rune)
            ? rune.ToString()
            : throw new FormatException("an escape names no Unicode scalar value");

    private static string Fold(List<string> parts)
    {
        StringBuilder folded = new(parts[0]);
        int breaks = 0;
        foreach (string part in parts.Skip(1))
        {
            if (part.Length == 0)
            {
                breaks++;
                continue;
            }

            folded.Append(breaks == 0 ? " " : new string('\n', breaks)).Append(part);
            breaks = 0;
        }

        return folded.ToString();
    }

    private static (char Chomping, int Indentation) BlockHeader(string header)
    {
        string indicators = header[1..].Trim();
        char chomping = indicators.FirstOrDefault(indicator => indicator is '-' or '+');
        int indentation = indicators.Where(char.IsAsciiDigit).Select(digit => digit - '0').FirstOrDefault();
        return indicators.Length == (chomping == '\0' ? 0 : 1) + (indentation == 0 ? 0 : 1)
            ? (chomping, indentation)
            : throw new FormatException($"{header} is no block scalar header");
    }

    private static bool IsFoldable(string line) => line.Length > 0 && line[0] != ' ';

    private static string FoldedBreak(List<string> body, int at) =>
        !IsFoldable(body[at - 1]) ? "\n"
        : IsFoldable(body[at]) ? " "
        : body[at].Length == 0 && IsFoldable(body.Skip(at).First(line => line.Length > 0)) ? string.Empty
        : "\n";

    private static string Chomped(char chomping, int textLines, int trailingLines) => chomping switch
    {
        '-' => string.Empty,
        '+' => new string('\n', trailingLines + Math.Min(textLines, 1)),
        _ => new string('\n', Math.Min(textLines, 1)),
    };

    private static int FlowDepth(string text)
    {
        int depth = 0;
        for (int at = 0; at < text.Length; at++)
        {
            depth += "[{".Contains(text[at], StringComparison.Ordinal) ? 1 : "]}".Contains(text[at], StringComparison.Ordinal) ? -1 : 0;
            at = IsQuote(text, at) && StartsScalar(text, at) ? QuotedEnd(text, at) : at;
        }

        return depth;
    }

    private static void SkipSpaces(string text, ref int at)
    {
        while (at < text.Length && text[at] == ' ')
        {
            at++;
        }
    }

    private static bool FlowEnds(string text, ref int at, char close)
    {
        SkipSpaces(text, ref at);
        if (at == text.Length)
        {
            throw new FormatException("a flow collection is not closed");
        }

        at += text[at] == close ? 1 : 0;
        return text[at - 1] == close;
    }

    private static void FlowSeparator(string text, ref int at, char close)
    {
        SkipSpaces(text, ref at);
        if (at < text.Length && text[at] == ',')
        {
            at++;
        }
        else if (at == text.Length || text[at] != close)
        {
            throw new FormatException($"a flow collection entry is not followed by , or {close}");
        }
    }

    private static string FlowScalar(string text, ref int at)
    {
        SkipSpaces(text, ref at);
        int start = at;
        if (IsQuote(text, at))
        {
            at = QuotedEnd(text, at) + 1;
            return at <= text.Length ? text[start..at] : throw new FormatException("a quoted scalar is not closed");
        }

        while (at < text.Length && !EndsFlowPlainScalar(text, at))
        {
            at++;
        }

        return text[start..at].TrimEnd();
    }

    private static bool EndsFlowPlainScalar(string text, int at) =>
        ",[]{}".Contains(text[at], StringComparison.Ordinal) || (text[at] == ':' && " ,]}".Contains((text + " ")[at + 1], StringComparison.Ordinal));

    private bool SkipToContent()
    {
        while (next < lines.Count && Content(lines[next]).Length == 0)
        {
            next++;
        }

        return next < lines.Count;
    }

    private void SkipDirectives()
    {
        while (SkipToContent() && (lines[next].StartsWith('%') || Content(lines[next]) == "---"))
        {
            next++;
        }
    }

    private bool IsInside(int parentIndent) => next < lines.Count && (IsBlank(lines[next]) || Indent(lines[next]) > parentIndent);

    private void WriteNode(int parentIndent)
    {
        if (SkipToContent() && Indent(lines[next]) > parentIndent)
        {
            WriteBlockNode(parentIndent);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private void WriteBlockNode(int parentIndent)
    {
        string content = Content(lines[next]);
        int indent = Indent(lines[next]);
        if (IsSequenceEntry(content))
        {
            WriteSequence(indent);
        }
        else if (KeyEnd(content) >= 0)
        {
            WriteMapping(indent);
        }
        else
        {
            next++;
            WriteValue(content, parentIndent);
        }
    }

    private void WriteSequence(int indent)
    {
        writer.WriteStartArray();
        while (SkipToContent() && Indent(lines[next]) == indent && IsSequenceEntry(Content(lines[next])))
        {
            string afterDash = lines[next][(indent + 1)..];
            string entry = afterDash.TrimStart(' ');
            if (Content(entry).Length == 0)
            {
                next++;
                WriteNode(indent);
            }
            else
            {
                lines[next] = new string(' ', indent + 1 + afterDash.Length - entry.Length) + entry;
                WriteBlockNode(indent);
            }
        }

        writer.WriteEndArray();
    }

    private void WriteMapping(int indent)
    {
        writer.WriteStartObject();
        while (SkipToContent() && Indent(lines[next]) == indent && !IsSequenceEntry(Content(lines[next])))
        {
            string content = Content(lines[next]);
            int colon = KeyEnd(content);
            if (colon < 0)
            {
                throw new FormatException($"{content} is not a mapping key");
            }

            writer.WritePropertyName(KeyText(content[..colon].Trim()));
            next++;
            WriteValue(content[(colon + 1)..].Trim(), indent);
        }

        writer.WriteEndObject();
    }

    private void WriteValue(string text, int parentIndent)
    {
        if (text.Length == 0)
        {
            WriteMappingValueNode(parentIndent);
        }
        else if ("|>".Contains(text[0], StringComparison.Ordinal))
        {
            WriteBlockScalar(text, parentIndent);
        }
        else if ("[{".Contains(text[0], StringComparison.Ordinal))
        {
            WriteFlow(text, parentIndent);
        }
        else if (IsQuote(text, 0))
        {
            WriteQuoted(GatherQuoted(text, parentIndent));
        }
        else
        {
            WritePlain(GatherPlain(text, parentIndent));
        }
    }

    private void WriteMappingValueNode(int keyIndent)
    {
        if (SkipToContent() && Indent(lines[next]) == keyIndent && IsSequenceEntry(Content(lines[next])))
        {
            WriteSequence(keyIndent);
        }
        else
        {
            WriteNode(keyIndent);
        }
    }

    private string GatherPlain(string text, int parentIndent)
    {
        List<string> parts = [text];
        while (IsInside(parentIndent) && (IsBlank(lines[next]) || Content(lines[next]).Length > 0))
        {
            parts.Add(Content(lines[next++]));
        }

        return Fold(parts);
    }

    private string GatherQuoted(string text, int parentIndent)
    {
        List<string> parts = [text];
        string joined = text;
        while (QuotedEnd(joined, 0) == joined.Length && IsInside(parentIndent))
        {
            parts.Add(lines[next++].Trim());
            joined = Fold(parts);
        }

        return joined;
    }

    private void WriteQuoted(string joined)
    {
        int end = QuotedEnd(joined, 0);
        if (end == joined.Length || Content(joined[(end + 1)..]).Length > 0)
        {
            throw new FormatException($"{joined} is not one quoted scalar");
        }

        writer.WriteStringValue(Unquote(joined[..(end + 1)]));
    }

    private void WritePlain(string text)
    {
        if (text.Length > 0 && "&*!".Contains(text[0], StringComparison.Ordinal))
        {
            throw new NotSupportedException($"{text} uses an anchor, an alias or a tag");
        }

        if (Literals.TryGetValue(text, out bool? literal))
        {
            WriteLiteral(literal);
        }
        else if (IsJsonNumber(text))
        {
            writer.WriteRawValue(text);
        }
        else
        {
            writer.WriteStringValue(text);
        }
    }

    private void WriteLiteral(bool? literal)
    {
        if (literal is { } boolean)
        {
            writer.WriteBooleanValue(boolean);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private void WriteBlockScalar(string header, int parentIndent)
    {
        (char chomping, int indentation) = BlockHeader(header);
        List<string> body = BlockLines(parentIndent, indentation);
        int textLines = body.Count;
        while (textLines > 0 && IsBlank(body[textLines - 1]))
        {
            textLines--;
        }

        List<string> text = body[..textLines];
        string joined = header[0] == '|'
            ? string.Join('\n', text)
            : string.Concat(text.Select((line, at) => at == 0 ? line : FoldedBreak(text, at) + line));
        writer.WriteStringValue(joined + Chomped(chomping, textLines, body.Count - textLines));
    }

    private List<string> BlockLines(int parentIndent, int indentation)
    {
        List<string> raw = [];
        while (IsInside(parentIndent))
        {
            raw.Add(lines[next++]);
        }

        int contentIndent = indentation > 0 ? Math.Max(parentIndent, 0) + indentation : raw.Where(line => !IsBlank(line)).Select(Indent).FirstOrDefault();
        return [.. raw.Select(line => line.Length > contentIndent ? line[contentIndent..] : string.Empty)];
    }

    private void WriteFlow(string text, int parentIndent)
    {
        string flow = text;
        while (FlowDepth(flow) > 0 && IsInside(parentIndent))
        {
            flow += " " + Content(lines[next++]);
        }

        int at = 0;
        WriteFlowNode(flow, ref at);
        if (at < flow.Length && flow[at..].Trim().Length > 0)
        {
            throw new FormatException($"{flow[at..]} follows a flow collection");
        }
    }

    private void WriteFlowNode(string text, ref int at)
    {
        SkipSpaces(text, ref at);
        char first = at < text.Length ? text[at] : ',';
        if (first == '[')
        {
            WriteFlowSequence(text, ref at);
        }
        else if (first == '{')
        {
            WriteFlowMapping(text, ref at);
        }
        else
        {
            string scalar = FlowScalar(text, ref at);
            WriteFlowScalar(scalar);
        }
    }

    private void WriteFlowScalar(string scalar)
    {
        if (IsQuote(scalar, 0))
        {
            writer.WriteStringValue(Unquote(scalar));
        }
        else
        {
            WritePlain(scalar);
        }
    }

    private void WriteFlowSequence(string text, ref int at)
    {
        writer.WriteStartArray();
        at++;
        while (!FlowEnds(text, ref at, ']'))
        {
            WriteFlowNode(text, ref at);
            FlowSeparator(text, ref at, ']');
        }

        writer.WriteEndArray();
    }

    private void WriteFlowMapping(string text, ref int at)
    {
        writer.WriteStartObject();
        at++;
        while (!FlowEnds(text, ref at, '}'))
        {
            writer.WritePropertyName(KeyText(FlowScalar(text, ref at)));
            SkipSpaces(text, ref at);
            if (at < text.Length && text[at] == ':')
            {
                at++;
                WriteFlowNode(text, ref at);
            }
            else
            {
                writer.WriteNullValue();
            }

            FlowSeparator(text, ref at, '}');
        }

        writer.WriteEndObject();
    }
}
