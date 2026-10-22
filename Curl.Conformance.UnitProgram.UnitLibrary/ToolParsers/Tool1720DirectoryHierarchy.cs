using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/tunit/tool1720.c</c>: every row of its <c>create_dir_hierarchy</c> table
/// through <see cref="OutputDirectoryHierarchy.DirectoriesToCreate(string, bool)"/>, each directory followed by
/// <c>|</c> as the C program's trace records it and <c>(null)</c> for none. The C program picks its rows by
/// <c>#ifdef</c>; the port runs the shared rows under both rule sets and the Windows rows under the Windows one.
/// </summary>
public sealed class Tool1720DirectoryHierarchy : IUnitProgram
{
    private static readonly (string Path, string Expected)[] SharedRows =
    [
        ("", "(null)"),
        ("filename", "(null)"),
        ("foo/bar/", "foo|foo/bar|"),
        ("foo/bar/filename", "foo|foo/bar|"),
        ("/foo/bar/filename", "/foo|/foo/bar|"),
    ];

    private static readonly (string Path, string Expected)[] WindowsRows =
    [
        ("C:/foo/bar/filename", "C:/foo|C:/foo/bar|"),
        ("C:foo/bar/filename", "C:foo|C:foo/bar|"),
        (@"foo\bar\filename", @"foo|foo\bar|"),
        (@"\foo\bar\filename", @"\foo|\foo\bar|"),
        (@"C:\foo\bar\filename", @"C:\foo|C:\foo\bar|"),
        (@"C:foo\bar\filename", @"C:foo|C:foo\bar|"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "tool1720";

    /// <summary>The trace the C program records for <paramref name="path"/>: each directory and <c>|</c>, or <c>(null)</c> for none.</summary>
    /// <param name="path">The output file's path.</param>
    /// <param name="runsOnWindows">Whether the Windows rule set applies.</param>
    /// <returns>The trace.</returns>
    public static string Trace(string path, bool runsOnWindows)
    {
        string trace = string.Concat(OutputDirectoryHierarchy.DirectoriesToCreate(path, runsOnWindows).Select(directory => directory + "|"));
        return trace.Length == 0 ? "(null)" : trace;
    }

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string path, string expected) in SharedRows)
        {
            Check(checks, Trace(path, runsOnWindows: false), expected);
            Check(checks, Trace(path, runsOnWindows: true), expected);
        }

        foreach ((string path, string expected) in WindowsRows)
        {
            Check(checks, Trace(path, runsOnWindows: true), expected);
        }

        return Task.FromResult(checks.Result);
    }

    private static void Check(UnitChecks checks, string actual, string expected) =>
        checks.FailUnless(actual == expected, $"Expected '{expected}' got '{actual}'");
}
