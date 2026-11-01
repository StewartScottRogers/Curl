using System.Xml.Linq;

namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>
/// Reads the <c>InitiateMultipartUploadResult</c> body of the Amazon S3 API reference
/// "CreateMultipartUpload": its <c>Bucket</c>, <c>Key</c> and <c>UploadId</c>.
/// </summary>
public static class S3InitiateMultipartUploadResultParser
{
    /// <summary>Reads <paramref name="xml"/> as the started upload.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The upload, or why the body could not be read.</returns>
    public static S3XmlParseResult<S3MultipartUpload> Parse(string xml) =>
        S3XmlBody.Parse(xml, "InitiateMultipartUploadResult", ReadUpload);

    private static S3MultipartUpload ReadUpload(XElement root) => new(
        S3XmlBody.RequiredText(root, "Bucket"),
        S3XmlBody.RequiredText(root, "Key"),
        S3XmlBody.RequiredText(root, "UploadId"));
}
