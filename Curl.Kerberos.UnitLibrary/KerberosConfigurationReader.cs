using System.Globalization;
using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Parses <c>krb5.conf</c> files into one tree as MIT's profile library
/// (<c>prof_parse.c</c>) does: lines before the first section are ignored, <c>#</c> and
/// <c>;</c> start a comment only at the start of a line, <c>include</c> and
/// <c>includedir</c> work only at the very start of a line, and a malformed line fails the
/// whole file (ADR-0160).
/// </summary>
/// <param name="files">Reads the configuration files and lists <c>includedir</c> directories.</param>
public sealed class KerberosConfigurationReader(IKerberosFileReader files)
{
    /// <summary>How deep includes may nest before the file fails, as MIT limits them.</summary>
    public const int MaximumIncludeDepth = 5;

    private const string IncludeDirective = "include";
    private const string IncludeDirectoryDirective = "includedir";

    private enum LineState
    {
        InitialComment,
        StandardLine,
        OpeningBrace,
    }

    /// <summary>
    /// Parses the file at <paramref name="path" /> into <paramref name="root" />, merging its
    /// sections with those already there.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <param name="root">The tree the file's sections are added to.</param>
    /// <returns><see langword="false" /> when no file exists at <paramref name="path" />.</returns>
    /// <exception cref="KerberosConfigurationException">The file, or a file it includes, is malformed.</exception>
    public bool ReadFile(string path, KerberosConfigurationNode root)
    {
        byte[]? bytes = files.ReadAllBytes(path);
        if (bytes is null)
        {
            return false;
        }

        Parse(Encoding.UTF8.GetString(bytes), path, root, 0);
        return true;
    }

    /// <summary>Parses <paramref name="text" /> into <paramref name="root" />.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="source">The file's name, used in error messages.</param>
    /// <param name="root">The tree the text's sections are added to.</param>
    /// <exception cref="KerberosConfigurationException">The text, or a file it includes, is malformed.</exception>
    public void Parse(string text, string source, KerberosConfigurationNode root) => Parse(text, source, root, 0);

    private static bool IsBlank(char character) => character is ' ' or '\t' or '\v' or '\f' or '\r' or '\n';

    private static bool IsEndOrComment(string line, int index) => index >= line.Length || line[index] is '#' or ';';

    private static int SkipBlanks(string line, int index)
    {
        while (index < line.Length && IsBlank(line[index]))
        {
            index++;
        }

        return index;
    }

    private static string? DirectiveArgument(string line, string directive) =>
        line.Length > directive.Length && line.StartsWith(directive, StringComparison.Ordinal) && IsBlank(line[directive.Length])
            ? line[SkipBlanks(line, directive.Length)..]
            : null;

