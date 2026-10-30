namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>The media types and interim status code the resumable upload protocols put on the wire.</summary>
public static class ResumableUploadMediaTypes
{
    /// <summary>The <c>Content-Type</c> of an append request in the resumable upload draft (draft-ietf-httpbis-resumable-upload §12.2).</summary>
    public const string PartialUpload = "application/partial-upload";

    /// <summary>The <c>Content-Type</c> of a tus 1.0 <c>PATCH</c> request (tus 1.0.0, "PATCH").</summary>
    public const string TusOffsetOctetStream = "application/offset+octet-stream";

    /// <summary>The interim status code telling a client the server supports resumable uploads and where the upload resource is (draft-ietf-httpbis-resumable-upload §4.2).</summary>
    public const int UploadResumptionSupportedStatusCode = 104;
}
