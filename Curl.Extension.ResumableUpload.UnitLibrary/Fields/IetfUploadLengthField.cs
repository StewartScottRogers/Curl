namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The resumable upload draft's <c>Upload-Length</c> field: a non-negative sf-integer, the upload's total length in bytes (draft-ietf-httpbis-resumable-upload §8.4).</summary>
public static class IetfUploadLengthField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Length";

    /// <summary>Formats the field value.</summary>
    /// <param name="length">The value, from zero to 999,999,999,999,999 (RFC 9651 §3.3.1).</param>
    /// <returns>The decimal digits.</returns>
    public static string Format(long length) => StructuredFieldItem.FormatInteger(length, nameof(length));

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="length">The parsed value, or zero when the value is malformed.</param>
    /// <returns>Whether the value is a non-negative sf-integer.</returns>
    public static bool TryParse(string? value, out long length)
    {
        length = 0;
        return value is not null && StructuredFieldItem.TryParseInteger(value, out length);
    }
}
