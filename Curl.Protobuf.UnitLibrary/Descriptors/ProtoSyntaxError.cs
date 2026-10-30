namespace Curl.Protobuf.Descriptors;

/// <summary>Why <c>ProtoParser</c> could not read a <c>.proto</c> text, and where.</summary>
/// <param name="Line">The 1-based line the error starts on.</param>
/// <param name="Column">The 1-based column, counted in characters, the error starts at.</param>
/// <param name="Message">What is wrong, in one sentence.</param>
public sealed record ProtoSyntaxError(int Line, int Column, string Message);
