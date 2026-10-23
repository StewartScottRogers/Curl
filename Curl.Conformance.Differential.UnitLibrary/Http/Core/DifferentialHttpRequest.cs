namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// One request as server.py's routes see it: the method, the path (an absolute-form target reduced
/// to its path) split from the query at the first <c>?</c>, the headers by lower-cased name (the last
/// one wins), and the body as it arrived (chunk framing kept).
/// </summary>
public sealed record DifferentialHttpRequest(
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body);
