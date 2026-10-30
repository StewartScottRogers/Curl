using System.Net;
using Curl.Extension.S3.Signing;

namespace Curl.Extension.S3.Client;

/// <summary>
/// Where and as whom an <c>s3://</c> transfer talks to S3 (ADR-1090): the access key from <c>--user</c> or from
/// <c>AWS_ACCESS_KEY_ID</c> and <c>AWS_SECRET_ACCESS_KEY</c> (none means unsigned, anonymous requests), the session token
/// from <c>AWS_SESSION_TOKEN</c>, the region from <c>AWS_REGION</c> or <c>AWS_DEFAULT_REGION</c> (else <c>us-east-1</c>),
/// and another service's endpoint from <c>AWS_ENDPOINT_URL_S3</c> or <c>AWS_ENDPOINT_URL</c>.
/// </summary>
/// <param name="Credentials">The access key to sign with, or <see langword="null"/> to send requests unsigned.</param>
/// <param name="SessionToken">The temporary credentials' session token, sent as <c>x-amz-security-token</c>, or <see langword="null"/>.</param>
/// <param name="Region">The region requests are signed for and, on AWS, sent to.</param>
/// <param name="EndpointUrl">The endpoint of an S3-compatible service such as MinIO or R2, or <see langword="null"/> for AWS.</param>
internal sealed record S3Configuration(S3Credentials? Credentials, string? SessionToken, string Region, string? EndpointUrl)
{
    /// <summary>The region used when the environment names none.</summary>
    internal const string DefaultRegion = "us-east-1";

    /// <summary>Reads the configuration from <paramref name="environment"/> and the <c>--user</c> credentials.</summary>
    /// <param name="environment">Gives an environment variable's value, or <see langword="null"/> when it is unset.</param>
    /// <param name="user">The <c>--user</c> credentials, whose name is the access key ID and password the secret, or <see langword="null"/>.</param>
    /// <returns>The configuration.</returns>
    internal static S3Configuration From(Func<string, string?> environment, NetworkCredential? user)
    {
        string region = environment("AWS_REGION") ?? environment("AWS_DEFAULT_REGION") ?? DefaultRegion;
        string? endpointUrl = environment("AWS_ENDPOINT_URL_S3") ?? environment("AWS_ENDPOINT_URL");
        return user is null
            ? new(CredentialsFrom(environment, region), environment("AWS_SESSION_TOKEN"), region, endpointUrl)
            : new(new S3Credentials(user.UserName, user.Password, region), null, region, endpointUrl);
    }

    private static S3Credentials? CredentialsFrom(Func<string, string?> environment, string region) =>
        (environment("AWS_ACCESS_KEY_ID"), environment("AWS_SECRET_ACCESS_KEY")) is (string accessKeyId, string secretAccessKey)
            ? new S3Credentials(accessKeyId, secretAccessKey, region)
            : null;
}
