using System.Globalization;
using Curl.Conformance.UnitProgram.Hashes;
using Curl.Original.Protocol.Ftp;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/unit/unit1668.c</c>: every row of its <c>ftp_213_date</c> table through
/// <c>FtpModificationTime.TryReadDate</c>, the date read as <c>%04d-%02d-%02d %02d:%02d:%02d</c>; the C
/// program's <c>\xNN</c> bytes are the characters of the same value. Like the C program it counts one
/// failure in all, <c>some tests of ftp_213_date() failed</c>, after writing a line for each failed row.
/// </summary>
public sealed class Unit1668Ftp213Date : IUnitProgram
{
    private const string Time = "1998-03-20 11:42:34";

    private static readonly (string In, string Out, bool Accepted)[] Rows =
    [
        ("19980320114234.123", Time, true),
        ("19980320114234.000", Time, true),
        ("19980320114234.aaa", Time, true),
        ("19980320114234.999", Time, true),
        ("19980320114234", Time, true),
        ("09980320114234", "0998-03-20 11:42:34", true),
        ("10980320114234", "1098-03-20 11:42:34", true),
        ("19080320114234", "1908-03-20 11:42:34", true),
        ("19900320114234", "1990-03-20 11:42:34", true),
        ("19980020114234", "1998-00-20 11:42:34", true),
        ("19980300114234", "1998-03-00 11:42:34", true),
        ("19980320014234", "1998-03-20 01:42:34", true),
        ("19980320110234", "1998-03-20 11:02:34", true),
        ("19980320114204", "1998-03-20 11:42:04", true),
        ("19980320114230", "1998-03-20 11:42:30", true),
        ("19980320256565", "", false),
        ("19980320236565", "", false),
        ("19980320235865", "", false),
        ("19980231114234", "1998-02-31 11:42:34", true),
        (":9980231114234", "", false),
        ("a9980231114234", "", false),
        ("1:980231114234", "", false),
        ("1a980231114234", "", false),
        ("19:80231114234", "", false),
        ("19b80231114234", "", false),
        ("199:0231114234", "", false),
        ("199c0231114234", "", false),
        ("1998:231114234", "", false),
        ("1998d231114234", "", false),
        ("19980:31114234", "", false),
        ("19980e31114234", "", false),
        ("199802:1114234", "", false),
        ("199802f1114234", "", false),
        ("1998023:114234", "", false),
        ("1998023/114234", "", false),
        ("19980231:14234", "", false),
        ("19980231x14234", "", false),
        ("199802311:4234", "", false),
        ("199802311z4234", "", false),
        ("1998023111:234", "", false),
        ("1998023111&234", "", false),
        ("19980231114:34", "", false),
        ("19980231114#34", "", false),
        ("199802311142:4", "", false),
        ("199802311142!4", "", false),
        ("1998023111423:", "", false),
        ("1998023111423@", "", false),
        ("19980320114234--", Time, true),
        (" 19980320114234", "", false),
        ("1998032011423", "", false),
        ("199803201142", "", false),
        ("19980320114", "", false),
        ("199803201", "", false),
        ("19980320", "", false),
        ("1998032", "", false),
        ("199803", "", false),
        ("19980", "", false),
        ("1998", "", false),
        ("199", "", false),
        ("19", "", false),
        ("1", "", false),
        ("", "", false),
        ("20260320114234.123", "2026-03-20 11:42:34", true),
        ("30260320114234.123", "3026-03-20 11:42:34", true),
        ("40260320114234.123", "4026-03-20 11:42:34", true),
        ("19980320114234ÿ", Time, true),
        ("1998~320114234ÿ", "", false),
        ("1998\u007f320114234ÿ", "", false),
        ("1998\u0080320114234ÿ", "", false),
        ("1998\u0081320114234ÿ", "", false),
        ("1998\u0082320114234ÿ", "", false),
        ("Ì", "", false),
        ("U", "", false),
    ];

    /// <inheritdoc/>
    public string ProgramName => "unit1668";

    /// <summary>Checks one row as the C program's <c>test1668</c> does, writing a line to standard error when it fails.</summary>
    /// <param name="standardError">Where a failed row's line is written.</param>
    /// <param name="row">The row's index.</param>
    /// <param name="spec">The input, the date expected and whether it is accepted.</param>
    /// <returns><see langword="true"/> when the row passed.</returns>
    public static bool CheckRow(TextWriter standardError, int row, (string In, string Out, bool Accepted) spec)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        bool accepted = FtpModificationTime.TryReadDate(spec.In, out FtpMdtmDate date);
        if (accepted != spec.Accepted)
        {
            standardError.WriteLine(string.Create(CultureInfo.InvariantCulture, $"test {row}: expected result {(spec.Accepted ? 1 : 0)}, got {(accepted ? 1 : 0)} for {spec.In}"));
            return false;
        }

        string text = string.Create(CultureInfo.InvariantCulture, $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2} {date.Hour:D2}:{date.Minute:D2}:{date.Second:D2}");
        if (accepted && text != spec.Out)
        {
            standardError.WriteLine(string.Create(CultureInfo.InvariantCulture, $"test {row}: (from input {spec.In}) got '{text}' expected '{spec.Out}'"));
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        bool allPassed = true;
        for (int row = 0; row < Rows.Length; row++)
        {
            allPassed &= CheckRow(context.StandardError, row, Rows[row]);
        }

        checks.FailUnless(allPassed, "some tests of ftp_213_date() failed");
        return Task.FromResult(checks.Result);
    }
}
