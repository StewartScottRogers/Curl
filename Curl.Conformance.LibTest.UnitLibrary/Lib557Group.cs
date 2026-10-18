namespace Curl.Conformance.LibTest;

/// <summary>
/// One of <c>lib557.c</c>'s <c>test_*</c> functions: its checks, and the line it prints on stdout when every one
/// passes and when one fails (<see langword="null"/> for the functions that only count their errors).
/// </summary>
/// <param name="Name">The name its failure lines start with, such as <c>unsigned short</c>.</param>
/// <param name="PassedLine">The line printed when every check passes.</param>
/// <param name="FailedLine">The line printed when a check fails.</param>
/// <param name="Checks">The checks.</param>
public sealed record Lib557Group(string Name, string? PassedLine, string? FailedLine, IReadOnlyList<Lib557Check> Checks);
