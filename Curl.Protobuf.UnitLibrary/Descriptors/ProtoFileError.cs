namespace Curl.Protobuf.Descriptors;

/// <summary>Why <see cref="ProtoFileSetReader" /> could not read a set of <c>.proto</c> files, and where.</summary>
/// <param name="Path">The import path of the file the error is in.</param>
/// <param name="Kind">What kind of error it is.</param>
/// <param name="Line">The 1-based line the error starts on; 0 for <see cref="ProtoFileErrorKind.FileNotFound" />.</param>
/// <param name="Column">The 1-based column the error starts at; 0 for <see cref="ProtoFileErrorKind.FileNotFound" />.</param>
/// <param name="Message">What is wrong, in one sentence.</param>
public sealed record ProtoFileError(string Path, ProtoFileErrorKind Kind, int Line, int Column, string Message);
