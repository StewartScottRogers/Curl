namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// What reading one request produced: the bytes the request log keeps (raw head plus body) and the
/// request, or <see langword="null"/> when the request line has no method and target, which ends the
/// connection after the request is logged.
/// </summary>
public sealed record DifferentialHttpRequestRead(byte[] LoggedBytes, DifferentialHttpRequest? Request);
