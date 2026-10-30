namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>tus 1.0's <c>Tus-Resumable</c> field: the protocol version a request or response uses, on every message but <c>OPTIONS</c> (tus 1.0.0, "Tus-Resumable").</summary>
public static class TusResumableField
{
    /// <summary>The field name.</summary>
    public const string Name = "Tus-Resumable";

    /// <summary>The version this library speaks, the field value it sends.</summary>
    public const string Version = "1.0.0";

    /// <summary>Parses the field value.</summary>
    /// <param name="value">The value, or <see langword="null"/> when the field is absent.</param>
    /// <param name="version">The version, or an empty string when the value is malformed.</param>
    /// <returns>Whether the value is a version such as <c>1.0.0</c>.</returns>
    public static bool TryParse(string? value, out string version)
    {
        version = value is not null && TusList.IsVersion(value) ? value : string.Empty;
        return version.Length > 0;
    }
}
