namespace Curl.Extension.CodeGeneration;

/// <summary>One file an option asked for: where it goes and what writes its text from the requests sent so far.</summary>
/// <param name="Path">The file named after <c>--csharp</c> or <c>--powershell</c>.</param>
/// <param name="Writer">Writes the file's text for the requests.</param>
internal sealed record CodeFile(string Path, Func<IReadOnlyList<GeneratedRequest>, string> Writer);
