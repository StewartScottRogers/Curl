namespace Curl.Http3;

/// <summary>
/// The values RFC 9114 reserves for greasing (sections 6.2.3, 7.2.4.1 and 7.2.8): frame
/// types, setting identifiers and stream types of the form <c>0x1f * N + 0x21</c>. A
/// receiver ignores them.
/// </summary>
public static class Http3ReservedIdentifier
{
    private const long First = 0x21;

    private const long Spacing = 0x1f;

    /// <summary>
    /// Gets whether <paramref name="value" /> is reserved for greasing.
    /// </summary>
    /// <param name="value">A frame type, setting identifier or stream type.</param>
    /// <returns><see langword="true" /> when it has the form <c>0x1f * N + 0x21</c>.</returns>
    public static bool IsReserved(long value) => value >= First && (value - First) % Spacing == 0;
}
