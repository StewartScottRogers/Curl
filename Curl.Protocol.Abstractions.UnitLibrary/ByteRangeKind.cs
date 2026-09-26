namespace Curl.Protocol.Abstractions;

/// <summary>
/// Which of curl's three <c>-r</c>/<c>--range</c> forms a <see cref="ByteRange" />
/// expresses.
/// </summary>
/// <remarks>
/// The three forms are not interchangeable, and the difference is not cosmetic: only
/// <see cref="Bounded" /> knows both ends, <see cref="FromOffset" /> runs to whatever
/// end the resource turns out to have, and <see cref="Suffix" /> cannot be turned into a
/// start offset at all until the length is known. Carrying the form explicitly keeps a
/// handler from guessing which of the three a pair of nullable numbers meant.
/// </remarks>
public enum ByteRangeKind
{
    /// <summary>
    /// <c>0-4</c>: an explicit first and last byte position, both inclusive.
    /// </summary>
    Bounded = 0,

    /// <summary>
    /// <c>6-</c>: an explicit first byte position, running to the end of the resource.
    /// </summary>
    FromOffset,

    /// <summary>
    /// <c>-5</c>: the last <em>n</em> bytes of the resource, with no start known in
    /// advance.
    /// </summary>
    Suffix,
}
