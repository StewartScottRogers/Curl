namespace Curl.Extension.S3.Xml;

/// <summary>A multipart upload S3 has started, read from its <c>InitiateMultipartUploadResult</c> body.</summary>
/// <param name="Bucket">The bucket the upload writes to.</param>
/// <param name="Key">The key the upload writes.</param>
/// <param name="UploadId">The identifier every later part and the completion name.</param>
public sealed record S3MultipartUpload(string Bucket, string Key, string UploadId);
