using Curl.Extension.ResumableUpload.Fields;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// draft-ietf-httpbis-resumable-upload: upload creation with <c>Upload-Complete: ?0</c> and <c>Upload-Length</c>, upload
/// append (<c>PATCH</c>, <c>Content-Type: application/partial-upload</c>, <c>Upload-Offset</c>, <c>Upload-Complete: ?1</c>)
/// and offset retrieval (<c>HEAD</c>), every request with <c>Upload-Draft-Interop-Version</c>.
/// </summary>
/// <param name="creationMethod">The method curl's options give the transfer: <c>PUT</c> for <c>-T</c>, or <c>-X</c>'s.</param>
internal sealed class IetfUploadProtocol(string creationMethod) : IResumableUploadProtocol
{
    /// <summary>The draft interop version this client implements (draft-ietf-httpbis-resumable-upload-05).</summary>
    public const long InteropVersion = 6;

    private static readonly KeyValuePair<string, string> Interop =
        new(IetfUploadDraftInteropVersionField.Name, IetfUploadDraftInteropVersionField.Format(InteropVersion));

    /// <inheritdoc />
    public string CreationMethod => creationMethod;

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> OffsetRetrievalHeaders => [Interop];

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> CreationHeaders(long length) =>
        [Interop, new(IetfUploadCompleteField.Name, IetfUploadCompleteField.Format(false)), new(IetfUploadLengthField.Name, IetfUploadLengthField.Format(length))];

    /// <inheritdoc />
    public IReadOnlyList<KeyValuePair<string, string>> AppendHeaders(long offset) =>
    [
        Interop,
        new(IetfUploadCompleteField.Name, IetfUploadCompleteField.Format(true)),
        new(IetfUploadOffsetField.Name, IetfUploadOffsetField.Format(offset)),
        new("Content-Type", ResumableUploadMediaTypes.PartialUpload),
    ];

    /// <inheritdoc />
    public bool TryReadOffset(ResumableUploadExchange response, out long offset) =>
        IetfUploadOffsetField.TryParse(response.FindHeader(IetfUploadOffsetField.Name), out offset);
}
