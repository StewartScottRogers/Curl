using System.Globalization;

namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>tus 1.0's <c>Upload-Length</c> field: a non-negative decimal integer, the total length of the upload in bytes (tus 1.0.0, "Upload-Length").</summary>
public static class TusUploadLengthField
{
    /// <summary>The field name.</summary>
    public const string Name = "Upload-Length";

    /// <summary>Formats the field value.</summary>
    /// <param name="length">The value, zero or more.</param>
    /// <returns>The decimal digits.</returns>
    public static string Format(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return length.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="length">The parsed value, or zero when the value is malformed.</param>
    /// <returns>Whether the value is decimal digits only.</returns>
    public static bool TryParse(string? value, out long length)
    {
        length = 0;
        return value is not null && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out length);
    }
}
