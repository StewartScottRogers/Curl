using System.Globalization;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Engine;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/tunit/tool1621.c</c>: every row of its <c>stripcredentials</c> table
/// (all protocols built in) through <see cref="OriginUrlCredentials.Strip(string)"/>, writing the C
/// program's <c>Test N got input ...</c> line for each.
/// </summary>
public sealed class Tool1621StripCredentials : IUnitProgram
{
    private static readonly (string Input, string Output)[] Rows =
    [
        ("ninja://foo@example.com", "(null)"),
        ("pop3s://foo@example.com", "pop3s://example.com/"),
        ("ldap://foo@example.com", "ldap://example.com/"),
        ("https://foo@example.com", "https://example.com/"),
        ("https://localhost:45", "https://localhost:45/"),
        ("https://foo@localhost:45", "https://localhost:45/"),
        ("https://user:pass@localhost:45", "https://localhost:45/"),
        ("http://daniel:password@localhost", "http://localhost/"),
        ("http://daniel@localhost", "http://localhost/"),
        ("http://localhost/", "http://localhost/"),
        ("http://odd%40host/", "(null)"),
        ("http://user@odd%40host/", "(null)"),
        ("http://host/@path/", "http://host/@path/"),
        ("http://emptypw:@host/", "http://host/"),
        ("http://:emptyuser@host/", "http://host/"),
        ("http://odd%40user@host/", "http://host/"),
        ("http://only%40one%40host/", "(null)"),
        ("http://odder%3auser@host/", "http://host/"),
    ];

    /// <inheritdoc/>
    public string ProgramName => "tool1621";

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        StandardOutputLines output = new(context);
        for (int row = 0; row < Rows.Length; row++)
        {
            (string input, string expected) = Rows[row];
            string stripped = OriginUrlCredentials.Strip(input) ?? "(null)";
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Test {row} got input \"{input}\", output: \"{stripped}\", expected: \"{expected}\""), cancellationToken).ConfigureAwait(false);
            checks.FailUnless(stripped == expected, expected);
        }

        return checks.Result;
    }
}
