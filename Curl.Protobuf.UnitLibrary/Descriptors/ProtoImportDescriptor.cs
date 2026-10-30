namespace Curl.Protobuf.Descriptors;

/// <summary>One <c>import</c> statement of a <c>.proto</c> file.</summary>
/// <param name="Path">The imported file's path as written, for an <see cref="IProtoFileReader" /> to read.</param>
/// <param name="Kind">Whether the import is a default, <c>public</c> or <c>weak</c> one.</param>
/// <param name="Line">The 1-based line of the statement's <c>import</c> keyword.</param>
/// <param name="Column">The 1-based column of the statement's <c>import</c> keyword.</param>
public sealed record ProtoImportDescriptor(string Path, ProtoImportKind Kind, int Line, int Column);
