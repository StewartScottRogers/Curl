using System.Globalization;
using System.Text;
using Libcurl.Misc.Printf;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib557.c</c>: libcurl's own printf (<c>curl_msnprintf</c> and the rest of <c>curl_m*printf</c>) over upstream's
/// tables - the integer types, strings, floats, octal and hexadecimal, positional and weird arguments, return codes
/// and the <c>v</c> functions - every table driven through one checker. Each <c>test_*</c> function that prints
/// one writes its <c>All curl_mprintf() ... tests OK!</c> line on stdout, a failed check its own line; any failure
/// ends the program with <c>TEST_ERR_MAJOR_BAD</c>.
/// </summary>
/// <remarks>
/// The integer tables are upstream's bit patterns, built per width rather than written out, each expected text the
/// value's invariant decimal; <c>long</c> is 32 bits on Windows and 64 elsewhere, as <c>SIZEOF_LONG</c> picks
/// upstream's table, and <c>curl_off_t</c> is formatted with <c>%lld</c>, 64 bits on every platform.
/// </remarks>
public sealed class Lib557 : ILibTest
{
    /// <summary><c>test_vversions</c>' format.</summary>
    public const string VVersionsFormat = "testing %s and %d is %s and %ld\n";

    // lib557's MAXIMIZE: -DBL_MAX.
    private const double Maximize = -1.7976931348623157081452E+308;

    // lib557's BUFSZ.
    private const int BufferSize = 256;

    /// <summary><c>test_vversions</c>' arguments.</summary>
    public static IReadOnlyList<object?> VVersionsArguments { get; } = ["a string", -443, "super fun", 9876543L];

