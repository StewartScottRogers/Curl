using System.Text.Json;
using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>
/// Reads a named environment from an <c>http-client.env.json</c> file (ADR-1073): a JSON object whose properties are
/// environments, each an object of variables. The <c>$shared</c> environment's variables are set first, then the named
/// one's, so a named environment replaces a shared value.
/// </summary>
internal static class HttpFileEnvironment
{
    /// <summary>The file, beside the .http file, that holds the environments.</summary>
    public const string FileName = "http-client.env.json";

    private const string Shared = "$shared";

    /// <summary>Gives the path of the environment file beside the .http file at <paramref name="httpFilePath" />.</summary>
    /// <param name="httpFilePath">The .http file's path, as given.</param>
    /// <returns>The path, keeping the directory part of <paramref name="httpFilePath" /> as it was written.</returns>
    public static string PathBeside(string httpFilePath)
    {
        int separator = Math.Max(httpFilePath.LastIndexOf('/'), httpFilePath.LastIndexOf('\\'));
        return httpFilePath[..(separator + 1)] + FileName;
    }

    /// <summary>Sets the variables of the environment named <paramref name="name" /> in the file at <paramref name="path" />.</summary>
    /// <param name="path">The environment file's path.</param>
    /// <param name="name">The environment's name.</param>
    /// <param name="context">Reads the file.</param>
    /// <param name="variables">Receives the variables.</param>
    /// <exception cref="ArgumentExpansionException">The file cannot be read (exit 26), is not a JSON object of objects, or has no such environment (exit 2).</exception>
    public static void Load(string path, string name, ArgumentExpansionContext context, HttpFileVariables variables)
    {
        byte[] bytes = context.ReadFile(path) ?? throw new ArgumentExpansionException($"http-file: cannot read environment file {path}", CurlExitCode.ReadError);
        using JsonDocument document = Parse(bytes, path);
        JsonElement root = ObjectOrThrow(document.RootElement, path);
        if (root.TryGetProperty(Shared, out JsonElement shared))
        {
            SetEach(ObjectOrThrow(shared, path), variables);
        }

        if (!root.TryGetProperty(name, out JsonElement environment))
        {
            throw new ArgumentExpansionException($"http-file: environment '{name}' is not in {path}", CurlExitCode.FailedInit);
        }

        SetEach(ObjectOrThrow(environment, path), variables);
    }

    private static JsonDocument Parse(byte[] bytes, string path)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            throw NotEnvironments(path);
        }
    }

    private static JsonElement ObjectOrThrow(JsonElement element, string path) =>
        element.ValueKind == JsonValueKind.Object ? element : throw NotEnvironments(path);

    private static ArgumentExpansionException NotEnvironments(string path) =>
        new($"http-file: {path} is not a JSON object of environments", CurlExitCode.FailedInit);

    private static void SetEach(JsonElement environment, HttpFileVariables variables)
    {
        foreach (JsonProperty variable in environment.EnumerateObject())
        {
            variables.Set(variable.Name, variable.Value.ValueKind == JsonValueKind.String ? variable.Value.GetString()! : variable.Value.GetRawText());
        }
    }
}
