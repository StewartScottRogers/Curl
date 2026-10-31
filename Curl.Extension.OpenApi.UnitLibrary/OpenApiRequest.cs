namespace Curl.Extension.OpenApi;

/// <summary>
/// Builds the curl arguments that call an operation with the <c>name=value</c> arguments given (OpenAPI 3.1
/// §4.8.8.1, §4.8.12, §4.8.13): each value that names a parameter goes in its place by its style, and the rest
/// become the request body.
/// </summary>
internal static class OpenApiRequest
{
    /// <summary>Gives the request's curl arguments: the method, the URL, then headers, cookies and body.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="serverUrl">The server URL to use in place of the operation's, or <see langword="null" />.</param>
    /// <param name="values">Each name's values, in command-line order.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> Arguments(OpenApiOperation operation, string? serverUrl, OrderedDictionary<string, List<string>> values)
    {
        string path = operation.Path;
        List<string> query = [];
        List<string> headers = [];
        List<string> cookies = [];
        foreach (OpenApiParameter parameter in operation.Parameters)
        {
            if (!values.Remove(parameter.Name, out List<string>? given))
            {
                if (parameter.Required)
                {
                    throw OpenApiFailure.Of($"operation {operation.Id} needs parameter {parameter.Name}");
                }

                continue;
            }

            string serialized = OpenApiParameterSerializer.Serialize(parameter, given);
            switch (parameter.In)
            {
                case "path":
                    path = path.Replace($"{{{parameter.Name}}}", serialized, StringComparison.Ordinal);
                    break;
                case "query":
                    query.Add(serialized);
                    break;
                case "header":
                    headers.Add($"{parameter.Name}: {serialized}");
                    break;
                default:
                    cookies.Add(serialized);
                    break;
            }
        }

        return Assemble(operation.Method, Url(serverUrl ?? operation.ServerUrl, path, query), headers, cookies, Body(operation, values));
    }

    private static List<string> Assemble(string method, string url, List<string> headers, List<string> cookies, IReadOnlyList<string> body)
    {
        List<string> arguments = [.. MethodArguments(method, body.Count > 0), url];
        arguments.AddRange(url.AsSpan().IndexOfAny("[]{}") >= 0 ? ["--globoff"] : []);
        arguments.AddRange(headers.SelectMany(header => new[] { "-H", header }));
        arguments.AddRange(cookies.Count > 0 ? ["-b", string.Join("; ", cookies)] : []);
        arguments.AddRange(body);
        return arguments;
    }

    private static string Url(string serverUrl, string path, List<string> query)
    {
        if (!serverUrl.Contains("://", StringComparison.Ordinal))
        {
            throw OpenApiFailure.Of($"server URL {serverUrl} is relative; give --openapi-server <url>");
        }

        return serverUrl.TrimEnd('/') + path + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
    }

    private static IReadOnlyList<string> Body(OpenApiOperation operation, OrderedDictionary<string, List<string>> values)
    {
        if (values.Count > 0 && operation.RequestBody is null)
        {
            throw OpenApiFailure.Of($"operation {operation.Id} has no parameter or request body for {values.GetAt(0).Key}");
        }

        return operation.RequestBody?.Arguments(values) ?? [];
    }

    private static IReadOnlyList<string> MethodArguments(string method, bool hasBody) => (method, hasBody) switch
    {
        ("head", _) => ["-I"],
        ("get", false) or ("post", true) => [],
        _ => ["-X", method.ToUpperInvariant()],
    };
}
