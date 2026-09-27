// Ported from curl 8.21.0 lib/vtls/x509asn1.c, Copyright (C) Daniel Stenberg,
// <daniel@haxx.se>, et al., under the curl licence (SPDX-License-Identifier: curl).

namespace Curl.Output;

/// <summary>
/// Reads one ASN.1 element at a time, as curl 8.21.0's <c>getASN1Element</c> in
/// <c>lib/vtls/x509asn1.c</c> does: lenient, checking only that the element fits, so
/// <see cref="PeerCertificateText" /> accepts and refuses the certificates curl does
/// (ADR-0047).
/// </summary>
/// <remarks>
/// It refuses an element that starts with a zero byte, uses a long-form tag, is longer
/// than 256 KiB of source, has a length of more than four bytes' worth, or does not fit
/// in what is left. An indefinite length is allowed only on a constructed element, whose
/// content then runs to the next zero byte after its children, nested at most 16 deep.
/// </remarks>
internal static class DerReader
{
    private const int MaximumSourceLength = 0x40000;

    private const int MaximumDepth = 16;

    private const int LongTag = 0x1F;

    /// <summary>Reads the element at <paramref name="start" />, or fails.</summary>
    /// <param name="der">The buffer.</param>
    /// <param name="start">The offset of the element's identifier byte.</param>
    /// <param name="end">The offset the element must end by.</param>
    /// <returns>The element.</returns>
    /// <exception cref="FormatException">No element curl would accept starts there.</exception>
    internal static DerElement Read(byte[] der, int start, int end) =>
        TryRead(der, start, end, out var element)
            ? element
            : throw new FormatException("The certificate holds an ASN.1 element curl cannot read.");

    /// <summary>Reads the element at <paramref name="start" /> if curl would accept it.</summary>
    /// <param name="der">The buffer.</param>
    /// <param name="start">The offset of the element's identifier byte.</param>
    /// <param name="end">The offset the element must end by.</param>
    /// <param name="element">The element, when there is one.</param>
    /// <returns><see langword="true" /> when an element was read.</returns>
    internal static bool TryRead(byte[] der, int start, int end, out DerElement element) =>
        TryRead(der, start, end, 0, out element);

    private static bool TryRead(byte[] der, int start, int end, int depth, out DerElement element)
    {
        element = default;
        if (!HasReadableIdentifier(der, start, end, depth))
        {
            return false;
        }

        var tag = der[start] & 0x1F;
        var isConstructed = (der[start] & 0x20) != 0;
        var position = start + 1;
        int lengthByte = der[position++];
        if (lengthByte == 0x80)
        {
            return isConstructed && TryReadIndefinite(der, position, end, depth, tag, out element);
        }

        if (!TryReadLength(der, ref position, end, lengthByte, out var length))
        {
            return false;
        }

        var contentEnd = position + (int)length;
        element = new DerElement(tag, isConstructed, position, contentEnd, contentEnd);
        return true;
    }

    // An identifier byte that is not zero and not a long-form tag, with a length byte after
    // it, in a source no longer than curl reads, no deeper than curl nests.
    private static bool HasReadableIdentifier(byte[] der, int start, int end, int depth)
    {
        if (start + 1 >= end || end - start > MaximumSourceLength || depth >= MaximumDepth)
        {
            return false;
        }

        return der[start] != 0 && (der[start] & 0x1F) != LongTag;
    }

    // The content runs from the children's start to the first zero byte where a child would
    // begin; like curl, only that one byte is skipped.
    private static bool TryReadIndefinite(byte[] der, int start, int end, int depth, int tag, out DerElement element)
    {
        element = default;
        var position = start;
        while (position < end && der[position] != 0)
        {
            if (!TryRead(der, position, end, depth + 1, out var child))
            {
                return false;
            }

            position = child.Next;
        }

        if (position >= end)
        {
            return false;
        }

        element = new DerElement(tag, true, start, position, position + 1);
        return true;
    }

    private static bool TryReadLength(byte[] der, ref int position, int end, int lengthByte, out long length)
    {
        length = lengthByte;
        if (lengthByte >= 0x80)
        {
            var count = lengthByte & 0x7F;
            if (count > end - position)
            {
                return false;
            }

            length = 0;
            for (; count > 0; count--)
            {
                if ((length & 0xFF000000L) != 0)
                {
                    return false;
                }

                length = (length << 8) | der[position++];
            }
        }

        return length <= end - position;
    }
}
