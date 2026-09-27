namespace Curl.Output;

/// <summary>
/// One ASN.1 element found by <see cref="DerReader" />: its tag number, whether it is
/// constructed, where its content lies in the buffer, and where the element after it begins.
/// </summary>
/// <param name="tag">The tag number, the low five bits of the identifier byte, whatever its class.</param>
/// <param name="isConstructed"><see langword="true" /> when the content is itself elements.</param>
/// <param name="start">The offset of the first content byte.</param>
/// <param name="end">The offset just past the last content byte.</param>
/// <param name="next">The offset just past the whole element, where the next one begins.</param>
internal readonly struct DerElement(int tag, bool isConstructed, int start, int end, int next)
{
    /// <summary>Gets the tag number, the low five bits of the identifier byte, whatever its class.</summary>
    internal int Tag { get; } = tag;

    /// <summary>Gets a value indicating whether the content is itself elements.</summary>
    internal bool IsConstructed { get; } = isConstructed;

    /// <summary>Gets the offset of the first content byte.</summary>
    internal int Start { get; } = start;

    /// <summary>Gets the offset just past the last content byte.</summary>
    internal int End { get; } = end;

    /// <summary>Gets the offset just past the whole element, where the next one begins.</summary>
    internal int Next { get; } = next;

    /// <summary>Gets the number of content bytes.</summary>
    internal int Length => End - Start;

    /// <summary>The same element with its content starting later, as curl strips an RSA modulus's leading zeros.</summary>
    /// <param name="contentStart">The new offset of the first content byte.</param>
    /// <returns>The element.</returns>
    internal DerElement StartingAt(int contentStart) => new(Tag, IsConstructed, contentStart, End, Next);
}
