namespace Curl.Extension.S3.Signing;

/// <summary>The AWS access key that signs an S3 request.</summary>
/// <param name="AccessKeyId">The access key ID, which the <c>Authorization</c> header names.</param>
/// <param name="SecretAccessKey">The secret access key, from which the signing key is derived.</param>
/// <param name="Region">The region the request is sent to, such as <c>us-east-1</c>.</param>
public sealed record S3Credentials(string AccessKeyId, string SecretAccessKey, string Region);
