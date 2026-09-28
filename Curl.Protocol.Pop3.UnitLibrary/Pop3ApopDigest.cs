using System.Security.Cryptography;
using System.Text;

namespace Curl.Protocol.Pop3;

/// <summary>
/// The digest an <c>APOP</c> command carries (RFC 1939 section 7): the MD5 of the greeting's
/// timestamp followed by the password, as 32 lower-case hexadecimal digits.
/// </summary>
internal static class Pop3ApopDigest
{
    /// <summary>
    /// Computes the digest of <paramref name="timestamp" /> and <paramref name="password" />,
    /// both taken as Latin-1, the encoding every POP3 line is sent in.
    /// </summary>
    /// <param name="timestamp">The timestamp with its angle brackets (<see cref="Pop3ApopTimestamp" />).</param>
    /// <param name="password">The password.</param>
    /// <returns>The digest, such as <c>d727ab40e6dcedbb6cf2f6735fe51cc8</c>.</returns>
    public static string Compute(string timestamp, string password) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.Latin1.GetBytes(timestamp + password)));
}
