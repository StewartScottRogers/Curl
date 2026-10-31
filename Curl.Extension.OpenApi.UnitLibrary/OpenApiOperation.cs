namespace Curl.Extension.OpenApi;

/// <summary>One operation of an OpenAPI description, with everything the request needs resolved (OpenAPI 3.1 §4.8.10).</summary>
/// <param name="Id">Its <c>operationId</c>.</param>
/// <param name="Method">Its HTTP method, lower case, as the Path Item Object's member is named (§4.8.9.1).</param>
/// <param name="Path">Its path template, such as <c>/pets/{petId}</c> (§4.8.8.1).</param>
/// <param name="ServerUrl">The server URL its path is appended to, variables replaced by their defaults (§4.8.5, §4.8.6).</param>
/// <param name="Parameters">Its parameters: the Path Item's, overridden by the operation's of the same name and location, ignored ones left out.</param>
/// <param name="RequestBody">Its request body, or <see langword="null" /> when it takes none.</param>
internal sealed record OpenApiOperation(
    string Id,
    string Method,
    string Path,
    string ServerUrl,
    IReadOnlyList<OpenApiParameter> Parameters,
    OpenApiRequestBody? RequestBody);
