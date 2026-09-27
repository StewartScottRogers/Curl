namespace Curl.Conformance;

/// <summary>A test case's command split into arguments by <see cref="UpstreamCommandLineSplitter"/>.</summary>
/// <param name="arguments">The arguments, in order.</param>
/// <param name="unsupportedShellSyntax">The first unquoted shell character that was not carried out, or <see langword="null"/>.</param>
internal sealed class UpstreamCommandLine(IReadOnlyList<string> arguments, char? unsupportedShellSyntax)
{
    /// <summary>The arguments, in order.</summary>
    public IReadOnlyList<string> Arguments { get; } = arguments;

    /// <summary>
    /// The first unquoted <c>|</c>, <c>;</c>, <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>$</c> or
    /// <c>`</c>, or unescaped <c>$</c> or <c>`</c> inside double quotes, which a shell would act on and the harness does not; <see langword="null"/> when there is none.
    /// </summary>
    public char? UnsupportedShellSyntax { get; } = unsupportedShellSyntax;
}
