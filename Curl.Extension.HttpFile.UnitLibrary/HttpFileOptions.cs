using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.HttpFile;

/// <summary>The command-line options the .http file extension adds, and its description (ADR-1073).</summary>
public static class HttpFileOptions
{
    /// <summary>Gets <c>--http-file &lt;file&gt;</c>: runs every request in the .http file, one <c>--next</c> group each.</summary>
    public static ExtensionOption File { get; } = new(
        "http-file",
        ExtensionOptionArgument.Value,
        "Run the requests in the .http request file <file>");

    /// <summary>Gets <c>--http-env &lt;name&gt;</c>: the environment of <c>http-client.env.json</c>, beside the file, whose variables the requests use.</summary>
    public static ExtensionOption Environment { get; } = new(
        "http-env",
        ExtensionOptionArgument.Value,
        "Environment in http-client.env.json beside the --http-file to use");

    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "http-file",
        "Runs the requests in a Visual Studio / VS Code REST Client .http file, with file variables, {{$processEnv}} and named environments from http-client.env.json.",
        [],
        [File, Environment],
        new Uri("https://learn.microsoft.com/aspnet/core/test/http-files"));
}
