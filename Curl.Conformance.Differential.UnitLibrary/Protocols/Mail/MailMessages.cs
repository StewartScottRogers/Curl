using System.Globalization;

namespace Curl.Conformance.Differential.Protocols.Mail;

/// <summary>
/// The two messages every mock POP3 and IMAP mailbox holds (proto_servers.py's <c>MSGS</c>), plus the
/// number parsing and indexing the Python handlers used, so a bad number ends the connection as the crashed handler did.
/// </summary>
public static class MailMessages
{
    /// <summary>The mailbox's messages, in order.</summary>
    public static IReadOnlyList<byte[]> All { get; } =
    [
        "From: a@b\r\nSubject: one\r\n\r\nbody one\r\n"u8.ToArray(),
        "From: c@d\r\nSubject: two\r\n\r\nbody two\r\n"u8.ToArray(),
    ];

    /// <summary>Parses a decimal number the way Python's <c>int()</c> does for an optionally signed run of digits.</summary>
    public static bool TryParseNumber(string text, out long number) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);

    /// <summary>
    /// Finds the message at a zero-based <paramref name="index"/> the way a Python list does, where a negative
    /// index counts from the end; <see langword="false"/> when the list would raise <c>IndexError</c>.
    /// </summary>
    public static bool TryGetMessage(long index, out byte[] message)
    {
        var count = All.Count;
        var position = index < 0 ? index + count : index;
        var found = position >= 0 && position < count;
        message = found ? All[(int)position] : [];
        return found;
    }
}
