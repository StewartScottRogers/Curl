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

    private const string OutputDirective = "output{";

    private const string FormHeadersParameter = ";headers=";

    private static readonly char[] GlobCharacters = ['{', '}', '[', ']'];

    private static readonly char[] UrlEncodedSeparators = ['=', '@'];

    /// <summary>The paths <paramref name="value" />, given to the option <paramref name="longName" />, names.</summary>
    /// <param name="longName">The option's long name without <c>--</c>.</param>
    /// <param name="use">How the option uses a path; never <see cref="McpOptionPathUse.None" />.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>The paths to check, in the order the value names them.</returns>
    internal static IReadOnlyList<McpValuePath> PathsIn(string longName, McpOptionPathUse use, string value) => use switch
    {
        McpOptionPathUse.ValueEmbedded => EmbeddedPaths(longName, value),
        McpOptionPathUse.ReadsFileOrLiteral => FileOrLiteralPaths(longName, value),
        McpOptionPathUse.WritesFile => WrittenFilePaths(longName, value),
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
    /// A written file: <c>-</c> is standard output. <c>-o</c> replaces <c>#1</c> and the like with
    /// text from the URL's glob, so such a name cannot be checked before curl runs.
    /// </summary>
    private static IReadOnlyList<McpValuePath> WrittenFilePaths(string longName, string value)
    {
        if (longName == "output" && HasGlobReference(value))
        {
            return [new(value, "names a file with a #<n> glob reference, which curl fills from the URL, so it cannot be checked before curl runs.")];
        }

        return FilePath(value);
    }

    private static bool HasGlobReference(string value)
    {
        for (int index = value.IndexOf('#', StringComparison.Ordinal); index >= 0; index = value.IndexOf('#', index + 1))
        {
            if (index + 1 < value.Length && char.IsAsciiDigit(value[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary><c>-T -</c> and <c>-T .</c> read standard input; a globbed <c>-T</c> name cannot be checked before curl expands it.</summary>
    private static IReadOnlyList<McpValuePath> UploadPaths(string value)
    {
        if (value is StandardStream or ".")
        {
            return [];
        }

        return value.IndexOfAny(GlobCharacters) >= 0
            ? [new(value, "names files with glob characters ({}, []), whose expanded paths cannot be checked before curl runs.")]
            : [new(value)];
    }

    /// <summary>An abstract socket's name lives in no file system, so the root cannot confine it.</summary>
    private static IReadOnlyList<McpValuePath> SocketPaths(string longName, string value) =>
        longName == "abstract-unix-socket"
            ? [new(value, "names an abstract Unix socket, which lives in no file system, so CURL_MCP_ROOT cannot confine it.")]
            : [new(value)];
}