    /// <inheritdoc/>
    public string ProgramName => "lib557";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int errors = Groups(OperatingSystem.IsWindows()).Sum(group => CheckGroup(context.StandardOutput, group));
        errors += CheckVVersions(context.StandardOutput, context.StandardError, BufferSize);
        return Task.FromResult(ResultFor(errors));
    }

    /// <summary><c>test()</c>'s answer: <c>CURLE_OK</c> with no errors, else <c>TEST_ERR_MAJOR_BAD</c>.</summary>
    /// <param name="errors">How many checks failed.</param>
    /// <returns>The program's exit code.</returns>
    public static LibTestResult ResultFor(int errors) =>
        errors == 0 ? LibTestResult.Success : LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);

    /// <summary>Every table, in the order <c>test()</c> runs them.</summary>
    /// <param name="longIsThirtyTwoBits">Whether C's <c>long</c> is 32 bits (Windows) rather than 64.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<Lib557Group> Groups(bool longIsThirtyTwoBits) =>
    [
        Silent("weird arguments", WeirdArgumentChecks()),
        Numbers("unsigned short", "%hu", Unsigned(16)),
        Numbers("signed short", "%hd", Signed(16)),
        Numbers("unsigned int", "%u", Unsigned(32)),
        Numbers("signed int", "%d", Signed(32)),
        Numbers("unsigned long", "%lu", Unsigned(longIsThirtyTwoBits ? 32 : 64)),
        Numbers("signed long", "%ld", Signed(longIsThirtyTwoBits ? 32 : 64)),
        Numbers("curl_off_t", "%lld", Signed(64)),
        Printed("strings", "All curl_mprintf() strings tests OK!\n", "Some curl_mprintf() string tests Failed!\n", StringChecks()),
        Printed("float", "All float strings tests OK!\n", "Some float strings tests Failed!\n", FloatChecks()),
        Printed(
            "octal and hexadecimal",
            "All curl_mprintf() octal and hexadecimal tests OK!\n",
            "Some curl_mprintf() octal and hexadecimal tests Failed!\n",
            [
                Text("%ho %hx %hX %+X %+x", "175020 fa10 FA10 +FA10 +fa10", 0xFA10U, 0xFA10U, 0xFA10U, 0xFA10, 0xFA10),
                Text("%o %x %X %+o", "37257011060 fabc1230 FABC1230 +2322", 0xFABC1230U, 0xFABC1230U, 0xFABC1230U, 1234U),
            ]),
        Silent(
            "positional arguments",
            [
                Text("%3$d %2$d %1$d", "502 501 500", 500, 501, 502),
                Text("%3$d %1$d %2$d", "502 500 501", 500, 501, 502),
                Text("%3$d %d %2$d", string.Empty, 500, 501, 502),
            ]),
        Silent(
            "return codes",
            [
                new("%d", [9999], 100, string.Empty, Count: 4),
                new("%d", [99999], 100, string.Empty, Count: 5),
                new("%d", [99999], 5, string.Empty, Count: 4),
                new("%s", ["helloooooooo"], 5, string.Empty, Count: 4),
                new("%s", ["helloooooooo"], 6, string.Empty, Count: 5),
            ]),
    ];

    /// <summary>
    /// Runs one group's checks, writing each failure's line and then the group's passed or failed line on
    /// <paramref name="standardOutput"/>.
    /// </summary>
    /// <param name="standardOutput">The program's stdout.</param>
    /// <param name="group">The group.</param>
    /// <returns>How many checks failed.</returns>
    public static int CheckGroup(Stream standardOutput, Lib557Group group)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(group);
        int failed = 0;
        for (int i = 0; i < group.Checks.Count; i++)
        {
            failed += Check(standardOutput, group.Name, i + 1, group.Checks[i]);
        }

        string? line = failed == 0 ? group.PassedLine : group.FailedLine;
        if (line is not null)
        {
            CurlPrintf.MFPrintf(standardOutput, "%s", line);
        }

        return failed;
    }

    /// <summary>
    /// <c>test_vversions</c>: one format through <c>curl_mvprintf</c>, <c>curl_mvfprintf</c> (to stderr),
    /// <c>curl_mvsnprintf</c>, <c>curl_mvsprintf</c> and <c>curl_mvaprintf</c>, which must all give the same length.
    /// </summary>
    /// <param name="standardOutput">The program's stdout, which <c>curl_mvprintf</c> writes to.</param>
    /// <param name="standardError">The program's stderr.</param>
    /// <param name="snprintfSize">The buffer <c>curl_mvsnprintf</c> is given.</param>
    /// <returns>1 when a length differs, else 0.</returns>
    public static int CheckVVersions(Stream standardOutput, TextWriter standardError, int snprintfSize)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        object?[] arguments = [.. VVersionsArguments];
        int printed = CurlPrintf.MFPrintf(standardOutput, VVersionsFormat, arguments);
        using var errorBytes = new MemoryStream();
        int[] others =
        [
            CurlPrintf.MFPrintf(errorBytes, VVersionsFormat, arguments),
            CurlPrintf.MSNPrintf(new byte[snprintfSize], VVersionsFormat, arguments),
            CurlPrintf.MSPrintf(new byte[BufferSize], VVersionsFormat, arguments),
            Encoding.UTF8.GetByteCount(CurlPrintf.MAPrintf(VVersionsFormat, arguments)!),
        ];
        standardError.Write(Encoding.UTF8.GetString(errorBytes.ToArray()));
        bool same = Array.TrueForAll(others, count => count == printed);
        CurlPrintf.MFPrintf(standardOutput, same ? "All v-functions test OK!\n" : "Some v-functions tests Failed!\n");
        return same ? 0 : 1;
    }

    // One check; 1 and its failure line when it fails.
    private static int Check(Stream standardOutput, string name, int number, Lib557Check check)
    {
        var buffer = new byte[check.Size];
        int count = CurlPrintf.MSNPrintf(buffer, check.Format, check.Arguments);
        string text = Encoding.UTF8.GetString(buffer, 0, count);
        if (check.Matches(count, text))
        {
            return 0;
        }

        CurlPrintf.MFPrintf(
            standardOutput,
            "%s test #%.2d: Failed (Expected: %s Got: %s (%d))\n",
            name,
            number,
            check.Described,
            text,
            count);
        return 1;
    }

    private static Lib557Group Numbers(string name, string format, IEnumerable<IFormattable> values) => new(
        name,
        $"All curl_mprintf() {name} tests OK!\n",
        $"Some curl_mprintf() {name} tests Failed!\n",
        [.. values.Select(value => Text(format, value.ToString(null, CultureInfo.InvariantCulture), value))]);

    private static Lib557Group Printed(string name, string passed, string failed, IReadOnlyList<Lib557Check> checks) =>
        new(name, passed, failed, checks);

    private static Lib557Group Silent(string name, IReadOnlyList<Lib557Check> checks) => new(name, null, null, checks);

    private static Lib557Check Text(string format, string expected, params object?[] arguments) =>
        new(format, arguments, BufferSize, expected);

    private static Lib557Check Length(int size, string format, int count, params object?[] arguments) =>
        new(format, arguments, size, string.Empty, Count: count);

    // The unsigned table of a width: the maximum, each of the nibbles F, E, C and 1 at every nibble position from
    // the top, and 0.
    private static IEnumerable<IFormattable> Unsigned(int bits)
    {
        ulong maximum = ulong.MaxValue >> (64 - bits);
        yield return Width(maximum, bits);
        foreach (ulong nibble in new ulong[] { 0xF, 0xE, 0xC, 0x1 })
        {
            for (int shift = bits - 4; shift >= 0; shift -= 4)
            {
                yield return Width(nibble << shift, bits);
            }
        }

        yield return Width(0, bits);
    }

    // The signed table of a width: the maximum, each of the nibbles 7, 5 and 1 at every nibble position from the
    // top, 0, and each of those less one negated (-0x7FFF-1, ..., -0x0000-1).
    private static IEnumerable<IFormattable> Signed(int bits)
    {
        var positives = new List<long> { (long)(ulong.MaxValue >> (65 - bits)) };
        foreach (long nibble in new long[] { 0x7, 0x5, 0x1 })
        {
            for (int shift = bits - 4; shift >= 0; shift -= 4)
            {
                positives.Add(nibble << shift);
            }
        }

        positives.Add(0);
        return positives.Concat(positives.Select(value => -value - 1)).Select(value => SignedWidth(value, bits));
    }

    // An unsigned value as the C type of its width, so its argument and its expected text agree.
    private static IFormattable Width(ulong value, int bits) => bits switch
    {
        16 => (ushort)value,
        32 => (uint)value,
        _ => value,
    };

    private static IFormattable SignedWidth(long value, int bits) => bits switch
    {
        16 => (short)value,
        32 => (int)value,
        _ => value,
    };

    // test_string_formatting.
    private static Lib557Check[] StringChecks() =>
    [
        Text("%0*d%s", "09foo", 2, 9, "foo"),
        Text("%*.*s", "   fo", 5, 2, "foo"),
        Text("%*.*s", "foo", 2, 5, "foo"),
        Text("%*.*s", "foo", 0, 10, "foo"),
        Text("%-10s", "foo       ", "foo"),
        Text("%10s", "       foo", "foo"),
        Text("%*.*s", "foo       ", -10, -10, "foo"),
    ];

    // test_weird_arguments, with test_width_precision.
    private static Lib557Check[] WeirdArgumentChecks() =>
    [
        Text("%-20d%% right? %%", "500                 % right? %", 500),
        Text(new string('%', 100), new string('%', 50)),
        Text("%2 AA %d %K", "%2 AA 500 %K", 500, 501, 502),
        Text("%2 %d %K", "%2 500 %K", 500, 501, 502),
        Text(
            string.Concat(Enumerable.Repeat("%d", 128)),
            string.Concat(Enumerable.Range(0, 128).Select(n => n % 10)),
            [.. Enumerable.Range(0, 128).Select(n => (object)(n % 10))]),
        Text(string.Concat(Enumerable.Repeat("%d", 129)), string.Empty, [.. Enumerable.Range(0, 129).Select(n => (object)(n % 10))]),
        new("%325.325f", [0.1], 1024, new string(' ', 324) + "0"),
        Length(1024, "%326.326f", 325, 0.1),
        Length(1024, "%1000.1000f", 325, 0.1),
        Length(1024, "%324.324f", 324, 0.1),
        Length(1024, "%324.0f", 324, 0.1),
        Length(1024, "%0.324f", 325, 0.1),
    ];

    // test_float_formatting.
    private static Lib557Check[] FloatChecks() =>
    [
        Text("%f", "9.000000", 9.0),
        Text("%F", "9.000000", 9.0),
        Text("%.1f", "9.1", 9.1),
        Text("%.2f", "9.10", 9.1),
        Text("%.0f", "9", 9.1),
        Text("%0f", "9.100000", 9.1),
        Text("%10f", "  9.100000", 9.1),
        Text("%10.3f", "     9.100", 9.1),
        Text("%-10.3f", "9.100     ", 9.1),
        Text("%-10.3f", "9.123     ", 9.123456),
        Text("%.-2f", "9.100000", 9.1),
        Text("%*f", "  9.100000", 10, 9.1),
        Text("%*f", "9.100000", 3, 9.1),
        Text("%*f", "9.298765", 6, 9.2987654),
        Text("%*f", "9.298765", 6, 9.298765),
        Text("%*f", "9.298760", 6, 9.29876),
        Text("%.*f", "9.298765", 6, 9.2987654),
        Text("%.*f", "9.29877", 5, 9.2987654),
        Text("%.*f", "9.2988", 4, 9.2987654),
        Text("%.*f", "9.299", 3, 9.2987654),
        Text("%.*f", "9.30", 2, 9.2987654),
        Text("%.*f", "9.3", 1, 9.2987654),
        Text("%.*f", "9", 0, 9.2987654),
        Length(512, "%.*f", 325, 1 << 30, 9.2987654),
        Length(512, "%10000.10000f", 325, 9.2987654),
        Length(512, "%240.10000f", 325, 123456789123456789123456789.2987654),
        new("%*f", [int.MinValue, 9.1], 512, "9.100000" + new string(' ', 317)),
        new("%*f", [1 << 30, 9.1], 512, new string(' ', 317) + "9.100000"),
        new("%100000f", [9.1], 512, new string(' ', 317) + "9.100000"),
        Length(512, "%f", 317, Maximize),
        Length(2, "%f", 1, Maximize),
        Length(3, "%f", 2, Maximize),
        Length(4, "%f", 3, Maximize),
        Length(5, "%f", 4, Maximize),
        Length(6, "%f", 5, Maximize),
        new("%.17Lf", [1.234567890123456789], 128, "1.23456789012345", string.Empty),
        new("%.17Le", [1.234567890123456789], 128, "1.234567890123456", "e+00"),
        new("%.17LE", [1.234567890123456789], 128, "1.234567890123456", "E+00"),
        new("%.17Lg", [1.234567890123456789], 128, "1.234567890123456", string.Empty),
        new("%.17LG", [1.234567890123456789], 128, "1.234567890123456", string.Empty),
    ];
}
