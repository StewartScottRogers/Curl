namespace Curl.Output;

/// <summary>How <see cref="TraceTransferEventWriter"/> shows the bytes of a dump.</summary>
public enum TraceDumpFormat
{
    /// <summary>
    /// <c>--trace</c>: 16 bytes a line, each as two hex digits, then the same bytes as text.
    /// </summary>
    HexAndText,

    /// <summary>
    /// <c>--trace-ascii</c>: up to 64 bytes a line as text only, a new line after each CR LF.
    /// </summary>
    TextOnly,
}
