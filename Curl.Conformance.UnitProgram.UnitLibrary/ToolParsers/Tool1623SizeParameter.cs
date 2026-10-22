using System.Globalization;
using Curl.Cli;
using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// Port of curl 8.22.0's <c>tests/tunit/tool1623.c</c>: every row of its <c>GetSizeParameter</c> table
/// through <see cref="CommandLineNumber.ParseSize(string, string, out long)"/>, the <c>--limit-rate</c>
/// size parser, with <c>PARAM_BAD_USE</c>, <c>PARAM_NUMBER_TOO_LARGE</c> and <c>PARAM_BAD_NUMERIC</c> read
/// from the refusal it returns.
/// </summary>
public sealed class Tool1623SizeParameter : IUnitProgram
{
    private const string Option = "--limit-rate";

    private static readonly (string Input, long Amount, ParameterError Error)[] Rows =
    [
        ("0", 0, ParameterError.Ok),
        ("00", 0, ParameterError.Ok),
        ("000", 0, ParameterError.Ok),
        ("1", 1, ParameterError.Ok),
        ("1b", 1, ParameterError.Ok),
        ("99B", 99, ParameterError.Ok),
        ("2", 2, ParameterError.Ok),
        ("3", 3, ParameterError.Ok),
        ("4", 4, ParameterError.Ok),
        ("5", 5, ParameterError.Ok),
        ("6", 6, ParameterError.Ok),
        ("7", 7, ParameterError.Ok),
        ("77", 77, ParameterError.Ok),
        ("8", 8, ParameterError.Ok),
        ("9", 9, ParameterError.Ok),
        ("10", 10, ParameterError.Ok),
        ("010", 10, ParameterError.Ok),
        ("000000000000000000000000000000000010", 10, ParameterError.Ok),
        ("1k", 1024, ParameterError.Ok),
        ("2K", 2048, ParameterError.Ok),
        ("3k", 3072, ParameterError.Ok),
        ("4K", 4096, ParameterError.Ok),
        ("5k", 5120, ParameterError.Ok),
        ("6K", 6144, ParameterError.Ok),
        ("7k", 7168, ParameterError.Ok),
        ("8K", 8192, ParameterError.Ok),
        ("9k", 9216, ParameterError.Ok),
        ("10K", 10240, ParameterError.Ok),
        ("20M", 20971520, ParameterError.Ok),
        ("30G", 32212254720, ParameterError.Ok),
        ("40T", 43980465111040, ParameterError.Ok),
        ("50P", 56294995342131200, ParameterError.Ok),
        ("1.1k", 1126, ParameterError.Ok),
        ("1.01k", 1034, ParameterError.Ok),
        ("1.001k", 1025, ParameterError.Ok),
        ("1.0001k", 1024, ParameterError.Ok),
        ("22.1m", 23173529, ParameterError.Ok),
        ("22.01m", 23079157, ParameterError.Ok),
        ("22.001m", 23069720, ParameterError.Ok),
        ("22.0001m", 23068776, ParameterError.Ok),
        ("22.00001m", 23068682, ParameterError.Ok),
        ("22.000001m", 23068673, ParameterError.Ok),
        ("22.0000001m", 23068672, ParameterError.Ok),
        ("22.000000001m", 23068672, ParameterError.Ok),
        ("3.4", 0, ParameterError.BadUse),
        ("3.14b", 0, ParameterError.BadUse),
        ("5000.9P", 5630512844129278361, ParameterError.Ok),
        ("5000.99P", 5630614175120894197, ParameterError.Ok),
        ("5000.999P", 5630624308220055781, ParameterError.Ok),
        ("5000.9999P", 5630625321529969316, ParameterError.Ok),
        ("8191P", 9222246136947933184, ParameterError.Ok),
        ("8191.9999999P", 9223372036735343194, ParameterError.Ok),
        ("8192P", 0, ParameterError.NumberTooLarge),
        ("9223372036854775807", 9223372036854775807, ParameterError.Ok),
        ("9223372036854775808", 0, ParameterError.NumberTooLarge),
        ("a", 0, ParameterError.BadNumeric),
        ("-2", 0, ParameterError.BadNumeric),
        ("+2", 0, ParameterError.BadNumeric),
        ("2,2k", 0, ParameterError.BadUse),
    ];

    /// <summary>The <c>ParameterError</c> codes the C table expects.</summary>
    public enum ParameterError
    {
        /// <summary><c>PARAM_OK</c>: the size was read.</summary>
        Ok,

        /// <summary><c>PARAM_BAD_USE</c>: <see cref="CommandLineRefusal.BadlyUsedHere(string)"/>.</summary>
        BadUse,

        /// <summary><c>PARAM_NUMBER_TOO_LARGE</c>: <see cref="CommandLineRefusal.TooLargeNumber(string)"/>.</summary>
        NumberTooLarge,

        /// <summary><c>PARAM_BAD_NUMERIC</c>: <see cref="CommandLineRefusal.ExpectedProperNumericalParameter(string)"/>.</summary>
        BadNumeric,
    }

    /// <inheritdoc/>
    public string ProgramName => "tool1623";

    /// <summary>Reads which <c>ParameterError</c> a <see cref="CommandLineNumber.ParseSize(string, string, out long)"/> answer is.</summary>
    /// <param name="refusal">The parser's refusal, or <see langword="null"/> when it read the size.</param>
    /// <returns>The code; any refusal other than a too large or bad number is <see cref="ParameterError.BadUse"/>.</returns>
    public static ParameterError ErrorOf(CommandLineRefusal? refusal)
    {
        if (refusal is null)
        {
            return ParameterError.Ok;
        }

        if (refusal.StandardErrorLines.SequenceEqual(CommandLineRefusal.TooLargeNumber(Option).StandardErrorLines))
        {
            return ParameterError.NumberTooLarge;
        }

        return refusal.StandardErrorLines.SequenceEqual(CommandLineRefusal.ExpectedProperNumericalParameter(Option).StandardErrorLines)
            ? ParameterError.BadNumeric
            : ParameterError.BadUse;
    }

    /// <inheritdoc/>
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string input, long amount, ParameterError expected) in Rows)
        {
            ParameterError error = ErrorOf(CommandLineNumber.ParseSize(Option, input, out long output));
            checks.FailUnless(error == expected, $"'{input}' unexpectedly returned {error}");
            checks.FailUnless(output == amount, string.Create(CultureInfo.InvariantCulture, $"'{input}' unexpectedly gave {output}"));
        }

        return Task.FromResult(checks.Result);
    }
}
