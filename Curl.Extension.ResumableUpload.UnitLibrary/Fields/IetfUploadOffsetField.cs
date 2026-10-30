namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The resumable upload draft's <c>Upload-Offset</c> field: a non-negative sf-integer, the number of bytes of the upload the server holds, where the next append starts (draft-ietf-httpbis-resumable-upload §8.1).</summary>
public static class IetfUploadOffsetField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Offset";

    /// <summary>Formats the field value.</summary>
    /// <param name="offset">The value, from zero to 999,999,999,999,999 (RFC 9651 §3.3.1).</param>
    /// <returns>The decimal digits.</returns>
    public static string Format(long offset) => StructuredFieldItem.FormatInteger(offset, nameof(offset));

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="offset">The parsed value, or zero when the value is malformed.</param>
    /// <returns>Whether the value is a non-negative sf-integer.</returns>
    public static bool TryParse(string? value, out long offset)
    {
        offset = 0;
        return value is not null && StructuredFieldItem.TryParseInteger(value, out offset);
    }
}
