namespace Curl.Conformance.LibTest;

/// <summary>
/// One <c>curl_msnprintf</c> check of <c>lib557.c</c>: the format and its arguments formatted into a buffer of
/// <see cref="Size"/> bytes, and what the answer must be.
/// </summary>
/// <param name="Format">The format.</param>
/// <param name="Arguments">The arguments it reads.</param>
/// <param name="Size">The buffer's length, <c>curl_msnprintf</c>'s <c>maxlength</c>.</param>
/// <param name="Expected">
/// The text the buffer must hold; with a <paramref name="Suffix"/>, only how it must start; ignored with a
/// <paramref name="Count"/>.
/// </param>
/// <param name="Suffix">How the text must end, where only its two ends are checked (<c>lib557.c</c>'s long double checks).</param>
/// <param name="Count">The return value it must give, where only that is checked (its <c>strlen_check</c> and return code checks).</param>
public sealed record Lib557Check(string Format, object?[] Arguments, int Size, string Expected, string? Suffix = null, int? Count = null)
{
    /// <summary>Whether the answer <c>curl_msnprintf</c> gave is the one expected.</summary>
    /// <param name="count">What it returned.</param>
    /// <param name="text">The text the buffer holds, up to its NUL.</param>
    /// <returns><see langword="true"/> when the check passes.</returns>
    public bool Matches(int count, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Count is int expectedCount)
        {
            return count == expectedCount;
        }

        return Suffix is null
            ? text == Expected
            : text.StartsWith(Expected, StringComparison.Ordinal) && text.EndsWith(Suffix, StringComparison.Ordinal);
    }

    /// <summary>What <see cref="Matches"/> wants, as the failure line shows it.</summary>
    public string Described => Count is int expectedCount
        ? expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Expected + (Suffix is null ? string.Empty : "..." + Suffix);
}
