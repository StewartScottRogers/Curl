using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit1395.c</c>: runs every one of its 106 <c>pairs</c> rows,
/// in its order, through <c>dedotdotify</c> (Curl's <see cref="CurlUrlDotSegments.Dedotdotify"/>) and
/// counts each row whose output differs, a <see langword="null"/> output meaning "nothing to remove".
/// </summary>
/// <remarks>
/// As in the C program, each mismatched row is one <c>fail()</c> and the closing
/// <c>fail_if(fails, ...)</c> one more, so the failure count is the mismatches plus one when there are any.
/// </remarks>
public sealed class Unit1395DotSegmentRemoval : IUnitProgram
{
    private readonly Func<string, string?> dedotdotify;

    /// <summary>Creates the program over Curl's <c>dedotdotify</c>.</summary>
    public Unit1395DotSegmentRemoval()
        : this(CurlUrlDotSegments.Dedotdotify)
    {
    }

    /// <summary>Creates the program over another <c>dedotdotify</c>, so a test can watch it fail.</summary>
    /// <param name="dedotdotify">Returns the path with its dot segments removed, or <see langword="null"/> when there are none.</param>
    public Unit1395DotSegmentRemoval(Func<string, string?> dedotdotify)
    {
        ArgumentNullException.ThrowIfNull(dedotdotify);
        this.dedotdotify = dedotdotify;
    }

    /// <summary>The C program's <c>pairs</c> table, row for row: the input and the output it must give.</summary>
    public static IReadOnlyList<(string Input, string? Output)> Pairs { get; } =
    [
        ("/%2f%2e%2e%2f/../a", "/a"),
        ("/%2f%2e%2e%2f/../", "/"),
        ("/%2f%2e%2e%2f/.", "/%2f%2e%2e%2f/"),
        ("/%2f%2e%2e%2f/", null),
        ("/%2f%2e%2e%2f", null),
        ("/%2f%2e%2e%2", null),
        ("/%2f%2e%2e%", null),
        ("/%2f%2e%2e", "/%2f%2e%2e"),
        ("/%2f%2e%2", null),
        ("/%2f%2e%", null),
        ("/%2f%2e", "/%2f%2e"),
        ("/%2f%2", null),
        ("/%2f%", null),
        ("/%2f", null),
        ("/%2", null),
        ("%2f%2e%2e%2f/../a", "%2f%2e%2e%2f/a"),
        ("%2f%2e%2e%2f/../", "%2f%2e%2e%2f/"),
        ("%2f%2e%2e%2f/.", "%2f%2e%2e%2f/"),
        ("%2f%2e%2e%2f/", null),
        ("%2f%2e%2e%2f", null),
        ("%2f%2e%2e%2", null),
        ("%2f%2e%2e%", null),
        ("%2f%2e%2e", "%2f%2e%2e"),
        ("%2f%2e%2", null),
        ("%2f%2e%", null),
        ("%2f%2e", "%2f%2e"),
        ("%2f%2", null),
        ("%2f%", null),
        ("%2f", null),
        ("%2", null),
        ("%", null),
        ("2", null),
        ("e", null),
        (".", null),
        ("./", ""),
        ("..", ""),
        ("../", ""),
        ("../a", "a"),
        ("///moo.", "///moo."),
        (".///moo.", "//moo."),
        ("./moo..", "moo.."),
        ("./moo../", "moo../"),
        ("./moo../.m", "moo../.m"),
        ("./moo", "moo"),
        ("../moo", "moo"),
        ("../moo?", "moo?"),
        ("../moo?#", "moo?#"),
        ("../moo?#?..", "moo?#?.."),
        ("/../moo/..", "/"),
        ("/a/c/%2e%2E/b", "/a/b"),
        ("/a/%2e/g", "/a/g"),
        ("/a/b/c/./g", "/a/b/c/g"),
        ("/a/c/../b", "/a/b"),
        ("/a/b/c/./../../g", "/a/g"),
        ("/a/b/c/./%2e%2E/../g", "/a/g"),
        ("/a/b/c/./../%2e%2E/g", "/a/g"),
        ("/a/b/c/%2E/%2e%2E/%2e%2E/g", "/a/g"),
        ("mid/content=5/../6", "mid/6"),
        ("/hello/../moo", "/moo"),
        ("/1/../1", "/1"),
        ("/1/./1", "/1/1"),
        ("/1/%2e/1", "/1/1"),
        ("/1/%2E/1", "/1/1"),
        ("/1/..", "/"),
        ("/1/.", "/1/"),
        ("/1/%2e", "/1/"),
        ("/1/%2E", "/1/"),
        ("/1/./..", "/"),
        ("/1/%2e/.%2E", "/"),
        ("/1/./%2e.", "/"),
        ("/1/./../2", "/2"),
        ("/hello/1/./../2", "/hello/2"),
        ("test/this", null),
        ("test/this/../now", "test/now"),
        ("/1../moo../foo", "/1../moo../foo"),
        ("/../../moo", "/moo"),
        ("/../../moo?", "/moo?"),
        ("/123?", null),
        ("/", null),
        ("", null),
        ("/.../", "/.../"),
        ("/.", "/"),
        ("/..", "/"),
        ("/moo/..", "/"),
        ("/..", "/"),
        ("/.", "/"),
        ("////../a", "///a"),
        ("/../../../../../../", "/"),
        ("/..//..//", "//"),
        ("/.config/../ssh", "/ssh"),
        ("/..config/..", "/"),
        ("/.../a", "/.../a"),
        ("/a/%2E%2e/b", "/b"),
        ("/a/%2e./b", "/b"),
        ("/a/.%2e/b", "/b"),
        ("/%2f..%2f", null),
        ("/a/b/.", "/a/b/"),
        ("/a/b/..", "/a/"),
        ("well-known", null),
        (".well-known", null),
        ("..well-known", null),
        ("...well-known", null),
        ("....well-known", null),
        ("%2ewell-known", null),
        ("%2Ewell-known", null),
        ("../.well-known", ".well-known"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "unit1395";

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int fails = 0;
        for (int index = 0; index < Pairs.Count; index++)
        {
            (string input, string? expected) = Pairs[index];
            string? output = dedotdotify(input);
            if (string.Equals(output, expected, StringComparison.Ordinal))
            {
                context.StandardError.Write($"Test {index}: OK\n");
                continue;
            }

            context.StandardError.Write($"Test {index}: '{input}' gave '{output ?? "(null)"}' instead of '{expected ?? "(null)"}'\n");
            context.StandardError.Write("unit1395.c: Test case output mismatched\n");
            fails++;
        }

        return Task.FromResult(new UnitProgramResult(fails == 0 ? 0 : fails + 1));
    }
}
