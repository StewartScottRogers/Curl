using System.Formats.Asn1;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Compares a SmtpUTF8Mailbox <c>otherName</c> with an rfc822Name constraint as OpenSSL's
/// <c>nc_email_eai</c> does (measured with curl 8.18.0 and OpenSSL 3.5.5, ADR-1442).
/// </summary>
internal static class OpenSslSmtpUtf8Mailbox
{
    /// <summary>The <c>otherName</c> type-id of a SmtpUTF8Mailbox (RFC 8398).</summary>
    internal const string TypeId = "1.3.6.1.5.5.7.8.9";

    // OpenSSL decodes the constraint into 256 bytes, its closing NUL included.
    private const int LongestConstraint = 255;

    private const string ALabelPrefix = "xn--";

    // Held in a field, so passing it to Select adds no branch.
    private static readonly Func<byte, byte> ToAsciiLower = value => char.IsAsciiLetterUpper((char)value) ? (byte)(value | 0x20) : value;

    /// <summary>
    /// Compares a mailbox with a constraint. A constraint starting with a dot is compared, one more dot
    /// before it, with the end of the whole mailbox; any other with what follows the mailbox's last
    /// <c>@</c>, so a constraint naming a mailbox matches none. <c>xn--</c> labels of the constraint are
    /// decoded first, and ASCII letters match in either case.
    /// </summary>
    /// <param name="constraint">The rfc822Name constraint's bytes.</param>
    /// <param name="value">The <c>otherName</c>'s encoded value: its tag, length and content.</param>
    /// <returns>
    /// 0 within the constraint, <see cref="CertificateNameConstraints.NotPermitted" /> outside it,
    /// <see cref="CertificateNameConstraints.UnsupportedNameSyntax" /> for a constraint holding a NUL or a
    /// value that is not a UTF8String holding an <c>@</c>, or
    /// <see cref="CertificateNameConstraints.UnspecifiedError" /> for a constraint that does not decode or is too long.
    /// </returns>
    internal static int Compare(byte[] constraint, byte[] value)
    {
        var mailbox = ReadMailbox(value);
        if (mailbox is null || constraint.Contains((byte)0))
        {
            return CertificateNameConstraints.UnsupportedNameSyntax;
        }

        return CompareWithDecoded(constraint, mailbox);
    }

    private static int CompareWithDecoded(byte[] constraint, byte[] mailbox)
    {
        var matchesTheEnd = constraint.AsSpan().StartsWith("."u8);
        var expected = DecodeConstraint(constraint, matchesTheEnd ? "." : string.Empty);
        if (expected is null)
        {
            return CertificateNameConstraints.UnspecifiedError;
        }

        var within = matchesTheEnd ? EndsWithAndIsLonger(mailbox, expected) : EqualsIgnoringAsciiCase(DomainOf(mailbox), expected);
        return within ? 0 : CertificateNameConstraints.NotPermitted;
    }

    // The mailbox of a value that is a UTF8String holding an '@', else null.
    private static byte[]? ReadMailbox(byte[] value)
    {
        var tag = AsnDecoder.ReadEncodedValue(value, AsnEncodingRules.BER, out var offset, out var length, out _);
        var mailbox = value[offset..(offset + length)];
        return tag.HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.UTF8String)) && mailbox.Contains((byte)'@') ? mailbox : null;
    }

    private static byte[] DomainOf(byte[] mailbox) => mailbox[(Array.LastIndexOf(mailbox, (byte)'@') + 1)..];

    private static bool EndsWithAndIsLonger(byte[] mailbox, byte[] end) =>
        mailbox.Length > end.Length && EqualsIgnoringAsciiCase(mailbox[^end.Length..], end);

    // OpenSSL's ossl_a2ulabel: each label starting "xn--" is Punycode-decoded to UTF-8, every other kept as it is.
    private static byte[]? DecodeConstraint(byte[] constraint, string prefix)
    {
        var labels = Encoding.Latin1.GetString(constraint).Split('.').Select(DecodeLabel).ToList();
        if (labels.Contains(null))
        {
            return null;
        }

        byte[] decoded = [.. Encoding.ASCII.GetBytes(prefix), .. labels.SelectMany(label => (byte[])[(byte)'.', .. label!]).Skip(1)];
        return decoded.Length > LongestConstraint ? null : decoded;
    }

    private static byte[]? DecodeLabel(string label)
    {
        if (!label.StartsWith(ALabelPrefix, StringComparison.Ordinal))
        {
            return Encoding.Latin1.GetBytes(label);
        }

        var decoded = PunycodeLabel.Decode(label[ALabelPrefix.Length..]);
        return decoded is null ? null : Encoding.UTF8.GetBytes(decoded);
    }

    private static bool EqualsIgnoringAsciiCase(byte[] left, byte[] right) =>
        left.Select(ToAsciiLower).SequenceEqual(right.Select(ToAsciiLower));
}
