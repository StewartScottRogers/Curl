using Curl.Conformance.UnitProgram.Hashes;
using Curl.Core.Globbing;
using static Curl.Core.Globbing.WindowsOutputFileNameSanitizer;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/tunit/tool1604.c</c>: every row of its <c>sanitize_file_name</c>
/// table, the Windows (not MS-DOS) build's rows, through
/// <see cref="WindowsOutputFileNameSanitizer.SanitizeFileName(string?, SanitizeFlags, out string?)"/>. The C
/// program runs them only where <c>_WIN32</c> is defined; Curl's sanitizer is the same code on every
/// platform, so the port runs them everywhere.
/// </summary>
public sealed class Tool1604SanitizeFileName : IUnitProgram
{
    private static readonly (string? Input, SanitizeFlags Flags, string? Output, SanitizeResult Result)[] Rows =
    [
        ("", SanitizeFlags.None, "", SanitizeResult.Ok),
        ("normal filename", SanitizeFlags.None, "normal filename", SanitizeResult.Ok),
        ("control\tchar", SanitizeFlags.None, "control_char", SanitizeResult.Ok),
        ("banned*char", SanitizeFlags.None, "banned_char", SanitizeResult.Ok),
        ("f:foo", SanitizeFlags.None, "f_foo", SanitizeResult.Ok),
        ("f:foo", SanitizeFlags.AllowPath, "f:foo", SanitizeResult.Ok),
        (@"f:\foo", SanitizeFlags.None, "f__foo", SanitizeResult.Ok),
        (@"f:\foo", SanitizeFlags.AllowPath, @"f:\foo", SanitizeResult.Ok),
        ("f:/foo", SanitizeFlags.None, "f__foo", SanitizeResult.Ok),
        ("f:/foo", SanitizeFlags.AllowPath, "f:/foo", SanitizeResult.Ok),
        (@"\\?\C:\foo", SanitizeFlags.AllowPath, @"\\?\C:\foo", SanitizeResult.Ok),
        (@"\\?\C:\foo", SanitizeFlags.None, "____C__foo", SanitizeResult.Ok),
        ("foo:bar", SanitizeFlags.None, "foo_bar", SanitizeResult.Ok),
        ("foo|<>/bar\\\":?*baz", SanitizeFlags.None, "foo____bar_____baz", SanitizeResult.Ok),
        ("f:foo::$DATA", SanitizeFlags.None, "f_foo__$DATA", SanitizeResult.Ok),
        ("con . air", SanitizeFlags.None, "con _ air", SanitizeResult.Ok),
        ("con.air", SanitizeFlags.None, "con_air", SanitizeResult.Ok),
        ("con:/x", SanitizeFlags.None, "con__x", SanitizeResult.Ok),
        ("file . . . .  ..  .", SanitizeFlags.None, "file", SanitizeResult.Ok),
        ("foo . . ? . . ", SanitizeFlags.None, "foo . . _", SanitizeResult.Ok),
        ("com1", SanitizeFlags.None, "_com1", SanitizeResult.Ok),
        ("com1", SanitizeFlags.AllowReserved, "com1", SanitizeResult.Ok),
        (@"f:\com1", SanitizeFlags.None, "f__com1", SanitizeResult.Ok),
        (@"f:\com1", SanitizeFlags.AllowPath, @"f:\_com1", SanitizeResult.Ok),
        (@"f:\com1", SanitizeFlags.AllowReserved, "f__com1", SanitizeResult.Ok),
        (@"f:\com1", SanitizeFlags.AllowReserved | SanitizeFlags.AllowPath, @"f:\com1", SanitizeResult.Ok),
        (@"com1:\com1", SanitizeFlags.AllowPath, @"_com1:\_com1", SanitizeResult.Ok),
        (@"com1:\com1", SanitizeFlags.AllowReserved | SanitizeFlags.AllowPath, @"com1:\com1", SanitizeResult.Ok),
        (@"com1:\com1", SanitizeFlags.AllowReserved, "com1__com1", SanitizeResult.Ok),
        (@"\com1", SanitizeFlags.AllowPath, @"\_com1", SanitizeResult.Ok),
        (@"\\com1", SanitizeFlags.AllowPath, @"\\com1", SanitizeResult.Ok),
        (@"\\?\C:\com1", SanitizeFlags.AllowPath, @"\\?\C:\com1", SanitizeResult.Ok),
        ("CoM1", SanitizeFlags.None, "_CoM1", SanitizeResult.Ok),
        ("CoM1", SanitizeFlags.AllowReserved, "CoM1", SanitizeResult.Ok),
        ("COM56", SanitizeFlags.None, "COM56", SanitizeResult.Ok),
        (null, SanitizeFlags.None, null, SanitizeResult.BadArgument),
    ];

    /// <inheritdoc/>
    public string ProgramName => "tool1604";

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string? input, SanitizeFlags flags, string? expectedOutput, SanitizeResult expectedResult) in Rows)
        {
            SanitizeResult result = SanitizeFileName(input, flags, out string? output);
            checks.FailUnless(
                (result == expectedResult) & (output == expectedOutput),
                $"sanitize_file_name failed. input: {input ?? "(null)"} flags: {flags} output: {output ?? "(null)"} result: {result} expected output: {expectedOutput ?? "(null)"} expected result: {expectedResult}");
        }

        return Task.FromResult(checks.Result);
    }
}
