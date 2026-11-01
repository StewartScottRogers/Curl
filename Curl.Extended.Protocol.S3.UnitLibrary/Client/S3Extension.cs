using Curl.Extension.Abstractions.Description;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>The <c>s3://</c> extension's description and handler factory (ADR-1090).</summary>
public static class S3Extension
{
    /// <summary>The size of each part of a multipart upload, and the size from which an upload is sent in parts: 8 MiB, the AWS CLI's default.</summary>
    internal const int DefaultPartSize = 8 * 1024 * 1024;

    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say of it.</summary>
    public static ExtensionDescription Description { get; } = new(
        "s3",
        "Reads and writes Amazon S3 objects (and MinIO or R2) at s3://bucket/key, signed with SigV4: a key downloads the object, -T uploads it (in 8 MiB parts from 8 MiB), an empty key or one ending in / lists the keys under it; --user ACCESS:SECRET or AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY sign, AWS_REGION picks the region and AWS_ENDPOINT_URL another service.",
        ["s3"],
        [],
        new Uri("https://docs.aws.amazon.com/AmazonS3/latest/API/Welcome.html"));

    /// <summary>Creates the handler over curl's connector, reading the AWS settings from the process environment.</summary>
    /// <param name="services">Curl's connection services.</param>
    /// <returns>The handler.</returns>
    public static IProtocolHandler CreateHandler(ProtocolHandlerServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new S3ProtocolHandler(services.Connector, Environment.GetEnvironmentVariable, DefaultPartSize);
    }
}
