namespace Curl.Extended.Protocol.GoogleCloudStorage.Xml;

/// <summary>An error Cloud Storage returned, read from its <c>Error</c> body.</summary>
/// <param name="Code">The error code, such as <c>NoSuchKey</c>.</param>
/// <param name="Message">The error's description, or empty when Cloud Storage sent none.</param>
/// <param name="Details">The error's further detail, or <see langword="null"/> when Cloud Storage sent none.</param>
public sealed record GcsErrorResponse(string Code, string Message, string? Details);
