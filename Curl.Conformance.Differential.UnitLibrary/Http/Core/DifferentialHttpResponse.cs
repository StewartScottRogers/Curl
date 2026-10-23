namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// The arguments of server.py's <c>resp</c>: a status, headers in order, a body, the version on the status
/// line, whether a <c>Content-Length</c> is added, and whether <c>Connection: close</c> is added.
/// </summary>
public sealed record DifferentialHttpResponse(
    int Status,
    IReadOnlyList<(string Name, string Value)> Headers,
    byte[] Body,
    string Version = "HTTP/1.1",
    bool AddsContentLength = true,
    bool AddsConnectionClose = false);
