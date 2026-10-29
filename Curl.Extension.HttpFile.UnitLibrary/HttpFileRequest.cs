namespace Curl.Extension.HttpFile;

/// <summary>One request of a .http file, its variables substituted, and the curl arguments that send it (ADR-1073).</summary>
/// <param name="Method">The request line's method, such as <c>GET</c>.</param>
/// <param name="Url">The URL, with any <c>?</c> and <c>&amp;</c> continuation lines appended.</param>
/// <param name="Headers">Each header line, as <c>Name: value</c>.</param>
/// <param name="Body">The body; <see langword="null" /> when the request has none.</param>
/// <param name="BodyFile">The file a <c>&lt; path</c> body line names; <see langword="null" /> when the body is inline or absent.</param>
internal sealed record HttpFileRequest(string Method, string Url, IReadOnlyList<string> Headers, string? Body, string? BodyFile)
{
    /// <summary>Gives the curl arguments that send this request: its method, URL, headers and body.</summary>
    /// <returns>The arguments, in order.</returns>
    public IReadOnlyList<string> Arguments()
    {
        List<string> arguments = [.. MethodArguments(), Url];
        foreach (string header in Headers)
        {
            arguments.Add("-H");
            arguments.Add(CurlHeader(header));
        }

        arguments.AddRange(BodyArguments());
        return arguments;
    }

    /// <summary>
    /// Gives <c>--head</c> for HEAD, nothing where curl already infers the method (GET with no body, POST with one),
    /// else <c>-X</c> and the method.
    /// </summary>
    private IEnumerable<string> MethodArguments()
    {
        if (Method == "HEAD")
        {
            return ["--head"];
        }

        string inferred = Body is not null || BodyFile is not null ? "POST" : "GET";
        return Method == inferred ? [] : ["-X", Method];
    }

    private IEnumerable<string> BodyArguments()
    {
        if (BodyFile is not null)
        {
            return ["--data-binary", "@" + BodyFile];
        }

        return Body is null ? [] : ["--data-raw", Body];
    }

    /// <summary>Gives a header with an empty value as <c>Name;</c>, which curl sends empty, where <c>Name:</c> would remove it.</summary>
    private static string CurlHeader(string header)
    {
        int colon = header.IndexOf(':', StringComparison.Ordinal);
        return header[(colon + 1)..].Trim().Length == 0 ? header[..colon].TrimEnd() + ";" : header;
    }
}
