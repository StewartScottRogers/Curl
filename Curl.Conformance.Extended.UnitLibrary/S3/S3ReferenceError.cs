namespace Curl.Conformance.Extended.S3;

/// <summary>An S3 error response: its status and the <c>Code</c> and <c>Message</c> of its <c>Error</c> body (API reference, "Error responses").</summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="Code">The error code, such as <c>NoSuchKey</c>.</param>
/// <param name="Message">The error message.</param>
public sealed record S3ReferenceError(int StatusCode, string Code, string Message);
