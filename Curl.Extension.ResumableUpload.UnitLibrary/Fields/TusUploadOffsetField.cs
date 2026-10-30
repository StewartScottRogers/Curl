using System.Globalization;

namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>tus 1.0's <c>Upload-Offset</c> field: a non-negative decimal integer, the byte offset within the upload the server holds, where a PATCH body starts (tus 1.0.0, "Upload-Offset").</summary>
public static class TusUploadOffsetField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Offset";

    /// <summary>Formats the field value.</summary>
    /// <param name="offset">The value, zero or more.</param>
    /// <returns>The decimal digits.</returns>
    public static string Format(long offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        return offset.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="offset">The parsed value, or zero when the value is malformed.</param>
    /// <returns>Whether the value is decimal digits only.</returns>
    public static bool TryParse(string? value, out long offset)
    {
        offset = 0;
        return value is not null && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
    }
}
