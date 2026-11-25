using System.Collections.Frozen;

namespace Curl.Mcp;

/// <summary>
/// Finds the local paths one option value names, by the option's <see cref="McpOptionPathUse" />,
/// the way curl reads the value (ADR-0564): <c>@file</c>, <c>name@file</c>, <c>name=@file</c>,
/// <c>name=&lt;file</c>, <c>%output{file}</c>, <c>file://</c> URLs and Unix socket proxy URLs. A
/// value that names standard input or output (<c>-</c>, <c>@-</c>) names no path.
/// </summary>
internal static class McpOptionValuePaths
{
    private const string StandardStream = "-";

    private const int MaxUploadExpansions = 1000;

    private const string GroupOpeners = "{[";

    private const string GroupClosers = "}]";

    private const string OutputDirective = "output{";

    private const string FormHeadersParameter = ";headers=";

    private static readonly char[] GlobCharacters = ['{', '}', '[', ']'];

    private static readonly char[] UrlEncodedSeparators = ['=', '@'];

    /// <summary>The paths <paramref name="value" />, given to the option <paramref name="longName" />, names.</summary>
    /// <param name="longName">The option's long name without <c>--</c>.</param>
    /// <param name="use">How the option uses a path; never <see cref="McpOptionPathUse.None" />.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="outputDirectory">The <c>--output-dir</c> value curl joins before an <c>-o</c> name, or <see langword="null" /> when none is given.</param>
    /// <param name="globAlternativesEscape">Whether an argument has a URL glob alternative that could carry a path out of the root (<see cref="AnyAlternativeEscapes" />), so an <c>-o</c> name with <c>#&lt;n&gt;</c> is refused.</param>
    /// <returns>The paths to check, in the order the value names them.</returns>
    internal static IReadOnlyList<McpValuePath> PathsIn(string longName, McpOptionPathUse use, string value, string? outputDirectory = null, bool globAlternativesEscape = false) => use switch
    {
        McpOptionPathUse.ValueEmbedded => EmbeddedPaths(longName, value),
        McpOptionPathUse.ReadsFileOrLiteral => FileOrLiteralPaths(longName, value),
        McpOptionPathUse.WritesFile => WrittenFilePaths(longName, value, outputDirectory, globAlternativesEscape),
        McpOptionPathUse.UnixSocketPath => SocketPaths(longName, value),
        _ => longName == "upload-file" ? UploadPaths(value) : [new(value)],
    };

