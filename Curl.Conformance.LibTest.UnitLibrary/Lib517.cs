using Libcurl.Misc.DatesAndLists;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib517.c</c>: <c>curl_getdate</c> (<see cref="CurlDate.Parse"/>) over a table of date strings, printing
/// <c>WRONGLY &lt;input&gt; =&gt; &lt;got&gt; (instead of &lt;expected&gt;)</c> for each one that parses to another
/// time, and answering <see cref="LibTestErrorCodes.Failure"/> when any did. A 64-bit <c>time_t</c> build,
/// so the table holds its two dates past 2038.
/// </summary>
public sealed class Lib517 : ILibTest
{
    /// <summary>The dates and the times <c>curl_getdate</c> must give them, -1 for a date it refuses.</summary>
    public static IReadOnlyList<(string Input, long Output)> Dates { get; } =
    [
        ("Sun, 06 Nov 1994 08:49:37 GMT", 784111777),
        ("Sunday, 06-Nov-94 08:49:37 GMT", 784111777),
        ("Sun Nov  6 08:49:37 1994", 784111777),
        ("Sun Nov  6 8:49:37 1994", 784111777),
        ("Sun Nov  6 8:9:37 1994", 784109377),
        ("Sun Nov  6 008:09:37 1994", -1),
        ("Nov      Sun      6 8:9:7 1994", 784109347),
        ("06 Nov 1994 08:49:37 GMT", 784111777),
        ("06-Nov-94 08:49:37 GMT", 784111777),
        ("Nov  6 08:49:37 1994", 784111777),
        ("06 Nov 1994 08:49:37", 784111777),
        ("06-Nov-94 08:49:37", 784111777),
        ("1994 Nov 6 08:49:37", 784111777),
        ("GMT 08:49:37 06-Nov-94 Sunday", 784111777),
        ("94 6 Nov 08:49:37", 784111777),
        ("1994 Nov 6", 784080000),
        ("06-Nov-94", 784080000),
        ("Sun Nov 6 94", 784080000),
        ("1994.Nov.6", 784080000),
        ("Sun/Nov/6/94/GMT", 784080000),
        ("Sun, 06 Nov 1994 08:49:37 CET", 784108177),
        ("Sun, 06 Nov 1994 08:49:37 cet", -1),
        ("Sun, 06 Nov 1994 08:49:37 utc", -1),
        ("Sun, 06 Nov 1994 08:49:37 gmt", -1),
        ("06 Nov 1994 08:49:37 EST", 784129777),
        ("Sun, 06 Nov 1994 08:49:37 UT", 784111777),
        ("Sun, 12 Sep 2004 15:05:58 -0700", 1095026758),
        ("Sat, 11 Sep 2004 21:32:11 +0200", 1094931131),
        ("20040912 15:05:58 -0700", 1095026758),
        ("20040911 +0200", 1094853600),
        ("Sun, 06 Nov 1994 08:49:37 +0530", 784091977),
        ("Sun, 06 Nov 1994 08:49:37 +0545", 784091077),
        ("06 Nov 1994 08:49:37 Z", 784111777),
        ("06 Nov 1994 08:49:37 A", 784108177),
        ("06 Nov 1994 08:49:37 B", 784104577),
        ("06 Nov 1994 08:49:37 C", 784100977),
        ("06 Nov 1994 08:49:37 D", 784097377),
        ("06 Nov 1994 08:49:37 E", 784093777),
        ("06 Nov 1994 08:49:37 F", 784090177),
        ("06 Nov 1994 08:49:37 G", 784086577),
        ("06 Nov 1994 08:49:37 H", 784082977),
        ("06 Nov 1994 08:49:37 I", 784079377),
        ("06 Nov 1994 08:49:37 J", -1),
        ("06 Nov 1994 08:49:37 K", 784075777),
        ("06 Nov 1994 08:49:37 L", 784072177),
        ("06 Nov 1994 08:49:37 M", 784068577),
        ("06 Nov 1994 08:49:37 N", 784115377),
        ("06 Nov 1994 08:49:37 O", 784118977),
        ("06 Nov 1994 08:49:37 P", 784122577),
        ("06 Nov 1994 08:49:37 Q", 784126177),
        ("06 Nov 1994 08:49:37 R", 784129777),
        ("06 Nov 1994 08:49:37 S", 784133377),
        ("06 Nov 1994 08:49:37 T", 784136977),
        ("06 Nov 1994 08:49:37 U", 784140577),
        ("06 Nov 1994 08:49:37 V", 784144177),
        ("06 Nov 1994 08:49:37 W", 784147777),
        ("06 Nov 1994 08:49:37 X", 784151377),
        ("06 Nov 1994 08:49:37 Y", 784154977),
        ("GMT+05:30", -1),
        ("GMT-08:00", -1),
        ("1994-11-06T08:49:37Z", -1),
        ("1994-11-06 08:49:37.123 GMT", -1),
        ("19941106T084937Z", -1),
        ("19 Jan 2038 03:14:07 GMT", 2147483647),
        ("19 Jan 2038 03:14:08 GMT", 2147483648),
        ("01 Jan 69 00:00:00 GMT", 3124224000),
        ("01 Jan 1500 00:00:00 GMT", -1),
        ("Sun, 06-Nov/1994 08:49:37", 784111777),
        ("Sun,    06 Nov   1994   08:49:37 GMT", 784111777),
        ("  Sun, 06 Nov 1994 08:49:37 GMT  ", 784111777),
        ("Date: Sun, 06 Nov 1994 08:49:37 GMT", -1),
        ("Monday, 06 Nov 1994 08:49:37 GMT", 784111777),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib517";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        CheckDatesAsync(context, Dates, cancellationToken);

    /// <summary>The program's loop over <paramref name="dates"/>.</summary>
    /// <param name="context">The program's context, whose stdout takes the <c>WRONGLY</c> lines.</param>
    /// <param name="dates">The dates and the times they must parse to.</param>
    /// <param name="cancellationToken">Stops the writes.</param>
    /// <returns><see cref="LibTestResult.Success"/>, or <see cref="LibTestErrorCodes.Failure"/> when a date parsed to another time.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async Task<LibTestResult> CheckDatesAsync(LibTestContext context, IEnumerable<(string Input, long Output)> dates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dates);
        string[] wrongly =
        [
            .. dates
                .Select(date => (date.Input, date.Output, Got: CurlDate.Parse(date.Input)))
                .Where(date => date.Got != date.Output)
                .Select(date => $"WRONGLY {date.Input} => {date.Got} (instead of {date.Output})\n"),
        ];
        StreamWriter output = LibTestRun.StandardOutputWriter(context);
        await using (output.ConfigureAwait(false))
        {
            await output.WriteAsync(string.Concat(wrongly).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return wrongly.Length == 0 ? LibTestResult.Success : LibTestResult.FromCurlCode(LibTestErrorCodes.Failure);
    }
}
