namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The resumable upload draft's <c>Upload-Complete</c> field: an sf-boolean saying whether the request or response carries the upload's last byte (draft-ietf-httpbis-resumable-upload §8.3).</summary>
public static class IetfUploadCompleteField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Complete";

    /// <summary>Formats the field value.</summary>
    /// <param name="complete">Whether the upload is complete.</param>
    /// <returns><c>?1</c> or <c>?0</c>.</returns>
    public static string Format(bool complete) => complete ? "?1" : "?0";

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="complete">Whether the upload is complete.</param>
    /// <returns>Whether the value is <c>?0</c> or <c>?1</c>; <see langword="false"/> for anything else.</returns>
    public static bool TryParse(string? value, out bool complete)
    {
        complete = false;
        return value is not null && StructuredFieldItem.TryParseBoolean(value, out complete);
    }
}
