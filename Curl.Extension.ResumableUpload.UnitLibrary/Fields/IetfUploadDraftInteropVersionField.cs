namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The resumable upload draft's <c>Upload-Draft-Interop-Version</c> field: a non-negative sf-integer, the interoperability version of the draft a client implements, such as 6 (draft-ietf-httpbis-resumable-upload, the note on draft interop versions).</summary>
public static class IetfUploadDraftInteropVersionField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Draft-Interop-Version";

    /// <summary>Formats the field value.</summary>
    /// <param name="version">The value, from zero to 999,999,999,999,999 (RFC 9651 §3.3.1).</param>
    /// <returns>The decimal digits.</returns>
    public static string Format(long version) => StructuredFieldItem.FormatInteger(version, nameof(version));

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="version">The parsed value, or zero when the value is malformed.</param>
    /// <returns>Whether the value is a non-negative sf-integer.</returns>
    public static bool TryParse(string? value, out long version)
    {
        version = 0;
        return value is not null && StructuredFieldItem.TryParseInteger(value, out version);
    }
}
