using System.Globalization;
using System.Text;
using Curl.Extension.DnsQuery.Wire;

namespace Curl.Extension.DnsQuery.Rendering;

/// <summary>
/// Writes a decoded message the way dig does: a header comment, the flags and counts, then each non-empty section
/// with one line per entry in master-file presentation format (RFC 1035 section 5.1), fields separated by tabs.
/// </summary>
internal static class DnsTextRenderer
{
    /// <summary>Writes a message as text, each line ending in a line feed.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The text.</returns>
    public static string Render(DnsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        StringBuilder text = new();
        DnsHeader header = message.Header;
        text.Append(CultureInfo.InvariantCulture, $";; ->>HEADER<<- opcode: {DnsMnemonics.OpcodeMnemonic(header.Opcode)}, ")
            .Append(CultureInfo.InvariantCulture, $"status: {DnsMnemonics.ResponseCodeMnemonic(header.ResponseCode)}, id: {header.Id}\n")
            .Append(CultureInfo.InvariantCulture, $";; flags:{string.Concat(header.SetFlagNames().Select(name => " " + name))}; QUERY: {message.Questions.Count}, ANSWER: {message.Answers.Count}, ")
            .Append(CultureInfo.InvariantCulture, $"AUTHORITY: {message.Authority.Count}, ADDITIONAL: {message.Additional.Count}\n");
        if (message.Questions.Count > 0)
        {
            text.Append("\n;; QUESTION SECTION:\n");
            foreach (DnsQuestion question in message.Questions)
            {
                text.Append(CultureInfo.InvariantCulture, $";{question.Name}\t\t{DnsMnemonics.ClassMnemonic(question.Class)}\t{DnsMnemonics.TypeMnemonic(question.Type)}\n");
            }
        }

        AppendSection(text, "ANSWER", message.Answers);
        AppendSection(text, "AUTHORITY", message.Authority);
        AppendSection(text, "ADDITIONAL", message.Additional);
        return text.ToString();
    }

    /// <summary>Gives a record's RDATA in presentation format: its fields separated by spaces.</summary>
    /// <param name="data">The record's fields.</param>
    /// <returns>The presentation text.</returns>
    public static string RecordData(IReadOnlyList<DnsRecordField> data) => string.Join(' ', data.Select(FieldText));

    private static void AppendSection(StringBuilder text, string title, IReadOnlyList<DnsResourceRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"\n;; {title} SECTION:\n");
        foreach (DnsResourceRecord record in records)
        {
            text.Append(CultureInfo.InvariantCulture, $"{record.Name}\t{record.TimeToLive}\t{DnsMnemonics.ClassMnemonic(record.Class)}\t")
                .Append(CultureInfo.InvariantCulture, $"{DnsMnemonics.TypeMnemonic(record.Type)}\t{RecordData(record.Data)}\n");
        }
    }

    private static string FieldText(DnsRecordField field) =>
        field.Kind == DnsFieldKind.CharacterStrings
            ? string.Join(' ', field.Values.Select(value => "\"" + value + "\""))
            : field.Values[0];
}
