namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>An error S3 returned, read from its <c>Error</c> body.</summary>
/// <param name="Code">The error code, such as <c>NoSuchKey</c>.</param>
/// <param name="Message">The error's description, or empty when S3 sent none.</param>
public sealed record S3ErrorResponse(string Code, string Message);
