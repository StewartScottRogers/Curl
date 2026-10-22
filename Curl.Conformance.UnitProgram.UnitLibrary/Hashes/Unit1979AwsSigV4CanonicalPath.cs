using System.Text;
using Curl.Authentication;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1979.c</c>: AWS SigV4's <c>canon_path</c>, here
/// <see cref="AwsSigV4UriEncoding.CanonicalPath"/>. curl normalizes the path for every service but S3, so a
/// row that does not normalize runs with the service <c>s3</c> and one that does with <c>execute-api</c>.
/// </summary>
public sealed class Unit1979AwsSigV4CanonicalPath : IUnitProgram
{
    private static readonly (string Name, bool Normalize, string UrlPart, string CanonicalUrl)[] Rows =
    [
        ("test-equals-encode", true, "/a=b", "/a%3Db"),
        ("test-equals-noencode", false, "/a=b", "/a=b"),
        ("test-s3-tables", true,
            "/tables/arn%3Aaws%3As3tables%3Aus-east-1%3A022954301426%3Abucket%2Fjasoehartablebucket/jasoeharnamespace/jasoehartable/encryption",
            "/tables/arn%253Aaws%253As3tables%253Aus-east-1%253A022954301426%253Abucket%252Fjasoehartablebucket/jasoeharnamespace/jasoehartable/encryption"),
        ("get-vanilla", true, "/", "/"),
        ("get-unreserved", true, "/-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz", "/-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"),
        ("get-slashes-unnormalized", false, "//example//", "//example//"),
        ("get-space-normalized", true, "/example space/", "/example%20space/"),
        ("get-plus-normalized", true, "/example+space/", "/example%2Bspace/"),
        ("get-slash-dot-slash-unnormalized", false, "/./", "/./"),
        ("get-slash-unnormalized", false, "//", "//"),
        ("get-relative-relative-unnormalized", false, "/example1/example2/../..", "/example1/example2/../.."),
    ];

    /// <inheritdoc />
    public string ProgramName => "unit1979";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        foreach ((string name, bool normalize, string urlPart, string canonicalUrl) in Rows)
        {
            string service = normalize ? "execute-api" : "s3";
            string received = AwsSigV4UriEncoding.CanonicalPath(urlPart, service, Encoding.UTF8);
            checks.FailUnless(received == canonicalUrl, $"{name}: Received \"{received}\" and should be \"{canonicalUrl}\", normalize ({Convert.ToInt32(normalize)})");
        }

        return Task.FromResult(checks.Result);
    }
}
