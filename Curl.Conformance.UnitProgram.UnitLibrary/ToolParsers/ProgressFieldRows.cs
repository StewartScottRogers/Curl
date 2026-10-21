using System.Globalization;

namespace Curl.Conformance.UnitProgram.ToolParsers;

/// <summary>
/// What tool1622 and unit1636 share: the values they format (63 rows of <c>secs = secs * 2 + 1</c> from
/// 0, and five byte counts) and the <c>%20 - %s</c> line they write for each.
/// </summary>
public static class ProgressFieldRows
{
    /// <summary>The byte counts both programs format after the 63 doubling rows.</summary>
    public static IReadOnlyList<long> ByteCounts { get; } = [131072, 12645826, 1073741824, 12938588979, 1099445657078333];

    /// <summary>The 63 values the programs' loops format: 0, 1, 3, 7, ... 2^62 - 1.</summary>
    public static IEnumerable<long> Doubling()
    {
        long value = 0;
        for (int row = 0; row < 63; row++)
        {
            yield return value;
            value = (value * 2) + 1;
        }
    }

    /// <summary>The line the programs write: the value right-aligned in 20 columns, <c> - </c>, then its text.</summary>
    /// <param name="value">The value formatted.</param>
    /// <param name="text">Its text.</param>
    /// <returns>The line, without its line feed.</returns>
    public static string Line(long value, string text) => string.Create(CultureInfo.InvariantCulture, $"{value,20} - {text}");
}