    /// <summary>
    /// The path a <c>file:</c> URL names, with any host form: none, <c>localhost</c> and
    /// <c>127.0.0.1</c> are this machine; any other host is kept as a <c>//host/path</c> share path.
    /// The query and fragment are dropped and percent-encoding decoded, as curl does before it opens
    /// the file. A URL of any other scheme names no path.
    /// </summary>
    /// <param name="url">The URL as given.</param>
    /// <returns>The URL's path, or nothing.</returns>
    internal static IReadOnlyList<McpValuePath> UrlPaths(string url)
    {
        const string FileScheme = "file:";
        if (!url.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        string rest = url[FileScheme.Length..];
        if (rest.IndexOfAny(GlobCharacters) >= 0)
        {
            return [new(url, "is a file:// URL with glob characters ({}, []), whose expanded paths cannot be checked before curl runs.")];
        }

        int queryOrFragment = rest.IndexOfAny(['?', '#']);
        string withoutQuery = queryOrFragment < 0 ? rest : rest[..queryOrFragment];
        string path = withoutQuery.StartsWith("//", StringComparison.Ordinal) ? PathAfterHost(withoutQuery[2..]) : withoutQuery;
        return [new(WithoutSlashBeforeDrive(Uri.UnescapeDataString(path)))];
    }

    /// <summary>The path of a <c>file://</c> URL's authority and path: the path alone for an empty, <c>localhost</c> or <c>127.0.0.1</c> host, otherwise <c>//host/path</c>.</summary>
    private static string PathAfterHost(string authorityAndPath)
    {
        int slash = authorityAndPath.IndexOf('/', StringComparison.Ordinal);
        string host = slash < 0 ? authorityAndPath : authorityAndPath[..slash];
        string path = slash < 0 ? "/" : authorityAndPath[slash..];
        return host.Length == 0 || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1"
            ? path
            : "//" + host + path;
    }

    /// <summary>Turns <c>/C:/dir</c>, a Windows drive path as a URL writes it, into <c>C:/dir</c>.</summary>
    private static string WithoutSlashBeforeDrive(string path) =>
        path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':' ? path[1..] : path;

    /// <summary>How each <see cref="McpOptionPathUse.ValueEmbedded" /> option's value names paths; the proxies are the rest.</summary>
    private static readonly FrozenDictionary<string, Func<string, IReadOnlyList<McpValuePath>>> EmbeddedPathReaders =
        new Dictionary<string, Func<string, IReadOnlyList<McpValuePath>>>(StringComparer.Ordinal)
        {
            ["data"] = AtFilePaths,
            ["data-ascii"] = AtFilePaths,
            ["data-binary"] = AtFilePaths,
            ["json"] = AtFilePaths,
            ["header"] = AtFilePaths,
            ["proxy-header"] = AtFilePaths,
            ["data-urlencode"] = UrlEncodedPaths,
            ["url-query"] = value => UrlEncodedPaths(value.StartsWith('+') ? value[1..] : value),
            ["variable"] = value => UrlEncodedPaths(value.StartsWith('%') ? value[1..] : value),
            ["form"] = FormPaths,
            // A -w @file template is read and checked by McpArgumentChecker itself (ADR-0600).
            ["write-out"] = TemplatePaths,
            ["url"] = UrlPaths,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The paths of a <see cref="McpOptionPathUse.ValueEmbedded" /> option's value: its reader above, or, for the proxy options with no reader, a Unix socket proxy URL.</summary>
    private static IReadOnlyList<McpValuePath> EmbeddedPaths(string longName, string value) =>
        EmbeddedPathReaders.TryGetValue(longName, out Func<string, IReadOnlyList<McpValuePath>>? readPaths)
            ? readPaths(value)
            : ProxySocketPaths(value);

    /// <summary>A value starting with <c>@</c> names the file after it (<c>-d @file</c>).</summary>
    private static IReadOnlyList<McpValuePath> AtFilePaths(string value) =>
        value.StartsWith('@') ? FilePath(value[1..]) : [];

    /// <summary>
    /// <c>name@file</c> or <c>@file</c> names a file; <c>name=content</c> does not. curl takes
    /// whichever of <c>=</c> and <c>@</c> comes first (<c>--data-urlencode</c>, <c>--url-query</c>, <c>--variable</c>).
    /// </summary>
    private static IReadOnlyList<McpValuePath> UrlEncodedPaths(string value)
    {
        int separator = value.IndexOfAny(UrlEncodedSeparators);
        return separator >= 0 && value[separator] == '@' ? FilePath(value[(separator + 1)..]) : [];
    }

    /// <summary>The one path <paramref name="path" /> names, or none for <c>-</c>, which curl reads or writes as a standard stream.</summary>
    private static IReadOnlyList<McpValuePath> FilePath(string path) => path == StandardStream ? [] : [new(path)];

    /// <summary>
    /// <c>-F name=@file</c> and <c>-F name=&lt;file</c> read a file, as does a <c>;headers=@file</c>
    /// or <c>;headers=&lt;file</c> parameter; the file name runs to <c>;</c> (so <c>;type=</c> is
    /// stripped) or is quoted, and each comma-separated name is checked.
    /// </summary>
    private static IReadOnlyList<McpValuePath> FormPaths(string value)
    {
        List<McpValuePath> paths = [];
        string content = value[(value.IndexOf('=', StringComparison.Ordinal) + 1)..];
        AddFormFiles(paths, content);
        for (int headers = content.IndexOf(FormHeadersParameter, StringComparison.Ordinal);
            headers >= 0;
            headers = content.IndexOf(FormHeadersParameter, headers + 1, StringComparison.Ordinal))
        {
            AddFormFiles(paths, content[(headers + FormHeadersParameter.Length)..]);
        }

        return paths;
    }

    /// <summary>Adds the comma-separated files <paramref name="text" /> names when it starts with <c>@</c> or <c>&lt;</c>.</summary>
    private static void AddFormFiles(List<McpValuePath> paths, string text)
    {
        if (!text.StartsWith('@') && !text.StartsWith('<'))
        {
            return;
        }

        foreach (string name in FormWord(text[1..]).Split(','))
        {
            paths.AddRange(FilePath(name));
        }
    }

    /// <summary>A form file name: quoted, to the next unescaped <c>"</c>; else to the first <c>;</c>.</summary>
    private static string FormWord(string text)
    {
        int semicolon = text.IndexOf(';', StringComparison.Ordinal);
        if (text.StartsWith('"'))
        {
            return QuotedFormWord(text);
        }

        return semicolon < 0 ? text : text[..semicolon];
    }

    /// <summary>The text after the opening <c>"</c> up to the next unescaped <c>"</c>, a backslash taking the character after it literally.</summary>
    private static string QuotedFormWord(string text)
    {
        System.Text.StringBuilder word = new();
        for (int index = 1; index < text.Length && text[index] != '"'; index++)
        {
            index += text[index] == '\\' && index + 1 < text.Length ? 1 : 0;
            word.Append(text[index]);
        }

        return word.ToString();
    }

    /// <summary>The files a <c>-w</c> template's <c>%output{file}</c> and <c>%output{&gt;&gt;file}</c> directives name.</summary>
    /// <param name="template">The template text, given inline or read from a <c>-w @file</c> file.</param>
    /// <returns>The paths to check, in the order the template names them.</returns>
    internal static List<McpValuePath> TemplatePaths(string template)
    {
        List<McpValuePath> paths = [];
        int index = 0;
        while (index < template.Length)
        {
            index = template[index] == '%' ? TakeDirective(template, index, paths) : index + 1;
        }

        return paths;
    }

    /// <summary>
    /// Reads the directive at the <c>%</c> at <paramref name="index" />: <c>%%</c> is a literal
    /// percent sign, and <c>%output{file}</c> or <c>%output{&gt;&gt;file}</c> adds its file.
    /// </summary>
    /// <returns>Where reading goes on.</returns>
    private static int TakeDirective(string template, int index, List<McpValuePath> paths)
    {
        ReadOnlySpan<char> rest = template.AsSpan(index + 1);
        if (rest.StartsWith("%"))
        {
            return index + 2;
        }

        if (!rest.StartsWith(OutputDirective))
        {
            return index + 1;
        }

        int start = index + 1 + OutputDirective.Length;
        int close = template.IndexOf('}', start);
        int end = close < 0 ? template.Length : close;
        string path = template[start..end];
        paths.Add(new(path.StartsWith(">>", StringComparison.Ordinal) ? path[2..] : path));
        return end + 1;
    }

    /// <summary>
    /// A proxy URL whose host is <c>localhost</c> and that has a path (<c>socks5h://localhost/run/tor.sock</c>)
    /// names the Unix socket at that path.
    /// </summary>
    private static IReadOnlyList<McpValuePath> ProxySocketPaths(string value)
    {
        int scheme = value.IndexOf("://", StringComparison.Ordinal);
        string rest = scheme < 0 ? value : value[(scheme + 3)..];
        int slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || slash == rest.Length - 1)
        {
            return [];
        }

        string hostAndPort = rest[(rest.LastIndexOf('@', slash) + 1)..slash];
        int colon = hostAndPort.IndexOf(':', StringComparison.Ordinal);
        string host = colon < 0 ? hostAndPort : hostAndPort[..colon];
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? [new(WithoutSlashBeforeDrive(rest[slash..]))] : [];
    }

    /// <summary>
    /// <c>-b</c> reads a file only when its value has no <c>=</c>; <c>--pinnedpubkey</c> only when it
    /// is not <c>sha256//</c> hashes; <c>-z</c> reads the time of the file its value names, after any
    /// <c>-</c>, <c>+</c> or <c>=</c> prefix, and a date resolves as a name inside the root.
    /// </summary>
    private static IReadOnlyList<McpValuePath> FileOrLiteralPaths(string longName, string value) => longName switch
    {
        "cookie" => value.Contains('=', StringComparison.Ordinal) ? [] : FilePath(value),
        "time-cond" => [new(value.Length > 0 && value[0] is '-' or '+' or '=' ? value[1..] : value)],
        _ => value.StartsWith("sha256//", StringComparison.Ordinal) ? [] : [new(value)],
    };

    /// <summary>
    /// A written file: <c>-</c> is standard output. <c>-o</c> is judged where curl writes it: below
    /// <paramref name="outputDirectory" /> when one is given, and a <c>#1</c> glob reference counts as
    /// file-name characters, so the name is judged on its literal directory part.
    /// </summary>
    private static IReadOnlyList<McpValuePath> WrittenFilePaths(string longName, string value, string? outputDirectory, bool globAlternativesEscape)
    {
        if (longName != "output")
        {
            return FilePath(value);
        }

        if (value == StandardStream || IsNullDevice(value))
        {
            return [];
        }

        string name = WithoutGlobReferences(value);
        return [OutputPath(value, name, outputDirectory, globAlternativesEscape && name != value)];
    }

    /// <summary>The path an <c>-o</c> name writes; refused when its <c>#&lt;n&gt;</c> would carry an escaping glob alternative into it.</summary>
    private static McpValuePath OutputPath(string value, string name, string? outputDirectory, bool carriesEscape) =>
        carriesEscape
            ? new(value, "names a #<n> glob reference while an argument has a {} or [] alternative holding /, \\ or .., which curl would write into the name outside CURL_MCP_ROOT.")
            : new(string.IsNullOrEmpty(outputDirectory) ? name : outputDirectory + "/" + name);

    /// <summary>Whether any argument holds a <c>{...}</c> or <c>[...]</c> alternative with <c>/</c>, <c>\</c> or <c>..</c>, which a <c>#1</c> in an <c>-o</c> name would carry into the path.</summary>
    internal static bool AnyAlternativeEscapes(IReadOnlyList<string> arguments)
    {
        foreach (string argument in arguments)
        {
            if (AlternativeEscapes(argument ?? string.Empty, '{', '}') || AlternativeEscapes(argument ?? string.Empty, '[', ']'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the <paramref name="open" />...<paramref name="close" /> alternatives in <paramref name="text" /> could carry a path
    /// out of the root into an <c>-o</c> name: a <c>{}</c> alternative holding <c>/</c>, <c>\</c>, <c>:</c> or <c>..</c>, or starting
    /// or ending with <c>.</c> (curl copies it verbatim, so a lone <c>.</c> beside a literal <c>.</c> makes <c>..</c>); a <c>[]</c>
    /// range holding anything but letters, digits, <c>-</c> and <c>:</c>.
    /// </summary>
    private static bool AlternativeEscapes(string text, char open, char close)
    {
        for (int start = text.IndexOf(open); start >= 0; start = text.IndexOf(open, start + 1))
        {
            int end = text.IndexOf(close, start + 1);
            string alternatives = end < 0 ? text[(start + 1)..] : text[(start + 1)..end];
            if (open == '{' ? BraceAlternativesEscape(alternatives) : RangeEscapes(alternatives))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BraceAlternativesEscape(string alternatives) =>
        alternatives.AsSpan().IndexOfAny('/', '\\', ':') >= 0
        || alternatives.Contains("..", StringComparison.Ordinal)
        || alternatives.Split(',').Any(alternative => alternative.StartsWith('.') || alternative.EndsWith('.'));

    private static bool RangeEscapes(string range) =>
        range.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or ':'));
    /// <summary>Replaces each <c>#</c> followed by a digit, curl's <c>#1</c> reference to a URL glob, with a file-name character.</summary>
    private static string WithoutGlobReferences(string value)
    {
        System.Text.StringBuilder name = new(value);
        for (int index = 0; index + 1 < name.Length; index++)
        {
            if (name[index] == '#' && char.IsAsciiDigit(name[index + 1]))
            {
                name.Remove(index, 2).Insert(index, "_");
            }
        }

        return name.ToString();
    }

    /// <summary>Whether <paramref name="value" /> is a spelling of the null device that <c>-o</c> may discard to: <c>/dev/null</c>, or <c>NUL</c> in any case (ADR-1301).</summary>
    private static bool IsNullDevice(string value) =>
        value == "/dev/null" || value.Equals("NUL", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>-T -</c> and <c>-T .</c> read standard input. A <c>{a,b}</c> or <c>[1-2]</c> glob is expanded as curl does,
    /// so each file it names is checked; a glob that cannot be expanded here is refused.
    /// </summary>
    private static IReadOnlyList<McpValuePath> UploadPaths(string value)
    {
        if (value is StandardStream or ".")
        {
            return [];
        }

        return value.IndexOfAny(GlobCharacters) < 0 ? [new(value)] : GlobUploadPaths(value);
    }

    private static IReadOnlyList<McpValuePath> GlobUploadPaths(string value)
    {
        List<string>? expansions = ExpandUploadGlob(value);
        return expansions is null
            ? [new(value, "names files with a malformed or very large glob ({}, []), whose expanded paths cannot be checked before curl runs.")]
            : [.. expansions.Select(expansion => new McpValuePath(expansion))];
    }

    /// <summary>Every file name a <c>-T</c> glob expands to, or <see langword="null" /> when it is malformed or expands to more than <see cref="MaxUploadExpansions" /> names.</summary>
    private static List<string>? ExpandUploadGlob(string value)
    {
        List<string> expansions = [string.Empty];
        int index = 0;
        while (index < value.Length)
        {
            string[]? alternatives = AlternativesAt(value, ref index);
            if (alternatives is null)
            {
                return null;
            }

            expansions = [.. expansions.SelectMany(_ => alternatives, (prefix, alternative) => prefix + alternative)];
            if (expansions.Count > MaxUploadExpansions)
            {
                return null;
            }
        }

        return expansions;
    }

    /// <summary>The alternatives the glob part at <paramref name="index" /> stands for (one for a literal character), moving <paramref name="index" /> past it; <see langword="null" /> when malformed.</summary>
    private static string[]? AlternativesAt(string value, ref int index)
    {
        char current = value[index];
        if (GroupClosers.Contains(current))
        {
            return null;
        }

        if (GroupOpeners.Contains(current))
        {
            return GroupAlternatives(value, ref index);
        }

        index += IsEscapedGlobCharacter(value, index) ? 2 : 1;
        return [value[index - 1].ToString()];
    }

    /// <summary>The alternatives of the <c>{...}</c> or <c>[...]</c> group opening at <paramref name="index" />, or <see langword="null" /> when it never closes or is malformed.</summary>
    private static string[]? GroupAlternatives(string value, ref int index)
    {
        bool isBrace = value[index] == '{';
        int close = value.IndexOf(isBrace ? '}' : ']', index + 1);
        if (close < 0)
        {
            return null;
        }

        string body = value[(index + 1)..close];
        index = close + 1;
        return isBrace ? body.Split(',') : RangeValues(body);
    }

    /// <summary>The values of a <c>[a-z]</c>, <c>[1-10]</c>, <c>[01-10]</c> or <c>[1-10:2]</c> range, or <see langword="null" /> when it is malformed, descending or too large.</summary>
    private static string[]? RangeValues(string body)
    {
        string[] byStep = body.Split(':');
        string[] ends = byStep[0].Split('-');
        int step = RangeStep(byStep);
        return step < 1 || ends.Length != 2 ? null : NumberRange(ends[0], ends[1], step) ?? LetterRange(ends[0], ends[1], step);
    }

    /// <summary>A range's step: 1 when none is given, 0 when it is not a number or the range has more than one <c>:</c>.</summary>
    private static int RangeStep(string[] byStep) =>
        byStep.Length == 1 ? 1 : byStep.Length == 2 && int.TryParse(byStep[1], out int parsed) ? parsed : 0;

    private static bool IsEscapedGlobCharacter(string value, int index) =>
        value[index] == '\\' && index + 1 < value.Length && GlobCharacters.Contains(value[index + 1]);

    private static string[]? NumberRange(string first, string last, int step)
    {
        if (!int.TryParse(first, out int start) || !int.TryParse(last, out int end))
        {
            return null;
        }

        int width = first.Length > 1 && first[0] == '0' ? first.Length : 0;
        return SteppedValues(start, end, step, number => number.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width, '0'));
    }

    private static string[]? LetterRange(string first, string last, int step) =>
        first.Length == 1 && last.Length == 1 && char.IsAsciiLetter(first[0]) && char.IsAsciiLetter(last[0])
            ? SteppedValues(first[0], last[0], step, letter => ((char)letter).ToString())
            : null;

    /// <summary>The <paramref name="format" />ted numbers from <paramref name="start" /> to <paramref name="end" /> by <paramref name="step" />; <see langword="null" /> when descending or more than <see cref="MaxUploadExpansions" />.</summary>
    private static string[]? SteppedValues(int start, int end, int step, Func<long, string> format)
    {
        if (end < start || ((long)end - start) / step >= MaxUploadExpansions)
        {
            return null;
        }

        List<string> values = [];
        for (long number = start; number <= end; number += step)
        {
            values.Add(format(number));
        }

        return [.. values];
    }
    /// <summary>An abstract socket's name lives in no file system, so the root cannot confine it.</summary>
    private static IReadOnlyList<McpValuePath> SocketPaths(string longName, string value) =>
        longName == "abstract-unix-socket"
            ? [new(value, "names an abstract Unix socket, which lives in no file system, so CURL_MCP_ROOT cannot confine it.")]
            : [new(value)];
}
