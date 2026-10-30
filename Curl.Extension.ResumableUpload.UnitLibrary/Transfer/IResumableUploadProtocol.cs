namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// What tus 1.0 and the IETF resumable upload draft send at each step of <see cref="ResumableUploadFlow" />: upload
/// creation, append (always <c>PATCH</c>) and offset retrieval (always <c>HEAD</c>).
/// </summary>
internal interface IResumableUploadProtocol
{
    /// <summary>Gets the method that creates the upload.</summary>
    string CreationMethod { get; }

    /// <summary>Gets the header fields of an offset retrieval request.</summary>
    IReadOnlyList<KeyValuePair<string, string>> OffsetRetrievalHeaders { get; }

    /// <summary>Gives the header fields of the request that creates an upload.</summary>
    /// <param name="length">The upload's length in bytes.</param>
    /// <returns>The fields.</returns>
    IReadOnlyList<KeyValuePair<string, string>> CreationHeaders(long length);

    /// <summary>Gives the header fields of a request appending the rest of the upload.</summary>
    /// <param name="offset">Where the appended content starts.</param>
    /// <returns>The fields.</returns>
    IReadOnlyList<KeyValuePair<string, string>> AppendHeaders(long offset);

    /// <summary>Reads the offset an offset retrieval response gives.</summary>
    /// <param name="response">The response.</param>
    /// <param name="offset">The offset, when there is a valid one.</param>
    /// <returns>Whether the response gave a valid offset.</returns>
    bool TryReadOffset(ResumableUploadExchange response, out long offset);
}
