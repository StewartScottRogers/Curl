namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>One of unit3200's file contents and what its <c>switch</c> case checks.</summary>
/// <param name="Contents">The file's bytes, one per character.</param>
/// <param name="Lines">Each line read in turn and the failure line for it.</param>
/// <param name="EndsAtEndOfFile">
/// Whether the last read must meet the end of the file, else leave the buffer empty (a line too large,
/// which <see cref="Curl.Core.CurlFileLineReader.ReadLineAsync(CancellationToken)"/> answers with <see langword="null"/>).
/// </param>
/// <param name="LastReadMessage">The failure line for the last read.</param>
public sealed record GetLineFileCase(string Contents, IReadOnlyList<(string Line, string Message)> Lines, bool EndsAtEndOfFile, string LastReadMessage);
