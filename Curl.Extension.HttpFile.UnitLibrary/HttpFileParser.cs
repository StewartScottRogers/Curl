using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>
/// Reads the requests of a .http file (ADR-1073), as the Visual Studio and VS Code REST Client formats write them:
/// requests separated by lines starting with <c>###</c>; before a request line, blank lines, <c>#</c> and <c>//</c>
/// comments and <c>@name = value</c> file variables; the request line <c>[METHOD] URL [HTTP/version]</c>; <c>?</c> and
/// <c>&amp;</c> query lines right after it; header lines up to the first blank line; and the body up to the next
/// separator, its trailing blank lines dropped, or a single <c>&lt; path</c> line naming a file to send.
/// </summary>
internal sealed class HttpFileParser
{
    private const string Separator = "###";

    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "GET", "HEAD", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "TRACE", "CONNECT",
    };

    private readonly HttpFileVariables variables;

    private readonly string lineEnding;

    private readonly List<HttpFileRequest> requests = [];

    private readonly List<string> headers = [];

    private readonly List<string> bodyLines = [];

    private string? method;

    private string url = string.Empty;

    private bool inBody;

    private HttpFileParser(HttpFileVariables variables, string lineEnding)
    {
        this.variables = variables;
        this.lineEnding = lineEnding;
    }

    /// <summary>Gives the requests in <paramref name="text" />, in file order, with their variables substituted.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="variables">The environment's variables; the file's own are added as they are read.</param>
    /// <returns>The requests.</returns>
    /// <exception cref="ArgumentExpansionException">A header line has no colon, or a reference names no variable (exit 2).</exception>
    public static IReadOnlyList<HttpFileRequest> Parse(string text, HttpFileVariables variables)
    {
        HttpFileParser parser = new(variables, text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n");
        foreach (string line in text.Split('\n'))
        {
            parser.Read(line.TrimEnd('\r'));
        }

        parser.FinishRequest();
        return parser.requests;
    }

    private static bool IsComment(string line) =>
        line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal);

    private void Read(string line)
    {
        if (line.StartsWith(Separator, StringComparison.Ordinal))
        {
            FinishRequest();
        }
        else if (method is null)
        {
            ReadBeforeRequest(line.Trim());
        }
        else if (inBody)
        {
            bodyLines.Add(line);
        }
        else
        {
            ReadHeaderLine(line.Trim());
        }
    }

    private void ReadBeforeRequest(string line)
    {
        if (line.Length == 0 || IsComment(line))
        {
            return;
        }

        int equals = line.IndexOf('=', StringComparison.Ordinal);
        if (line.StartsWith('@') && equals > 0)
        {
            variables.Define(line[1..equals].Trim(), line[(equals + 1)..].Trim());
            return;
        }

        ReadRequestLine(line);
    }

    /// <summary>Reads <c>[METHOD] URL [HTTP/version]</c>; a first word that is not a method is the URL of a GET.</summary>
    private void ReadRequestLine(string line)
    {
        string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool named = words.Length > 1 && Methods.Contains(words[0]);
        method = named ? words[0] : "GET";
        url = variables.Substitute(named ? words[1] : words[0]);
    }

    private void ReadHeaderLine(string line)
    {
        if (line.Length == 0)
        {
            inBody = true;
        }
        else if (headers.Count == 0 && (line.StartsWith('?') || line.StartsWith('&')))
        {
            url += variables.Substitute(line);
        }
        else if (!IsComment(line))
        {
            headers.Add(HeaderOrThrow(variables.Substitute(line)));
        }
    }

    private static string HeaderOrThrow(string line) =>
        line.IndexOf(':', StringComparison.Ordinal) > 0
            ? line
            : throw new ArgumentExpansionException($"http-file: '{line}' is not a header line", CurlExitCode.FailedInit);

    /// <summary>Adds the request read so far, if any, and starts the next.</summary>
    private void FinishRequest()
    {
        if (method is not null)
        {
            int count = bodyLines.Count;
            while (count > 0 && bodyLines[count - 1].Trim().Length == 0)
            {
                count--;
            }

            AddRequest(bodyLines.Take(count).ToList());
        }

        method = null;
        inBody = false;
        headers.Clear();
        bodyLines.Clear();
    }

    private void AddRequest(List<string> body)
    {
        string? bodyFile = body.Count == 1 && body[0].StartsWith("< ", StringComparison.Ordinal) ? variables.Substitute(body[0][2..].Trim()) : null;
        string? inline = body.Count == 0 || bodyFile is not null ? null : variables.Substitute(string.Join(lineEnding, body));
        requests.Add(new HttpFileRequest(method!, url, [.. headers], inline, bodyFile));
    }
}
