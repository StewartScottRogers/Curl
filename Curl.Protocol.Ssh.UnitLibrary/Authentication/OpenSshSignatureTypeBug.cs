namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// libssh2 1.11.1's <c>SSH_BUG_SIGTYPE</c> test (<c>src/userauth.c</c>,
/// <c>is_version_less_than_78</c>): a server whose banner names OpenSSH 7.7 or older does
/// not accept an RSA certificate signed by <c>rsa-sha2-256</c> or <c>rsa-sha2-512</c>, so
/// libssh2 leaves an <c>ssh-rsa-cert-v01@openssh.com</c> key's method as it is.
/// </summary>
internal static class OpenSshSignatureTypeBug
{
    private const string VersionPrefix = "OpenSSH_";

    /// <summary>
    /// Gets whether <paramref name="serverIdentification" /> names OpenSSH before 7.8, read as
    /// libssh2 reads it: the text after the first <c>OpenSSH_</c> is parsed by
    /// <c>strtol</c> (leading white space and a sign allowed) up to a required dot, and the
    /// version is older when the major number is 1 to 6, or 7 with a minor digit of 0 to 7.
    /// </summary>
    /// <param name="serverIdentification">The server's identification string, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the server has the bug.</returns>
    internal static bool AffectsServer(string? serverIdentification)
    {
        int start = serverIdentification?.IndexOf(VersionPrefix, StringComparison.Ordinal) ?? -1;
        return start >= 0 && IsVersionBefore78(serverIdentification!.AsSpan(start + VersionPrefix.Length));
    }

    private static bool IsVersionBefore78(ReadOnlySpan<char> version)
    {
        (long major, int end) = ParseStrtol(version);
        return end < version.Length && version[end] == '.' && IsBefore78(major, CharacterAt(version, end + 1));
    }

    private static bool IsBefore78(long major, char minor) => major is >= 1 and <= 6 || (major == 7 && minor is >= '0' and <= '7');

    // Past the banner's end is C's terminating zero, no digit.
    private static char CharacterAt(ReadOnlySpan<char> text, int index) => index < text.Length ? text[index] : '\0';

    // strtol in base 10: white space, an optional sign, then digits; the value saturates, and
    // with no digits the end is the start, as strtol's end pointer is.
    private static (long Value, int End) ParseStrtol(ReadOnlySpan<char> text)
    {
        int index = text.Length - text.TrimStart(" \t\n\v\f\r").Length;
        char sign = CharacterAt(text, index);
        index += sign is '+' or '-' ? 1 : 0;
        ReadOnlySpan<char> digits = LeadingDigits(text[index..]);
        long value = ValueOf(digits);
        return digits.IsEmpty ? (0, 0) : (sign == '-' ? -value : value, index + digits.Length);
    }

    private static ReadOnlySpan<char> LeadingDigits(ReadOnlySpan<char> text)
    {
        int end = text.IndexOfAnyExceptInRange('0', '9');
        return end < 0 ? text : text[..end];
    }

    private static long ValueOf(ReadOnlySpan<char> digits)
    {
        long value = 0;
        foreach (char digit in digits)
        {
            value = Math.Min((value * 10) + (digit - '0'), int.MaxValue);
        }

        return value;
    }
}