    private static bool IsIncludableFileName(string fileName) =>
        !fileName.StartsWith('.')
        && (fileName.EndsWith(".conf", StringComparison.Ordinal) || fileName.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));

    private void Parse(string text, string source, KerberosConfigurationNode root, int depth)
    {
        ParseState state = new(root);
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string where = string.Create(CultureInfo.InvariantCulture, $"{source}:{index + 1}");
            ParseLine(lines[index].TrimEnd('\r'), state, where, depth);
        }
    }

    private void ParseLine(string line, ParseState state, string where, int depth)
    {
        string? included = DirectiveArgument(line, IncludeDirective);
        if (included is not null)
        {
            IncludeFile(included, state.Root, where, depth);
            return;
        }

        string? includedDirectory = DirectiveArgument(line, IncludeDirectoryDirective);
        if (includedDirectory is not null)
        {
            IncludeDirectory(includedDirectory, state.Root, where, depth);
            return;
        }

        ParseStatementLine(line, state, where);
    }

    private static void ParseStatementLine(string line, ParseState state, string where)
    {
        switch (state.State)
        {
            case LineState.InitialComment when !line.StartsWith('['):
                return;
            case LineState.OpeningBrace:
                ParseOpeningBrace(line, state, where);
                return;
            default:
                state.State = LineState.StandardLine;
                ParseStandardLine(line, state, where);
                return;
        }
    }

    private static void ParseOpeningBrace(string line, ParseState state, string where)
    {
        int start = SkipBlanks(line, 0);
        if (start >= line.Length || line[start] != '{')
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.MissingOpeningBrace, where);
        }

        state.State = LineState.StandardLine;
    }

    private static void ParseStandardLine(string line, ParseState state, string where)
    {
        int start = SkipBlanks(line, 0);
        if (IsEndOrComment(line, start))
        {
            return;
        }

        switch (line[start])
        {
            case '[':
                ParseSectionHeader(line, start + 1, state, where);
                return;
            case '}':
                CloseGroup(state, where);
                return;
            default:
                ParseRelation(line, start, state, where);
                return;
        }
    }

    private static void ParseSectionHeader(string line, int nameStart, ParseState state, string where)
    {
        if (state.Groups.Count > 0)
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.SectionNotTop, where);
        }

        int close = line.IndexOf(']', nameStart);
        if (close < 0)
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.SectionSyntax, where);
        }

        state.Section = state.Root.GetOrAddGroup(line[nameStart..close]);
    }

    private static void CloseGroup(ParseState state, string where)
    {
        if (state.Groups.Count == 0)
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.ExtraClosingBrace, where);
        }

        state.Groups.Pop();
    }

    private static void ParseRelation(string line, int tagStart, ParseState state, string where)
    {
        string tag = RelationTag(line, tagStart, where, out int equals);
        int valueStart = SkipBlanks(line, equals + 1);
        if (valueStart < line.Length && line[valueStart] == '"')
        {
            state.Current.AddRelation(tag, UnquoteValue(line, valueStart + 1));
            return;
        }

        if (IsEndOrComment(line, valueStart))
        {
            state.State = LineState.OpeningBrace;
            state.Groups.Push(state.Current.GetOrAddGroup(tag));
            return;
        }

        if (line[valueStart] == '{')
        {
            OpenGroup(line, valueStart, tag, state, where);
            return;
        }

        state.Current.AddRelation(tag, line[valueStart..].TrimEnd(' ', '\t', '\v', '\f'));
    }

    private static void OpenGroup(string line, int brace, string tag, ParseState state, string where)
    {
        if (!IsEndOrComment(line, SkipBlanks(line, brace + 1)))
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.RelationSyntax, where);
        }

        state.Groups.Push(state.Current.GetOrAddGroup(tag));
    }

    private static string RelationTag(string line, int tagStart, string where, out int equals)
    {
        equals = line.IndexOf('=', tagStart);
        if (equals <= tagStart)
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.RelationSyntax, where);
        }

        string written = line[tagStart..equals];
        string tag = written.TrimEnd(' ', '\t', '\v', '\f');
        if (tag.Any(IsBlank))
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.RelationSyntax, where);
        }

        int final = tag.IndexOf('*', StringComparison.Ordinal);
        return final < 0 ? tag : tag[..final];
    }

    private static string UnquoteValue(string line, int start)
    {
        StringBuilder value = new();
        for (int index = start; index < line.Length && line[index] != '"'; index++)
        {
            if (line[index] != '\\')
            {
                value.Append(line[index]);
                continue;
            }

            index++;
            if (index >= line.Length)
            {
                break;
            }

            value.Append(Unescape(line[index]));
        }

        return value.ToString();
    }

    private static char Unescape(char escaped) => escaped switch
    {
        'n' => '\n',
        't' => '\t',
        'b' => '\b',
        _ => escaped,
    };

    private void IncludeFile(string path, KerberosConfigurationNode root, string where, int depth)
    {
        if (depth >= MaximumIncludeDepth)
        {
            throw new KerberosConfigurationException(KerberosConfigurationError.TooManyIncludes, where);
        }

        byte[] bytes = files.ReadAllBytes(path)
            ?? throw new KerberosConfigurationException(KerberosConfigurationError.IncludeFileNotFound, where);
        Parse(Encoding.UTF8.GetString(bytes), path, root, depth + 1);
    }

    private void IncludeDirectory(string path, KerberosConfigurationNode root, string where, int depth)
    {
        IReadOnlyList<string> fileNames = files.ListFileNames(path)
            ?? throw new KerberosConfigurationException(KerberosConfigurationError.IncludeDirectoryNotFound, where);
        foreach (string fileName in fileNames.Where(IsIncludableFileName).Order(StringComparer.Ordinal))
        {
            IncludeFile(path.TrimEnd('/') + "/" + fileName, root, where, depth);
        }
    }

    private sealed class ParseState(KerberosConfigurationNode root)
    {
        public KerberosConfigurationNode Root { get; } = root;

        public KerberosConfigurationNode Section { get; set; } = root;

        public Stack<KerberosConfigurationNode> Groups { get; } = new();

        public LineState State { get; set; } = LineState.InitialComment;

        public KerberosConfigurationNode Current => Groups.Count > 0 ? Groups.Peek() : Section;
    }
}
