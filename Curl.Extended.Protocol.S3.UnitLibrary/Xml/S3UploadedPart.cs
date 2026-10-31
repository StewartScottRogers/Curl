namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>A part of a multipart upload S3 has accepted.</summary>
/// <param name="PartNumber">The part's number, from 1.</param>
/// <param name="ETag">The entity tag S3 returned for the part, quotes included as S3 sent it.</param>
public sealed record S3UploadedPart(int PartNumber, string ETag);
