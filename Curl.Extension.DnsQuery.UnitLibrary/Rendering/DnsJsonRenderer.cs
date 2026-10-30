using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Curl.Extension.DnsQuery.Wire;

namespace Curl.Extension.DnsQuery.Rendering;

/// <summary>
/// Writes a decoded message as one JSON object with <see cref="Utf8JsonWriter" /> (no serializer, so no reflection):
/// <c>id</c>, <c>opcode</c>, <c>status</c>, <c>flags</c>, then the <c>question</c>, <c>answer</c>, <c>authority</c>
/// and <c>additional</c> arrays. A record's <c>data</c> object holds its fields by name: a number field as a JSON
/// number, a character-string field as an array of strings, any other field as a string, each in presentation format.
/// </summary>
internal static class DnsJsonRenderer
{
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Writes a message as JSON, on one line with no final line feed.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The JSON text.</returns>
    public static string Render(DnsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", message.Header.Id);
            writer.WriteString("opcode", DnsMnemonics.OpcodeMnemonic(message.Header.Opcode));
            writer.WriteString("status", DnsMnemonics.ResponseCodeMnemonic(message.Header.ResponseCode));
            WriteFlags(writer, message.Header);
            writer.WriteStartArray("question");
            foreach (DnsQuestion question in message.Questions)
            {
                writer.WriteStartObject();
                writer.WriteString("name", question.Name);
                writer.WriteString("type", DnsMnemonics.TypeMnemonic(question.Type));
                writer.WriteString("class", DnsMnemonics.ClassMnemonic(question.Class));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            WriteRecords(writer, "answer", message.Answers);
            WriteRecords(writer, "authority", message.Authority);
            WriteRecords(writer, "additional", message.Additional);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteFlags(Utf8JsonWriter writer, DnsHeader header)
    {
        writer.WriteStartArray("flags");
        foreach (string name in header.SetFlagNames())
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
    }

    private static void WriteRecords(Utf8JsonWriter writer, string section, IReadOnlyList<DnsResourceRecord> records)
    {
        writer.WriteStartArray(section);
        foreach (DnsResourceRecord record in records)
        {
            writer.WriteStartObject();
            writer.WriteString("name", record.Name);
            writer.WriteString("type", DnsMnemonics.TypeMnemonic(record.Type));
            writer.WriteString("class", DnsMnemonics.ClassMnemonic(record.Class));
            writer.WriteNumber("ttl", record.TimeToLive);
            writer.WriteStartObject("data");
            foreach (DnsRecordField field in record.Data)
            {
                WriteField(writer, field);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteField(Utf8JsonWriter writer, DnsRecordField field)
    {
        switch (field.Kind)
        {
            case DnsFieldKind.Number:
                writer.WritePropertyName(field.Name);
                writer.WriteRawValue(field.Values[0]);
                break;
            case DnsFieldKind.CharacterStrings:
                writer.WriteStartArray(field.Name);
                foreach (string value in field.Values)
                {
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteString(field.Name, field.Values[0]);
                break;
        }
    }
}
