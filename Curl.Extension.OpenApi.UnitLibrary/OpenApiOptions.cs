using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.OpenApi;

/// <summary>The command-line options the OpenAPI extension adds, and its description (ADR-1083).</summary>
public static class OpenApiOptions
{
    /// <summary>Gets <c>--openapi &lt;spec&gt;</c>: the OpenAPI description, a JSON file, whose operation the group calls.</summary>
    public static ExtensionOption Specification { get; } = new(
        "openapi",
        ExtensionOptionArgument.Value,
        "Build the request from the OpenAPI description in <spec> (JSON)");

    /// <summary>Gets <c>--operation &lt;id&gt;</c>: the <c>operationId</c> of the operation to call, after <c>--openapi</c>.</summary>
    public static ExtensionOption Operation { get; } = new(
        "operation",
        ExtensionOptionArgument.Value,
        "operationId of the --openapi operation to call; name=value arguments give its parameters and body");

    /// <summary>Gets <c>--openapi-server &lt;url&gt;</c>: the server URL to use in place of the description's own.</summary>
    public static ExtensionOption Server { get; } = new(
        "openapi-server",
        ExtensionOptionArgument.Value,
        "Server URL to call the --openapi operation on, in place of the description's servers");

    /// <summary>Gets what <c>curl --extended-help</c> and <c>curl --ai-help extensions</c> say about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "openapi",
        "Builds a request from an OpenAPI 3 description: --openapi <spec> --operation <id> name=value... gives the method, server URL, path, query, header and cookie parameters by their style, and a JSON, form or multipart body.",
        [],
        [Specification, Operation, Server],
        new Uri("https://spec.openapis.org/oas/v3.1.1.html"));
}
