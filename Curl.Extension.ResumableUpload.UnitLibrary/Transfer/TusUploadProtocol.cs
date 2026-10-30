using Curl.Extension.ResumableUpload.Fields;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// tus 1.0: "Creation" (<c>POST</c> with <c>Upload-Length</c>), and the "Core Protocol"'s <c>PATCH</c> with
/// <c>Content-Type: application/offset+octet-stream</c> and <c>Upload-Offset</c> and <c>HEAD</c> for the offset, every
/// request with <c>Tus-Resumable: 1.0.0</c>.
/// </summary>
internal sealed class TusUploadProtocol : IResumableUploadProtocol
{
    /// <summary>The one instance.</summary>
    public static readonly TusUploadProtocol Instance = new();

    private static readonly KeyValuePair<string, string> Resumable = new(TusResumableField.Name, TusResumableField.Version);

    private TusUploadProtocol()
    {
    }

    /// <inheritdoc />
    public string CreationMethod => "POST";

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> OffsetRetrievalHeaders => [Resumable];

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> CreationHeaders(long length) =>
        [Resumable, new(TusUploadLengthField.Name, TusUploadLengthField.Format(length))];

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> AppendHeaders(long offset) =>
        [Resumable, new(TusUploadOffsetField.Name, TusUploadOffsetField.Format(offset)), new("Content-Type", ResumableUploadMediaTypes.TusOffsetOctetStream)];

    /// <inheritdoc />
    public bool TryReadOffset(ResumableUploadExchange response, out long offset) =>
        TusUploadOffsetField.TryParse(response.FindHeader(TusUploadOffsetField.Name), out offset);
}
