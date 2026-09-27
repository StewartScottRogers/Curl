using System.Globalization;

namespace Curl.Output;

/// <summary>
/// One <c>-w</c> variable's value in both the forms curl 8.21.0 prints it: as
/// <c>%{name}</c> text, and as its member value in <c>%{json}</c>.
/// </summary>
/// <param name="Text">What <c>%{name}</c> prints.</param>
/// <param name="Json">What <c>%{json}</c> prints after the member name.</param>
internal readonly record struct WriteOutValue(string Text, string Json)
{
    /// <summary>
    /// A text value: quoted in JSON, and <c>null</c> there when curl has no value, which
    /// <c>%{name}</c> prints as nothing.
    /// </summary>
    /// <param name="text">The text, or <see langword="null"/> when curl has none.</param>
    /// <returns>The value.</returns>
    public static WriteOutValue FromText(string? text)
    {
        return text is null ? new(string.Empty, "null") : new(text, WriteOutJson.Quote(text));
    }

    /// <summary>A whole number, printed the same in both forms.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    public static WriteOutValue FromNumber(long number)
    {
        string text = number.ToString(CultureInfo.InvariantCulture);
        return new(text, text);
    }

    /// <summary>
    /// A status code: three digits as <c>%{name}</c> text, so <c>000</c> when none arrived,
    /// and a plain number in JSON, so <c>0</c>.
    /// </summary>
    /// <param name="code">The status code, <c>0</c> when none arrived.</param>
    /// <returns>The value.</returns>
    public static WriteOutValue FromStatusCode(int code)
    {
        return new(code.ToString("D3", CultureInfo.InvariantCulture), FromNumber(code).Json);
    }

    /// <summary>
    /// A number curl prints unquoted in JSON that is already formatted, such as a
    /// <c>time_*</c> value's seconds with six decimals.
    /// </summary>
    /// <param name="formatted">The formatted number.</param>
    /// <returns>The value.</returns>
    public static WriteOutValue FromFormattedNumber(string formatted)
    {
        return new(formatted, formatted);
    }
}
