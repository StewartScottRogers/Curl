using System.Text;

namespace Curl.Console;

/// <summary>
/// The Windows <see cref="IExtendedAttributeWriter" />: curl 8.22's <c>--xattr</c> on Windows writes no
/// extended attributes but the <c>Zone.Identifier</c> alternate data stream, <c>[ZoneTransfer]</c>
/// followed by <c>HostUrl=</c> (the URL without credentials) and, when a <c>Referer</c> was sent,
/// <c>ReferrerUrl=</c> (ADR-1287).
/// </summary>
/// <remarks>
/// <see cref="OutputFileExtendedAttributes" /> hands over the referrer before the origin URL, so the
/// referrer is kept until the origin URL arrives and both are written in one stream. The other
/// attributes (<c>user.creator</c>, <c>user.mime_type</c>) have no place in the stream and are accepted
/// without a write.
/// </remarks>
internal sealed class WindowsZoneIdentifierWriter : IExtendedAttributeWriter
{
    /// <summary>The name of the alternate data stream, after the file's path and a colon.</summary>
    internal const string StreamName = "Zone.Identifier";

    /// <summary>Writes a stream's text for a path; gives <see langword="null" /> on success, else the failure's text.</summary>
    private readonly Func<string, string, string?> writeStream;

    /// <summary>The referrer received for the file being written, until its origin URL arrives.</summary>
    private string? referrer;

    /// <summary>Creates a writer over one way of writing the stream.</summary>
    /// <param name="writeStream">Given the stream's path (<c>file:Zone.Identifier</c>) and text, writes it.</param>
    internal WindowsZoneIdentifierWriter(Func<string, string, string?> writeStream) => this.writeStream = writeStream;

    /// <inheritdoc />
    public bool TryWrite(string path, string name, string value, out string errorText)
    {
        errorText = string.Empty;
        if (name == OutputFileExtendedAttributes.ReferrerUrlName)
        {
            referrer = value;
            return true;
        }

        if (name != OutputFileExtendedAttributes.OriginUrlName)
        {
            return true;
        }

        StringBuilder text = new StringBuilder("[ZoneTransfer]\r\nHostUrl=").Append(value).Append("\r\n");
        if (referrer is not null)
        {
            text.Append("ReferrerUrl=").Append(referrer).Append("\r\n");
        }

        referrer = null;
        errorText = writeStream($"{path}:{StreamName}", text.ToString()) ?? string.Empty;
        return errorText.Length == 0;
    }
}
